using System.Globalization;
using System.Text.Json;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using DecisionSimulator.Infrastructure.Simulation.Correlation;
using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.Engines;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;

namespace DecisionSimulator.Infrastructure.Simulation.Orchestration;

/// <summary>
/// A scenario variable paired with the stable key of the definition it belongs
/// to. The orchestrator needs the key, not the display name, to bind the value
/// to a model input.
/// </summary>
public sealed record ResolvedVariable(string Key, ScenarioVariable Variable);

/// <summary>Everything the orchestrator needs to execute one scenario.</summary>
public sealed class SimulationRequest
{
    public required Scenario Scenario { get; init; }

    /// <summary>Variables of the scenario, each paired with its definition key.</summary>
    public required IReadOnlyList<ResolvedVariable> Variables { get; init; }

    /// <summary>Correlations declared between variable keys. Empty means independent sampling.</summary>
    public IReadOnlyList<CorrelationAssumption> Correlations { get; init; } = [];

    public required MonteCarloOptions Options { get; init; }

    /// <summary>Currency the scenario is expressed in. Never assumed.</summary>
    public required string CurrencyCode { get; init; }
}

/// <summary>
/// The outcome of an orchestrated run: the persisted run entity plus the
/// in-memory result the UI renders.
/// </summary>
public sealed class SimulationOutcome
{
    public required SimulationRun Run { get; init; }

    public required MonteCarloResult Result { get; init; }

    /// <summary>
    /// Inputs the model required but the scenario did not supply. Empty on a
    /// successful run. Reported rather than defaulted, because inventing a
    /// missing assumption would silently change the answer.
    /// </summary>
    public required IReadOnlyCollection<string> MissingInputs { get; init; }

    /// <summary>Assumptions the model declares. Must be shown with the result.</summary>
    public required IReadOnlyCollection<string> ModelAssumptions { get; init; }
}

