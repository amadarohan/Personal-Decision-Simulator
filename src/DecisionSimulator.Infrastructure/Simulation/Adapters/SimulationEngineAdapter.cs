using DecisionSimulator.Application.Features.Simulations;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using DecisionSimulator.Infrastructure.Simulation.Correlation;
using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.Engines;
using DecisionSimulator.Infrastructure.Simulation.Engines.Models;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;
using DecisionSimulator.Infrastructure.Simulation.Sensitivity;
using DecisionSimulator.Infrastructure.Simulation.StressTesting;

namespace DecisionSimulator.Infrastructure.Simulation.Adapters;

/// <summary>
/// Adapts the deterministic simulation engine to the application layer's
/// <see cref="ISimulationEngine"/> contract.
/// <para>
/// The adapter exists so the application layer never references the engine's
/// own types. It also translates the engine's exceptions into the application's
/// vocabulary, which keeps the dependency direction pointing inward.
/// </para>
/// </summary>
public sealed class SimulationEngineAdapter : ISimulationEngine
{
    private readonly Dictionary<string, ISimulationModel> _models;

    public SimulationEngineAdapter()
    {
        var models = new ISimulationModel[]
        {
            new CashFlowProjectionModel(),
            new DebtAmortizationModel(),
            new BusinessViabilityModel(),
            new RelocationComparisonModel()
        };

        _models = models.ToDictionary(m => m.ModelKey, StringComparer.OrdinalIgnoreCase);

        AvailableModels = models
            .Select(m => new SimulationModelInfoDto(
                m.ModelKey,
                DisplayNameFor(m.ModelKey),
                m.ModelVersion,
                m.ModelType,
                m.RequiredInputs.ToList(),
                m.Assumptions.ToList()))
            .ToList();
    }

    public IReadOnlyList<SimulationModelInfoDto> AvailableModels { get; }

    public SimulationModelInfoDto? FindModel(string modelKey) =>
        string.IsNullOrWhiteSpace(modelKey)
            ? null
            : AvailableModels.FirstOrDefault(m =>
                string.Equals(m.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase));

    public EngineRunResult Run(string modelKey, EngineRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = GetModel(modelKey);
        var inputs = BuildInputs(request);

        // Report every missing input at once. The engine throws on the first one
        // it needs, which would force the user to fix the scenario one key per
        // run. Checking up front lets the UI name them all in a single pass.
        var missingInputs = model.RequiredInputs
            .Where(key => !inputs.Has(key))
            .ToList();

        if (missingInputs.Count > 0)
        {
            return new EngineRunResult(
                model.ModelKey,
                model.ModelVersion,
                [],
                [],
                0m,
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                false,
                null,
                model.Assumptions.ToList(),
                missingInputs);
        }

        var samplingMap = BuildSamplingMap(model, request);

        var options = new MonteCarloOptions
        {
            Iterations = request.Iterations,
            Seed = request.Seed
        };

        var correlations = BuildCorrelations(request);

        MonteCarloResult result;

        try
        {
            var runner = new MonteCarloRunner(model);
            result = runner.Run(inputs, samplingMap, options, correlations);
        }
        catch (MissingInputException ex)
        {
            throw new MissingAssumptionException(ex.Key);
        }
        catch (InvalidDistributionException ex)
        {
            throw new InvalidAssumptionException(ex.Message);
        }

        return new EngineRunResult(
            model.ModelKey,
            model.ModelVersion,
            result.Metrics
                .Select(m => new EngineMetric(
                    m.MetricKey,
                    m.Mean,
                    m.StandardDeviation,
                    m.Minimum,
                    m.Maximum,
                    m.Percentiles,
                    m.SampleCount))
                .ToList(),
            result.CashBands
                .Select(b => new EngineBand(b.MonthIndex, b.Mean, b.Percentiles))
                .ToList(),
            result.ShareOfPathsWithNegativeCash,
            result.WarningCounts,
            result.IsEffectivelyDeterministic,
            result.CorrelationHonoured,
            model.Assumptions.ToList(),
            []);
    }

