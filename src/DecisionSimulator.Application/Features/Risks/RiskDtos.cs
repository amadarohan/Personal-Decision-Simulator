using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Risks;

/// <summary>
/// A risk as presented to the user. Likelihood and confidence stay qualitative:
/// the application never converts them into a numeric probability, because no
/// credible source supports such a number for most of these factors.
/// </summary>
public sealed record RiskFactorDto(
    Guid Id,
    string Name,
    string? Description,
    RiskCategory Category,
    GeographicScope GeographicScope,
    ImpactDirection ImpactDirection,
    string? ImpactMagnitude,
    ConfidenceLevel LikelihoodAssessment,
    ConfidenceLevel Confidence,
    string? SourceReference,
    DateTime? LastVerifiedAtUtc,
    bool IsActive);

public sealed record CreateRiskFactorRequest(
    string Name,
    RiskCategory Category,
    GeographicScope GeographicScope,
    ImpactDirection ImpactDirection,
    string? Description = null,
    string? ImpactMagnitude = null,
    ConfidenceLevel LikelihoodAssessment = ConfidenceLevel.Unknown,
    ConfidenceLevel Confidence = ConfidenceLevel.Unknown,
    string? SourceReference = null);

public sealed record UpdateRiskFactorRequest(
    string Name,
    string? Description,
    RiskCategory Category,
    GeographicScope GeographicScope,
    ImpactDirection ImpactDirection,
    string? ImpactMagnitude,
    ConfidenceLevel LikelihoodAssessment,
    ConfidenceLevel Confidence,
    string? SourceReference);
