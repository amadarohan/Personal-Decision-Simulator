using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A retrospective evaluation of a decision against what actually happened.
/// This is how the application learns from the gap between expectation and reality.
/// </summary>
public class DecisionReview : Entity
{
    private DecisionReview()
    {
    }

    internal DecisionReview(
        Guid decisionId,
        DateTime reviewDateUtc,
        string actualOutcome,
        string? expectedOutcome,
        string? whatWentWell,
        string? whatWentWrong,
        string? lessonsLearned)
    {
        DecisionId = decisionId;
        ReviewDate = reviewDateUtc;
        ActualOutcome = actualOutcome;
        ExpectedOutcome = expectedOutcome;
        WhatWentWell = whatWentWell;
        WhatWentWrong = whatWentWrong;
        LessonsLearned = lessonsLearned;
    }

    public Guid DecisionId { get; private set; }

    public DateTime ReviewDate { get; private set; }

    public string ActualOutcome { get; private set; } = string.Empty;

    public string? ExpectedOutcome { get; private set; }

    /// <summary>
    /// Free-form description of the gap between expectation and reality. Kept as
    /// text because the difference is rarely a single number.
    /// </summary>
    public string? OutcomeDifference { get; private set; }

    public string? WhatWentWell { get; private set; }

    public string? WhatWentWrong { get; private set; }

    public string? LessonsLearned { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public void Update(
        string actualOutcome,
        string? expectedOutcome,
        string? outcomeDifference,
        string? whatWentWell,
        string? whatWentWrong,
        string? lessonsLearned)
    {
        ActualOutcome = actualOutcome;
        ExpectedOutcome = expectedOutcome;
        OutcomeDifference = outcomeDifference;
        WhatWentWell = whatWentWell;
        WhatWentWrong = whatWentWrong;
        LessonsLearned = lessonsLearned;
    }
}
