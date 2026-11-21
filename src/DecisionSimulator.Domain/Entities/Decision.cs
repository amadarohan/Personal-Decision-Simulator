using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A real-world decision the user is trying to make. Aggregate root for
/// alternatives, scenarios, documents, reviews and AI conversations.
/// </summary>
public class Decision : AuditableEntity, IAggregateRoot, IConcurrencyGuarded
{
    private readonly List<DecisionAlternative> _alternatives = [];
    private readonly List<Scenario> _scenarios = [];
    private readonly List<DecisionReview> _reviews = [];
    private readonly List<DecisionDocument> _documents = [];

    private Decision()
    {
    }

    public Decision(
        string title,
        DecisionCategory category,
        string? description = null,
        ImportanceLevel importance = ImportanceLevel.Moderate,
        DecisionHorizon horizon = DecisionHorizon.MediumTerm,
        RiskTolerance riskTolerance = RiskTolerance.Moderate)
    {
        SetTitle(title);
        Category = category;
        Description = description;
        Importance = importance;
        Horizon = horizon;
        RiskTolerance = riskTolerance;
        Status = DecisionStatus.Draft;
        DecisionDate = DateTime.UtcNow;
    }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DecisionCategory Category { get; private set; }

    public DecisionStatus Status { get; private set; }

    public ImportanceLevel Importance { get; private set; }

    public DecisionHorizon Horizon { get; private set; }

    /// <summary>When the decision was first recorded.</summary>
    public DateTime DecisionDate { get; private set; }

    /// <summary>Deadline by which the user wants to have decided.</summary>
    public DateTime? TargetDecisionDate { get; private set; }

    /// <summary>Free-form situational context written by the user.</summary>
    public string? Context { get; private set; }

    /// <summary>What a good outcome looks like, in the user's own words.</summary>
    public string? DesiredOutcome { get; private set; }

    /// <summary>Structured constraints (budget caps, time limits, non-negotiables).</summary>
    public string? ConstraintsJson { get; private set; }

    public RiskTolerance RiskTolerance { get; private set; }

    /// <summary>Date on which the user wants to review the real outcome.</summary>
    public DateTime? ReviewScheduledForUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<DecisionAlternative> Alternatives => _alternatives;

    public IReadOnlyCollection<Scenario> Scenarios => _scenarios;

    public IReadOnlyCollection<DecisionReview> Reviews => _reviews;

    public IReadOnlyCollection<DecisionDocument> Documents => _documents;

    public void UpdateDetails(
        string title,
        string? description,
        DecisionCategory category,
        ImportanceLevel importance,
        DecisionHorizon horizon,
        RiskTolerance riskTolerance,
        string? context,
        string? desiredOutcome,
        string? constraintsJson,
        DateTime? targetDecisionDate)
    {
        SetTitle(title);
        Description = description;
        Category = category;
        Importance = importance;
        Horizon = horizon;
        RiskTolerance = riskTolerance;
        Context = context;
        DesiredOutcome = desiredOutcome;
        ConstraintsJson = constraintsJson;
        TargetDecisionDate = targetDecisionDate;
        Touch();
    }

    /// <summary>
    /// Moves the decision to a new status. Transitions are validated so that a
    /// cancelled decision cannot silently return to modelling.
    /// </summary>
    public void ChangeStatus(DecisionStatus newStatus)
    {
        if (Status == newStatus)
        {
            return;
        }

        if (!IsTransitionAllowed(Status, newStatus))
        {
            throw new InvalidStateTransitionException(nameof(Decision), Status.ToString(), newStatus.ToString());
        }

        Status = newStatus;
        Touch();
    }

    public void ScheduleReview(DateTime reviewDateUtc)
    {
        ReviewScheduledForUtc = reviewDateUtc;
        Touch();
    }

