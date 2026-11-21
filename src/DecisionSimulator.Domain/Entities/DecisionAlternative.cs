using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// One possible choice within a decision. Alternatives are what the user is
/// actually choosing between; scenarios describe futures for an alternative.
/// </summary>
public class DecisionAlternative : AuditableEntity
{
    private DecisionAlternative()
    {
    }

    internal DecisionAlternative(
        Guid decisionId,
        string name,
        string? description,
        bool isBaseline,
        decimal? estimatedInitialCost,
        decimal? estimatedRecurringCost,
        string? expectedBenefits,
        string? knownRisks)
    {
        DecisionId = decisionId;
        SetName(name);
        Description = description;
        IsBaseline = isBaseline;
        EstimatedInitialCost = estimatedInitialCost;
        EstimatedRecurringCost = estimatedRecurringCost;
        ExpectedBenefits = expectedBenefits;
        KnownRisks = knownRisks;
    }

    public Guid DecisionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// The "do nothing / stay as is" option. Exactly one alternative per
    /// decision may be the baseline; it is the reference for comparisons.
    /// </summary>
    public bool IsBaseline { get; private set; }

    /// <summary>One-off cost to take this option, in the profile currency.</summary>
    public decimal? EstimatedInitialCost { get; private set; }

    /// <summary>Recurring monthly cost attributable to this option.</summary>
    public decimal? EstimatedRecurringCost { get; private set; }

    public string? ExpectedBenefits { get; private set; }

    public string? KnownRisks { get; private set; }

    /// <summary>Structured resource requirements (time, capital, skills).</summary>
    public string? RequiredResourcesJson { get; private set; }

    public string? ConstraintsJson { get; private set; }

    public void Update(
        string name,
        string? description,
        decimal? estimatedInitialCost,
        decimal? estimatedRecurringCost,
        string? expectedBenefits,
        string? knownRisks,
        string? requiredResourcesJson,
        string? constraintsJson)
    {
        SetName(name);
        Description = description;
        EstimatedInitialCost = estimatedInitialCost;
        EstimatedRecurringCost = estimatedRecurringCost;
        ExpectedBenefits = expectedBenefits;
        KnownRisks = knownRisks;
        RequiredResourcesJson = requiredResourcesJson;
        ConstraintsJson = constraintsJson;
        Touch();
    }

    internal void SetBaseline(bool isBaseline)
    {
        IsBaseline = isBaseline;
        Touch();
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("An alternative must have a name.");
        }

        Name = name.Trim();
    }
}
