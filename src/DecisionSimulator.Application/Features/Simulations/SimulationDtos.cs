using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Simulations;

/// <summary>
/// A metric summary produced by a run. Percentiles are keyed by their
/// percentile value (10, 50, 90) so the UI can label them without guessing.
/// </summary>
public sealed record SimulationMetricDto(
    string MetricKey,
    MetricUnit Unit,
    decimal Mean,
    decimal? StandardDeviation,
    decimal Minimum,
    decimal Maximum,
    IReadOnlyDictionary<decimal, decimal> Percentiles,
    int SampleCount);

/// <summary>One month of the percentile fan.</summary>
public sealed record SimulationBandPointDto(
    int MonthIndex,
    decimal Mean,
    IReadOnlyDictionary<decimal, decimal> Percentiles);

/// <summary>
/// The complete, displayable result of a run.
/// <para>
/// <see cref="ShareOfPathsWithNegativeCash"/> is a frequency observed in the
/// sample, not a probability about the real world. The UI must label it as
/// such; the field name and this comment exist to make that hard to forget.
/// </para>
/// </summary>
public sealed record SimulationResultDto(
    Guid RunId,
    Guid ScenarioId,
    string ModelKey,
    string ModelVersion,
    SimulationRunType RunType,
    SimulationRunStatus Status,
    int Iterations,
    int? Seed,
    int AssumptionSetVersion,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage,
    IReadOnlyList<SimulationMetricDto> Metrics,
    IReadOnlyList<SimulationBandPointDto> Bands,
    decimal ShareOfPathsWithNegativeCash,
    IReadOnlyDictionary<string, int> WarningCounts,
    bool IsEffectivelyDeterministic,
    bool? CorrelationHonoured,
    IReadOnlyList<string> ModelAssumptions,
    IReadOnlyList<string> MissingInputs);

/// <summary>Request to run one scenario through one model.</summary>
public sealed record RunSimulationRequest(
    Guid ScenarioId,
    string ModelKey,
    int Iterations = 1_000,
    int? Seed = null,
    IReadOnlyList<CorrelationInput>? Correlations = null);

/// <summary>A declared correlation between two variable keys.</summary>
public sealed record CorrelationInput(
    string VariableA,
    string VariableB,
    decimal Coefficient,
    DistributionBasis Basis = DistributionBasis.UserDefined,
    string? SourceReference = null);

/// <summary>Describes a model the user can choose, including what it needs.</summary>
public sealed record SimulationModelInfoDto(
    string ModelKey,
    string DisplayName,
    string ModelVersion,
    ModelType ModelType,
    IReadOnlyList<string> RequiredInputs,
    IReadOnlyList<string> Assumptions);

/// <summary>One variable's influence on one output metric.</summary>
public sealed record SensitivityEntryDto(
    string VariableKey,
    string MetricKey,
    decimal LowValue,
    decimal HighValue,
    decimal LowMetricValue,
    decimal HighMetricValue,
    decimal Swing);

public sealed record SensitivityResultDto(
    Guid ScenarioId,
    string ModelKey,
    string MetricKey,
    IReadOnlyList<SensitivityEntryDto> Entries);

/// <summary>One stress scenario applied to a baseline.</summary>
public sealed record StressTestOutcomeDto(
    string ShockKey,
    string ShockName,
    string Description,
    decimal BaselineMetricValue,
    decimal StressedMetricValue,
    decimal Change,
    decimal? ChangePercentage,
    IReadOnlyList<string> Warnings);

public sealed record StressTestReportDto(
    Guid ScenarioId,
    string ModelKey,
    string MetricKey,
    decimal BaselineMetricValue,
    IReadOnlyList<StressTestOutcomeDto> Outcomes);
