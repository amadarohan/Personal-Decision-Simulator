using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A potentially relevant external or internal uncertainty. Likelihood is
/// qualitative by default because most of these cannot be measured.
/// </summary>
public class RiskFactor : AuditableEntity, IAggregateRoot
{
    private RiskFactor()
    {
    }

    public RiskFactor(
        string name,
        RiskCategory category,
        GeographicScope geographicScope,
        ImpactDirection impactDirection,
        string? description = null,
        string? impactMagnitude = null,
        ConfidenceLevel likelihoodAssessment = ConfidenceLevel.Unknown,
        ConfidenceLevel confidence = ConfidenceLevel.Unknown,
        string? sourceReference = null)
    {
        Name = name;
        Category = category;
        GeographicScope = geographicScope;
        ImpactDirection = impactDirection;
        Description = description;
        ImpactMagnitude = impactMagnitude;
        LikelihoodAssessment = likelihoodAssessment;
        Confidence = confidence;
        SourceReference = sourceReference;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public RiskCategory Category { get; private set; }

    public GeographicScope GeographicScope { get; private set; }

    public ImpactDirection ImpactDirection { get; private set; }

    /// <summary>
    /// Qualitative magnitude description (for example "could remove 40% of
    /// household income"). Deliberately not a single numeric score.
    /// </summary>
    public string? ImpactMagnitude { get; private set; }

    /// <summary>
    /// Qualitative likelihood. Never presented as an objective probability
    /// unless a credible source supports it.
    /// </summary>
    public ConfidenceLevel LikelihoodAssessment { get; private set; }

    public ConfidenceLevel Confidence { get; private set; }

    public string? SourceReference { get; private set; }

    public DateTime? LastVerifiedAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(
        string name,
        string? description,
        RiskCategory category,
        GeographicScope geographicScope,
        ImpactDirection impactDirection,
        string? impactMagnitude,
        ConfidenceLevel likelihoodAssessment,
        ConfidenceLevel confidence,
        string? sourceReference)
    {
        Name = name;
        Description = description;
        Category = category;
        GeographicScope = geographicScope;
        ImpactDirection = impactDirection;
        ImpactMagnitude = impactMagnitude;
        LikelihoodAssessment = likelihoodAssessment;
        Confidence = confidence;
        SourceReference = sourceReference;
        Touch();
    }

    public void MarkVerified(DateTime verifiedAtUtc)
    {
        LastVerifiedAtUtc = verifiedAtUtc;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }
}