    public DecisionAlternative AddAlternative(
        string name,
        string? description,
        bool isBaseline,
        decimal? estimatedInitialCost,
        decimal? estimatedRecurringCost,
        string? expectedBenefits,
        string? knownRisks)
    {
        if (isBaseline && _alternatives.Any(a => a.IsBaseline))
        {
            throw new DomainException("A decision can only have one baseline alternative.");
        }

        var alternative = new DecisionAlternative(
            Id,
            name,
            description,
            isBaseline,
            estimatedInitialCost,
            estimatedRecurringCost,
            expectedBenefits,
            knownRisks);

        _alternatives.Add(alternative);
        Touch();
        return alternative;
    }

    public void RemoveAlternative(Guid alternativeId)
    {
        var alternative = _alternatives.FirstOrDefault(a => a.Id == alternativeId)
            ?? throw new EntityNotFoundException(nameof(DecisionAlternative), alternativeId);

        _alternatives.Remove(alternative);
        Touch();
    }

    public Scenario AddScenario(
        string name,
        ScenarioType type,
        int timeHorizonMonths,
        Guid? alternativeId = null,
        Guid? parentScenarioId = null,
        string? description = null)
    {
        if (alternativeId is not null && _alternatives.All(a => a.Id != alternativeId))
        {
            throw new DomainException("The scenario references an alternative that does not belong to this decision.");
        }

        if (parentScenarioId is not null && _scenarios.All(s => s.Id != parentScenarioId))
        {
            throw new DomainException("The parent scenario does not belong to this decision.");
        }

        var scenario = new Scenario(Id, name, type, timeHorizonMonths, alternativeId, parentScenarioId, description);
        _scenarios.Add(scenario);
        Touch();
        return scenario;
    }

    public void RemoveScenario(Guid scenarioId)
    {
        var scenario = _scenarios.FirstOrDefault(s => s.Id == scenarioId)
            ?? throw new EntityNotFoundException(nameof(Scenario), scenarioId);

        _scenarios.Remove(scenario);
        Touch();
    }

    public DecisionReview AddReview(
        DateTime reviewDateUtc,
        string actualOutcome,
        string? expectedOutcome,
        string? whatWentWell,
        string? whatWentWrong,
        string? lessonsLearned)
    {
        var review = new DecisionReview(
            Id,
            reviewDateUtc,
            actualOutcome,
            expectedOutcome,
            whatWentWell,
            whatWentWrong,
            lessonsLearned);

        _reviews.Add(review);
        Touch();
        return review;
    }

    public DecisionDocument AddDocument(
        string fileName,
        string relativePath,
        string contentType,
        long fileSizeBytes,
        string checksum)
    {
        var document = new DecisionDocument(Id, fileName, relativePath, contentType, fileSizeBytes, checksum);
        _documents.Add(document);
        Touch();
        return document;
    }

    private void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A decision must have a title.");
        }

        if (title.Length > 300)
        {
            throw new DomainException("A decision title cannot exceed 300 characters.");
        }

        Title = title.Trim();
    }

    private static bool IsTransitionAllowed(DecisionStatus from, DecisionStatus to) => from switch
    {
        DecisionStatus.Draft => to is DecisionStatus.Exploring or DecisionStatus.Cancelled,
        DecisionStatus.Exploring => to is DecisionStatus.Modeling or DecisionStatus.Draft or DecisionStatus.Cancelled,
        DecisionStatus.Modeling => to is DecisionStatus.Evaluating or DecisionStatus.Exploring or DecisionStatus.Cancelled,
        DecisionStatus.Evaluating => to is DecisionStatus.Decided or DecisionStatus.Modeling or DecisionStatus.Cancelled,
        DecisionStatus.Decided => to is DecisionStatus.Monitoring or DecisionStatus.Evaluating,
        DecisionStatus.Monitoring => to is DecisionStatus.Reviewed or DecisionStatus.Decided,
        DecisionStatus.Reviewed => to is DecisionStatus.Monitoring,
        DecisionStatus.Cancelled => to is DecisionStatus.Draft,
        _ => false
    };
}
