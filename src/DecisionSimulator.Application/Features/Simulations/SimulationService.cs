using System.Text.Json;
using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Simulations;

/// <summary>
/// Runs scenarios through the deterministic engine and records the run.
/// <para>
/// The service owns the boundary between persisted assumptions and the engine.
/// It resolves variable keys, refuses to run when a required assumption is
/// missing, and stores a frozen input snapshot with every run so the result
/// stays reproducible after the user edits the scenario.
/// </para>
/// </summary>
public sealed class SimulationService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { WriteIndented = false };

    private readonly IApplicationDbContext _db;
    private readonly ISimulationEngine _engine;
    private readonly IUserPreferencesAccessor _preferences;

    public SimulationService(
        IApplicationDbContext db,
        ISimulationEngine engine,
        IUserPreferencesAccessor preferences)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public IReadOnlyList<SimulationModelInfoDto> AvailableModels => _engine.AvailableModels;

    /// <summary>
    /// Runs a scenario and persists the run. A run that cannot proceed is still
    /// recorded, with its failure reason, so the history shows what was attempted.
    /// </summary>
    public async Task<Result<SimulationResultDto>> RunAsync(
        RunSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = _engine.FindModel(request.ModelKey);
        if (model is null)
        {
            return Result<SimulationResultDto>.Failure(
                ErrorCodes.NotFound,
                $"No simulation model is registered under the key '{request.ModelKey}'.");
        }

        if (request.Iterations < 1)
        {
            return Result<SimulationResultDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A run needs at least one iteration.");
        }

        var scenario = await _db.Scenarios
            .Include(s => s.Variables)
            .FirstOrDefaultAsync(s => s.Id == request.ScenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result<SimulationResultDto>.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        var definitions = await _db.VariableDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var variables = ResolveVariables(scenario, definitions);

        var preferences = await _preferences.GetAsync(cancellationToken);

        var engineRequest = new EngineRunRequest(
            scenario.TimeHorizonMonths,
            preferences.DefaultCurrencyCode,
            variables,
            request.Iterations,
            request.Seed ?? Random.Shared.Next(),
            request.Correlations?
                .Select(c => new EngineCorrelation(c.VariableA, c.VariableB, c.Coefficient))
                .ToList());

        var snapshotJson = SerializeSnapshot(engineRequest);

        var run = scenario.RegisterRun(
            simulationModelId: Guid.Empty,
            modelVersion: model.ModelVersion,
            runType: SimulationRunType.MonteCarlo,
            iterationCount: request.Iterations,
            randomSeed: engineRequest.Seed,
            inputSnapshotJson: snapshotJson);

        run.MarkRunning();

        EngineRunResult result;

        try
        {
            result = _engine.Run(request.ModelKey, engineRequest);
        }
        catch (Exception ex) when (ex is InvalidAssumptionException or MissingAssumptionException)
        {
            // A malformed assumption is the user's to fix, so the message is
            // recorded verbatim rather than replaced with a generic failure.
            run.Fail(ex.Message);
            await _db.SaveChangesAsync(cancellationToken);

            return Result<SimulationResultDto>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        if (result.MissingInputs.Count > 0)
        {
            var message = "The scenario is missing required assumptions: " +
                string.Join(", ", result.MissingInputs) + ".";

            run.Fail(message);
            await _db.SaveChangesAsync(cancellationToken);

            return Result<SimulationResultDto>.Success(MapResult(run, result, model));
        }

        run.Complete(SerializeResult(result));
        scenario.MarkSimulated();

        await _db.SaveChangesAsync(cancellationToken);

        return Result<SimulationResultDto>.Success(MapResult(run, result, model));
    }

    /// <summary>Loads a previously recorded run and its stored summary.</summary>
    public async Task<Result<SimulationResultDto>> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.SimulationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        if (run is null)
        {
            return Result<SimulationResultDto>.Failure(ErrorCodes.NotFound, "The run could not be found.");
        }

        var model = _engine.AvailableModels.FirstOrDefault(m => m.ModelVersion == run.ModelVersion);

        return Result<SimulationResultDto>.Success(new SimulationResultDto(
            run.Id,
            run.ScenarioId,
            model?.ModelKey ?? string.Empty,
            run.ModelVersion,
            run.RunType,
            run.Status,
            run.IterationCount,
            run.RandomSeed,
            run.AssumptionSetVersion,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.ErrorMessage,
            [],
            [],
            0m,
            new Dictionary<string, int>(),
            false,
            null,
            model?.Assumptions ?? [],
            []));
    }

    public async Task<Result<SensitivityResultDto>> AnalyzeSensitivityAsync(
        Guid scenarioId,
        string modelKey,
        string metricKey,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(scenarioId, modelKey, cancellationToken);
        if (prepared.Failed)
        {
            return Result<SensitivityResultDto>.Failure(prepared.ErrorCode!, prepared.ErrorMessage!);
        }

        var (engineRequest, model) = prepared.Value!;

        // Only variables that declare a range can be varied. A fixed assumption
        // has no range to test, so it is reported as untested rather than
        // silently assigned an invented one.
        var ranges = engineRequest.Variables
            .Where(v => v.MinimumValue is not null && v.MaximumValue is not null)
            .ToDictionary(
                v => v.Key,
                v => (Low: v.MinimumValue!.Value, High: v.MaximumValue!.Value),
                StringComparer.OrdinalIgnoreCase);

        if (ranges.Count == 0)
        {
            return Result<SensitivityResultDto>.Failure(
                ErrorCodes.MissingAssumptions,
                "No assumption declares a range, so there is nothing to vary. Give at least one variable a minimum and maximum value.");
        }

        EngineSensitivityResult analysis;

        try
        {
            analysis = _engine.AnalyzeSensitivity(modelKey, engineRequest, ranges, metricKey);
        }
        catch (ArgumentException ex)
        {
            return Result<SensitivityResultDto>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        return Result<SensitivityResultDto>.Success(new SensitivityResultDto(
            scenarioId,
            model.ModelKey,
            analysis.MetricKey,
            analysis.Entries
                .Select(e => new SensitivityEntryDto(
                    e.VariableKey,
                    analysis.MetricKey,
                    e.LowValue,
                    e.HighValue,
                    e.MetricAtLow,
                    e.MetricAtHigh,
                    e.Swing))
                .ToList()));
    }

    public async Task<Result<StressTestReportDto>> RunStressTestsAsync(
        Guid scenarioId,
        string modelKey,
        string metricKey,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(scenarioId, modelKey, cancellationToken);
        if (prepared.Failed)
        {
            return Result<StressTestReportDto>.Failure(prepared.ErrorCode!, prepared.ErrorMessage!);
        }

        var (engineRequest, model) = prepared.Value!;

        EngineStressReport report;

        try
        {
            report = _engine.RunStressTests(modelKey, engineRequest);
        }
        catch (Exception ex) when (ex is MissingAssumptionException or InvalidAssumptionException)
        {
            return Result<StressTestReportDto>.Failure(ErrorCodes.MissingAssumptions, ex.Message);
        }

        if (!report.BaselineMetrics.TryGetValue(metricKey, out var baseline))
        {
            return Result<StressTestReportDto>.Failure(
                ErrorCodes.ValidationFailed,
                $"The model does not report the metric '{metricKey}'.");
        }

        var outcomes = new List<StressTestOutcomeDto>(report.Outcomes.Count);

        foreach (var outcome in report.Outcomes)
        {
            if (!outcome.Metrics.TryGetValue(metricKey, out var stressed))
            {
                continue;
            }

            var change = stressed - baseline;
            var changePercentage = baseline == 0m ? (decimal?)null : change / Math.Abs(baseline) * 100m;

            outcomes.Add(new StressTestOutcomeDto(
                outcome.ShockKey,
                outcome.ShockName,
                outcome.Description,
                baseline,
                stressed,
                change,
                changePercentage,
                outcome.Warnings));
        }

        return Result<StressTestReportDto>.Success(new StressTestReportDto(
            scenarioId,
            model.ModelKey,
            metricKey,
            baseline,
            outcomes));
    }

    /// <summary>
    /// Builds the engine request for a scenario, or explains why it cannot be built.
    /// </summary>
    private async Task<Result<(EngineRunRequest Request, SimulationModelInfoDto Model)>> PrepareAsync(
        Guid scenarioId,
        string modelKey,
        CancellationToken cancellationToken)
    {
        var model = _engine.FindModel(modelKey);
        if (model is null)
        {
            return Result<(EngineRunRequest, SimulationModelInfoDto)>.Failure(
                ErrorCodes.NotFound,
                $"No simulation model is registered under the key '{modelKey}'.");
        }

        var scenario = await _db.Scenarios
            .AsNoTracking()
            .Include(s => s.Variables)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result<(EngineRunRequest, SimulationModelInfoDto)>.Failure(
                ErrorCodes.NotFound,
                "The scenario could not be found.");
        }

        var definitions = await _db.VariableDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var variables = ResolveVariables(scenario, definitions);
        var preferences = await _preferences.GetAsync(cancellationToken);

        return Result<(EngineRunRequest, SimulationModelInfoDto)>.Success((
            new EngineRunRequest(scenario.TimeHorizonMonths, preferences.DefaultCurrencyCode, variables),
            model));
    }

    private static List<EngineVariable> ResolveVariables(
        Scenario scenario,
        Dictionary<Guid, VariableDefinition> definitions)
    {
        var variables = new List<EngineVariable>(scenario.Variables.Count);

        foreach (var variable in scenario.Variables)
        {
            if (!definitions.TryGetValue(variable.VariableDefinitionId, out var definition))
            {
                // A value whose definition was deleted cannot be bound to a model
                // input, so it is skipped rather than guessed at.
                continue;
            }

            variables.Add(new EngineVariable(
                definition.Key,
                variable.Value,
                variable.DistributionType,
                variable.MinimumValue,
                variable.MaximumValue,
                variable.StandardDeviation,
                variable.DiscreteOutcomesJson));
        }

        return variables;
    }

    private static string SerializeSnapshot(EngineRunRequest request) =>
        JsonSerializer.Serialize(new
        {
            request.TimeHorizonMonths,
            request.CurrencyCode,
            request.Iterations,
            request.Seed,
            Variables = request.Variables.Select(v => new
            {
                v.Key,
                v.Value,
                DistributionType = v.DistributionType.ToString(),
                v.MinimumValue,
                v.MaximumValue,
                v.StandardDeviation
            }),
            Correlations = request.Correlations?.Select(c => new
            {
                c.VariableA,
                c.VariableB,
                c.Coefficient
            })
        }, SnapshotJsonOptions);

    private static string SerializeResult(EngineRunResult result) =>
        JsonSerializer.Serialize(new
        {
            result.ModelKey,
            result.ModelVersion,
            result.ShareOfPathsWithNegativeCash,
            result.IsEffectivelyDeterministic,
            result.CorrelationHonoured,
            Metrics = result.Metrics.Select(m => new
            {
                m.MetricKey,
                m.Mean,
                m.StandardDeviation,
                m.Minimum,
                m.Maximum,
                m.SampleCount
            }),
            result.WarningCounts
        }, SnapshotJsonOptions);

    private static SimulationResultDto MapResult(
        SimulationRun run,
        EngineRunResult result,
        SimulationModelInfoDto model) => new(
        run.Id,
        run.ScenarioId,
        model.ModelKey,
        run.ModelVersion,
        run.RunType,
        run.Status,
        run.IterationCount,
        run.RandomSeed,
        run.AssumptionSetVersion,
        run.StartedAtUtc,
        run.CompletedAtUtc,
        run.ErrorMessage,
        result.Metrics
            .Select(m => new SimulationMetricDto(
                m.MetricKey,
                MetricUnitFor(m.MetricKey),
                m.Mean,
                m.StandardDeviation,
                m.Minimum,
                m.Maximum,
                m.Percentiles,
                m.SampleCount))
            .ToList(),
        result.Bands
            .Select(b => new SimulationBandPointDto(b.MonthIndex, b.Mean, b.Percentiles))
            .ToList(),
        result.ShareOfPathsWithNegativeCash,
        result.WarningCounts,
        result.IsEffectivelyDeterministic,
        result.CorrelationHonoured,
        result.ModelAssumptions,
        result.MissingInputs);

    /// <summary>
    /// Maps a metric key to its unit. Kept explicit rather than inferred from
    /// the name so a new metric cannot silently render with the wrong suffix.
    /// </summary>
    private static MetricUnit MetricUnitFor(string metricKey) => metricKey switch
    {
        "final_cash" or "minimum_cash" or "total_income" or "total_expenses"
            or "total_interest" or "net_profit" or "total_cost" => MetricUnit.Currency,
        "months_to_negative" or "breakeven_month" or "months_to_target" => MetricUnit.Months,
        "debt_free_month" => MetricUnit.Months,
        _ => MetricUnit.Index
    };
}