    public EngineSensitivityResult AnalyzeSensitivity(
        string modelKey,
        EngineRunRequest request,
        IReadOnlyDictionary<string, (decimal Low, decimal High)> ranges,
        string metricKey)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(ranges);

        var model = GetModel(modelKey);
        var inputs = BuildInputs(request);

        SensitivityResult analysis;

        try
        {
            var analyzer = new SensitivityAnalyzer(model);
            analysis = analyzer.Analyze(inputs, ranges, metricKey);
        }
        catch (MissingInputException ex)
        {
            throw new MissingAssumptionException(ex.Key);
        }

        return new EngineSensitivityResult(
            analysis.MetricKey,
            analysis.BaselineMetric,
            analysis.Entries
                .Select(e => new EngineSensitivityEntry(
                    e.VariableKey,
                    e.LowValue,
                    e.HighValue,
                    e.MetricAtLow,
                    e.MetricAtHigh,
                    e.Swing))
                .ToList(),
            analysis.IsMonotonic);
    }

    public EngineStressReport RunStressTests(string modelKey, EngineRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = GetModel(modelKey);
        var inputs = BuildInputs(request);

        StressTestReport report;

        try
        {
            var runner = new StressTestRunner(model);
            report = runner.Run(inputs, StressTestRunner.DefaultShocks);
        }
        catch (MissingInputException ex)
        {
            throw new MissingAssumptionException(ex.Key);
        }

        return new EngineStressReport(
            report.BaselineMetrics,
            report.Outcomes
                .Select(o => new EngineStressOutcome(
                    o.ShockKey,
                    o.ShockName,
                    o.Description,
                    o.Metrics,
                    o.MetricDeltas,
                    o.Warnings,
                    o.Survives))
                .ToList());
    }

    private ISimulationModel GetModel(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey) || !_models.TryGetValue(modelKey, out var model))
        {
            throw new ArgumentException(
                $"No simulation model is registered under the key '{modelKey}'.",
                nameof(modelKey));
        }

        return model;
    }

    private static SimulationInputs BuildInputs(EngineRunRequest request)
    {
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in request.Variables)
        {
            values[variable.Key] = variable.Value;
        }

        return new SimulationInputs(values, request.TimeHorizonMonths, request.CurrencyCode);
    }

    /// <summary>
    /// Only variables the model consumes are sampled. Sampling an unused
    /// variable would inflate the reported uncertainty for no reason.
    /// </summary>
    private static Dictionary<string, IDistribution> BuildSamplingMap(
        ISimulationModel model,
        EngineRunRequest request)
    {
        var required = new HashSet<string>(model.RequiredInputs, StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, IDistribution>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in request.Variables)
        {
            if (!required.Contains(variable.Key))
            {
                continue;
            }

            if (variable.DistributionType == DistributionType.Fixed)
            {
                continue;
            }

            map[variable.Key] = DistributionFactory.Create(
                variable.DistributionType,
                variable.Value,
                variable.MinimumValue,
                variable.MaximumValue,
                variable.StandardDeviation,
                variable.DiscreteOutcomesJson);
        }

        return map;
    }

    private static List<CorrelationAssumption>? BuildCorrelations(EngineRunRequest request)
    {
        if (request.Correlations is null || request.Correlations.Count == 0)
        {
            return null;
        }

        return request.Correlations
            .Select(c => new CorrelationAssumption
            {
                VariableA = c.VariableA,
                VariableB = c.VariableB,
                Coefficient = c.Coefficient,
                Basis = c.Basis.ToString(),
                SourceReference = c.SourceReference,
                IsUserSupplied = true
            })
            .ToList();
    }

    private static string DisplayNameFor(string modelKey) => modelKey switch
    {
        CashFlowProjectionModel.Key => "Cash flow projection",
        DebtAmortizationModel.Key => "Debt amortization",
        BusinessViabilityModel.Key => "Business viability",
        RelocationComparisonModel.Key => "Relocation comparison",
        _ => modelKey
    };
}