/// <summary>
/// Binds a persisted scenario to a simulation model, runs it, and records the
/// run on the scenario.
/// <para>
/// The orchestrator is the only place that knows how scenario variables become
/// model inputs. Keeping that mapping in one place means a model can never
/// silently read a value the user did not supply, and every run carries a
/// frozen input snapshot that makes it reproducible.
/// </para>
/// </summary>
public sealed class SimulationOrchestrator
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ISimulationModel _model;

    public SimulationOrchestrator(ISimulationModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ISimulationModel Model => _model;

    /// <summary>
    /// Validates the request without running anything. Used by the UI to show
    /// missing assumptions before the user commits to a run.
    /// </summary>
    public IReadOnlyCollection<string> FindMissingInputs(SimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplied = new HashSet<string>(
            request.Variables.Select(v => v.Key),
            StringComparer.OrdinalIgnoreCase);

        return _model.RequiredInputs
            .Where(required => !supplied.Contains(required))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Executes the run and records it on the scenario.
    /// <para>
    /// A run with missing inputs is recorded as failed rather than executed with
    /// substituted values. The returned outcome still carries the missing keys
    /// so the caller can tell the user exactly what to supply.
    /// </para>
    /// </summary>
    public SimulationOutcome Run(SimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missing = FindMissingInputs(request);
        var baseInputs = BuildInputs(request);
        var snapshotJson = SerializeSnapshot(request, baseInputs);

        var run = request.Scenario.RegisterRun(
            simulationModelId: Guid.Empty,
            modelVersion: _model.ModelVersion,
            runType: SimulationRunType.MonteCarlo,
            iterationCount: request.Options.Iterations,
            randomSeed: request.Options.Seed,
            inputSnapshotJson: snapshotJson);

        if (missing.Count > 0)
        {
            run.Fail(
                "The scenario is missing required assumptions: " +
                string.Join(", ", missing) + ".");

            return new SimulationOutcome
            {
                Run = run,
                Result = EmptyResult(request.Options),
                MissingInputs = missing,
                ModelAssumptions = _model.Assumptions
            };
        }

        run.MarkRunning();

        try
        {
            var samplingMap = BuildSamplingMap(request);
            var runner = new MonteCarloRunner(_model);
            var result = runner.Run(baseInputs, samplingMap, request.Options, request.Correlations);

            run.Complete(SerializeResult(result));
            request.Scenario.MarkSimulated();

            return new SimulationOutcome
            {
                Run = run,
                Result = result,
                MissingInputs = [],
                ModelAssumptions = _model.Assumptions
            };
        }
        catch (Exception ex) when (ex is MissingInputException or InvalidDistributionException)
        {
            // These are user-fixable input problems, not engine faults. Record
            // the message verbatim so the UI can show it without translation.
            run.Fail(ex.Message);

            return new SimulationOutcome
            {
                Run = run,
                Result = EmptyResult(request.Options),
                MissingInputs = missing,
                ModelAssumptions = _model.Assumptions
            };
        }
    }

    private static SimulationInputs BuildInputs(SimulationRequest request)
    {
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, variable) in request.Variables)
        {
            values[key] = variable.Value;
        }

        return new SimulationInputs(values, request.Scenario.TimeHorizonMonths, request.CurrencyCode);
    }

    /// <summary>
    /// Only variables the model actually consumes are sampled. Sampling an
    /// unused variable would inflate the reported uncertainty for no reason.
    /// </summary>
    private Dictionary<string, IDistribution> BuildSamplingMap(SimulationRequest request)
    {
        var required = new HashSet<string>(_model.RequiredInputs, StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, IDistribution>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, variable) in request.Variables)
        {
            if (!required.Contains(key))
            {
                continue;
            }

            map[key] = DistributionFactory.Create(variable);
        }

        return map;
    }

    private string SerializeSnapshot(SimulationRequest request, SimulationInputs inputs)
    {
        var snapshot = new
        {
            modelKey = _model.ModelKey,
            modelVersion = _model.ModelVersion,
            scenarioId = request.Scenario.Id,
            assumptionSetVersion = request.Scenario.AssumptionSetVersion,
            timeHorizonMonths = inputs.TimeHorizonMonths,
            currencyCode = inputs.CurrencyCode,
            iterations = request.Options.Iterations,
            seed = request.Options.Seed,
            inputs = inputs.Values,
            variables = request.Variables.Select(v => new
            {
                key = v.Key,
                value = v.Variable.Value,
                distribution = v.Variable.DistributionType.ToString(),
                minimum = v.Variable.MinimumValue,
                maximum = v.Variable.MaximumValue,
                standardDeviation = v.Variable.StandardDeviation,
                confidence = v.Variable.Confidence.ToString(),
                sourceType = v.Variable.SourceType.ToString(),
                sourceReference = v.Variable.SourceReference,
                distributionBasis = v.Variable.DistributionBasis.ToString(),
                isUserOverride = v.Variable.IsUserOverride
            }),
            correlations = request.Correlations.Select(c => new
            {
                variableA = c.VariableA,
                variableB = c.VariableB,
                coefficient = c.Coefficient,
                basis = c.Basis,
                sourceReference = c.SourceReference,
                isUserSupplied = c.IsUserSupplied
            })
        };

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    private static string SerializeResult(MonteCarloResult result)
    {
        var summary = new
        {
            iterations = result.Iterations,
            seed = result.Seed,
            modelKey = result.ModelKey,
            modelVersion = result.ModelVersion,
            shareOfPathsWithNegativeCash = result.ShareOfPathsWithNegativeCash,
            isEffectivelyDeterministic = result.IsEffectivelyDeterministic,
            correlationHonoured = result.CorrelationHonoured,
            warningCounts = result.WarningCounts,
            metrics = result.Metrics.Select(m => new
            {
                key = m.MetricKey,
                mean = m.Mean,
                standardDeviation = m.StandardDeviation,
                minimum = m.Minimum,
                maximum = m.Maximum,
                percentiles = m.Percentiles.ToDictionary(
                    p => p.Key.ToString(CultureInfo.InvariantCulture),
                    p => p.Value),
                sampleCount = m.SampleCount
            })
        };

        return JsonSerializer.Serialize(summary, SnapshotJsonOptions);
    }

    private static MonteCarloResult EmptyResult(MonteCarloOptions options) => new()
    {
        Iterations = options.Iterations,
        Seed = options.Seed,
        ModelKey = string.Empty,
        ModelVersion = string.Empty,
        Metrics = [],
        CashBands = [],
        ShareOfPathsWithNegativeCash = 0m,
        WarningCounts = new Dictionary<string, int>(),
        IsEffectivelyDeterministic = true
    };
}
