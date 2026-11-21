using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Scenarios;

/// <summary>Full scenario detail, including its assumptions and provenance.</summary>
public sealed record ScenarioDetailDto(
    Guid Id,
    Guid DecisionId,
    Guid? AlternativeId,
    Guid? ParentScenarioId,
    string Name,
    string? Description,
    ScenarioType Type,
    ScenarioStatus Status,
    int TimeHorizonMonths,
    bool IsBaseline,
    decimal? ProbabilityWeight,
    int AssumptionSetVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ScenarioVariableDto> Variables,
    IReadOnlyList<ScenarioDependencyDto> Dependencies,
    IReadOnlyList<SimulationRunSummaryDto> Runs);

/// <summary>
/// One assumption. Every value carries its provenance because a number without
/// a stated source cannot be judged, and the UI must be able to show where it
/// came from.
/// </summary>
public sealed record ScenarioVariableDto(
    Guid Id,
    Guid VariableDefinitionId,
    string VariableKey,
    string VariableName,
    string Unit,
    VariableCategory Category,
    decimal Value,
    DistributionType DistributionType,
    decimal? MinimumValue,
    decimal? MaximumValue,
    decimal? StandardDeviation,
    ConfidenceLevel Confidence,
    SourceType SourceType,
    string? SourceReference,
    DistributionBasis DistributionBasis,
    bool IsUserOverride,
    string? DiscreteOutcomesJson);

public sealed record ScenarioDependencyDto(
    Guid DependsOnScenarioId,
    ScenarioRelationshipType RelationshipType);

public sealed record SimulationRunSummaryDto(
    Guid Id,
    SimulationRunType RunType,
    SimulationRunStatus Status,
    string ModelVersion,
    int IterationCount,
    int? RandomSeed,
    int AssumptionSetVersion,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage);

public sealed record CreateScenarioRequest(
    string Name,
    ScenarioType Type,
    int TimeHorizonMonths,
    Guid? AlternativeId = null,
    Guid? ParentScenarioId = null,
    string? Description = null);

public sealed record UpdateScenarioRequest(
    string Name,
    string? Description,
    ScenarioType Type,
    int TimeHorizonMonths);

/// <summary>
/// Input for setting one assumption. The distribution parameters are optional
/// because a fixed value needs none, but the domain rejects a normal
/// distribution without a standard deviation.
/// </summary>
public sealed record SetScenarioVariableRequest(
    Guid VariableDefinitionId,
    decimal Value,
    DistributionType DistributionType = DistributionType.Fixed,
    decimal? MinimumValue = null,
    decimal? MaximumValue = null,
    decimal? StandardDeviation = null,
    ConfidenceLevel Confidence = ConfidenceLevel.Unknown,
    SourceType SourceType = SourceType.UserInput,
    string? SourceReference = null,
    DistributionBasis DistributionBasis = DistributionBasis.UserDefined,
    bool IsUserOverride = false,
    string? DiscreteOutcomesJson = null);
