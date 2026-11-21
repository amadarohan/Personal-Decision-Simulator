using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Decisions;

/// <summary>Summary row for the decision list.</summary>
public sealed record DecisionSummaryDto(
    Guid Id,
    string Title,
    DecisionCategory Category,
    DecisionStatus Status,
    ImportanceLevel Importance,
    DecisionHorizon Horizon,
    int AlternativeCount,
    int ScenarioCount,
    DateTime DecisionDate,
    DateTime? TargetDecisionDate,
    DateTime? ReviewScheduledForUtc,
    DateTime UpdatedAtUtc);

/// <summary>Full detail for the decision workspace.</summary>
public sealed record DecisionDetailDto(
    Guid Id,
    string Title,
    string? Description,
    DecisionCategory Category,
    DecisionStatus Status,
    ImportanceLevel Importance,
    DecisionHorizon Horizon,
    RiskTolerance RiskTolerance,
    DateTime DecisionDate,
    DateTime? TargetDecisionDate,
    string? Context,
    string? DesiredOutcome,
    string? ConstraintsJson,
    DateTime? ReviewScheduledForUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<DecisionAlternativeDto> Alternatives,
    IReadOnlyList<ScenarioSummaryDto> Scenarios,
    IReadOnlyList<DecisionReviewDto> Reviews);

public sealed record DecisionAlternativeDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsBaseline,
    decimal? EstimatedInitialCost,
    decimal? EstimatedRecurringCost,
    string? ExpectedBenefits,
    string? KnownRisks);

public sealed record ScenarioSummaryDto(
    Guid Id,
    string Name,
    ScenarioType Type,
    ScenarioStatus Status,
    int TimeHorizonMonths,
    Guid? AlternativeId,
    Guid? ParentScenarioId,
    bool IsBaseline,
    decimal? ProbabilityWeight,
    int AssumptionSetVersion,
    int VariableCount,
    int RunCount,
    DateTime? LastRunAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DecisionReviewDto(
    Guid Id,
    DateTime ReviewDate,
    string ActualOutcome,
    string? ExpectedOutcome,
    string? OutcomeDifference,
    string? WhatWentWell,
    string? WhatWentWrong,
    string? LessonsLearned);

/// <summary>Input for creating a decision.</summary>
public sealed record CreateDecisionRequest(
    string Title,
    DecisionCategory Category,
    string? Description = null,
    ImportanceLevel Importance = ImportanceLevel.Moderate,
    DecisionHorizon Horizon = DecisionHorizon.MediumTerm,
    RiskTolerance RiskTolerance = RiskTolerance.Moderate);

/// <summary>Input for editing a decision's descriptive fields.</summary>
public sealed record UpdateDecisionRequest(
    string Title,
    string? Description,
    DecisionCategory Category,
    ImportanceLevel Importance,
    DecisionHorizon Horizon,
    RiskTolerance RiskTolerance,
    string? Context,
    string? DesiredOutcome,
    string? ConstraintsJson,
    DateTime? TargetDecisionDate);

public sealed record CreateAlternativeRequest(
    string Name,
    string? Description = null,
    bool IsBaseline = false,
    decimal? EstimatedInitialCost = null,
    decimal? EstimatedRecurringCost = null,
    string? ExpectedBenefits = null,
    string? KnownRisks = null);

public sealed record CreateReviewRequest(
    DateTime ReviewDateUtc,
    string ActualOutcome,
    string? ExpectedOutcome = null,
    string? WhatWentWell = null,
    string? WhatWentWrong = null,
    string? LessonsLearned = null);
