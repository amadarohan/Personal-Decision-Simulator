using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Simulations;

/// <summary>
/// One assumption as the engine sees it: a stable key plus its uncertainty
/// model. Deliberately free of entity types so the application layer can call
/// the engine without referencing the persistence model.
/// </summary>
public sealed record EngineVariable(
    string Key,
    decimal Value,
    DistributionType DistributionType = DistributionType.Fixed,
    decimal? MinimumValue = null,
    decimal? MaximumValue = null,
    decimal? StandardDeviation = null,
    string? DiscreteOutcomesJson = null);

/// <summary>
/// A declared dependence between two variables. The basis and source travel with
/// the coefficient because a correlation without a stated origin cannot be
/// judged, and the UI must be able to show where it came from.
/// </summary>
public sealed record EngineCorrelation(
    string VariableA,
    string VariableB,
    decimal Coefficient,
    DistributionBasis Basis = DistributionBasis.UserDefined,
    string? SourceReference = null);

/// <summary>Everything the engine needs to execute one run.</summary>
public sealed record EngineRunRequest(
    int TimeHorizonMonths,
    string CurrencyCode,
    IReadOnlyList<EngineVariable> Variables,
    int Iterations = 1_000,
    int Seed = 0,
    IReadOnlyList<EngineCorrelation>? Correlations = null);

/// <summary>One metric summary returned by the engine.</summary>
public sealed record EngineMetric(
    string MetricKey,
    decimal Mean,
    decimal? StandardDeviation,
    decimal Minimum,
    decimal Maximum,
    IReadOnlyDictionary<decimal, decimal> Percentiles,
    int SampleCount);

/// <summary>One month of the percentile fan.</summary>
public sealed record EngineBand(
    int MonthIndex,
    decimal Mean,
    IReadOnlyDictionary<decimal, decimal> Percentiles);

/// <summary>The engine's output, expressed only in primitives and records.</summary>
public sealed record EngineRunResult(
    string ModelKey,
    string ModelVersion,
    IReadOnlyList<EngineMetric> Metrics,
    IReadOnlyList<EngineBand> Bands,
    decimal ShareOfPathsWithNegativeCash,
    IReadOnlyDictionary<string, int> WarningCounts,
    bool IsEffectivelyDeterministic,
    bool? CorrelationHonoured,
    IReadOnlyList<string> ModelAssumptions,
    IReadOnlyList<string> MissingInputs);

/// <summary>One variable's influence on one metric.</summary>
public sealed record EngineSensitivityEntry(
    string VariableKey,
    decimal LowValue,
    decimal HighValue,
    decimal MetricAtLow,
    decimal MetricAtHigh,
    decimal Swing);

public sealed record EngineSensitivityResult(
    string MetricKey,
    decimal BaselineMetric,
    IReadOnlyList<EngineSensitivityEntry> Entries,
    bool IsMonotonic);

/// <summary>One shock applied to a baseline.</summary>
public sealed record EngineStressOutcome(
    string ShockKey,
    string ShockName,
    string Description,
    IReadOnlyDictionary<string, decimal> Metrics,
    IReadOnlyDictionary<string, decimal> MetricDeltas,
    IReadOnlyList<string> Warnings,
    bool Survives);

public sealed record EngineStressReport(
    IReadOnlyDictionary<string, decimal> BaselineMetrics,
    IReadOnlyList<EngineStressOutcome> Outcomes);

/// <summary>
/// The deterministic simulation engine.
/// <para>
/// This is the only source of numbers in the application. The AI layer may
/// describe a result but must never compute one, so every figure the user sees
/// traces back to a call through this interface.
/// </para>
/// </summary>
public interface ISimulationEngine
{
    /// <summary>Models the user can choose from, with their required inputs.</summary>
    IReadOnlyList<SimulationModelInfoDto> AvailableModels { get; }

    SimulationModelInfoDto? FindModel(string modelKey);

    /// <summary>
    /// Runs a model. Returns a result whose <c>MissingInputs</c> is non-empty
    /// when the scenario lacks a required assumption; in that case no numbers
    /// are produced, because substituting a value would silently change the answer.
    /// </summary>
    EngineRunResult Run(string modelKey, EngineRunRequest request);

    /// <summary>
    /// One-at-a-time sensitivity. Reports which assumptions move the metric most,
    /// and states that interactions between variables are not captured.
    /// </summary>
    EngineSensitivityResult AnalyzeSensitivity(
        string modelKey,
        EngineRunRequest request,
        IReadOnlyDictionary<string, (decimal Low, decimal High)> ranges,
        string metricKey);

    /// <summary>Applies the default adverse shocks and reports which ones break the plan.</summary>
    EngineStressReport RunStressTests(string modelKey, EngineRunRequest request);
}
