using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Decisions;
using DecisionSimulator.Application.Features.Scenarios;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class DecisionServiceTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();
    private readonly FixedClock _clock = new(new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));
    private readonly DecisionService _service;

    public DecisionServiceTests()
    {
        _service = new DecisionService(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    private Task<Guid> CreateDecisionAsync(string title = "Change career track") =>
        _service
            .CreateAsync(new CreateDecisionRequest(title, DecisionCategory.Career))
            .ContinueWith(t => t.Result.Value);

    [Fact]
    public async Task Creates_a_decision_and_returns_its_id()
    {
        var result = await _service.CreateAsync(
            new CreateDecisionRequest(
                "Move to a new city",
                DecisionCategory.Relocation,
                Description: "Compare staying against relocating.",
                Importance: ImportanceLevel.High,
                Horizon: DecisionHorizon.LongTerm,
                RiskTolerance: RiskTolerance.Low));

        result.Succeeded.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        var detail = await _service.GetAsync(result.Value);
        detail.Succeeded.Should().BeTrue();
        detail.Value.Title.Should().Be("Move to a new city");
        detail.Value.Category.Should().Be(DecisionCategory.Relocation);
        detail.Value.Importance.Should().Be(ImportanceLevel.High);
        detail.Value.Horizon.Should().Be(DecisionHorizon.LongTerm);
        detail.Value.RiskTolerance.Should().Be(RiskTolerance.Low);
        detail.Value.Status.Should().Be(DecisionStatus.Draft);
    }

    [Fact]
    public async Task Rejects_a_blank_title_with_a_validation_failure()
    {
        var result = await _service.CreateAsync(
            new CreateDecisionRequest("   ", DecisionCategory.Other));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Returns_not_found_for_an_unknown_decision()
    {
        var result = await _service.GetAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Lists_decisions_filtered_by_status_and_category()
    {
        await _service.CreateAsync(new CreateDecisionRequest("Career A", DecisionCategory.Career));
        await _service.CreateAsync(new CreateDecisionRequest("Career B", DecisionCategory.Career));
        await _service.CreateAsync(new CreateDecisionRequest("Housing A", DecisionCategory.Housing));

        var all = await _service.ListAsync();
        all.Should().HaveCount(3);

        var career = await _service.ListAsync(category: DecisionCategory.Career);
        career.Should().HaveCount(2);

        var decided = await _service.ListAsync(status: DecisionStatus.Decided);
        decided.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_a_status_transition_that_skips_a_stage()
    {
        var id = await CreateDecisionAsync();

        // Draft cannot jump straight to Decided; the workflow must pass through
        // Exploring, Modeling, and Evaluating first.
        var result = await _service.ChangeStatusAsync(id, DecisionStatus.Decided);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.InvalidState);
    }

    [Fact]
    public async Task Allows_a_valid_status_transition()
    {
        var id = await CreateDecisionAsync();

        var result = await _service.ChangeStatusAsync(id, DecisionStatus.Exploring);

        result.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(id);
        detail.Value.Status.Should().Be(DecisionStatus.Exploring);
    }

    [Fact]
    public async Task Rejects_a_review_date_in_the_past()
    {
        var id = await CreateDecisionAsync();

        var result = await _service.ScheduleReviewAsync(id, _clock.UtcNow.AddDays(-1));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Schedules_a_review_in_the_future()
    {
        var id = await CreateDecisionAsync();
        var reviewDate = _clock.UtcNow.AddMonths(6);

        var result = await _service.ScheduleReviewAsync(id, reviewDate);

        result.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(id);
        detail.Value.ReviewScheduledForUtc.Should().Be(reviewDate);
    }

    [Fact]
    public async Task Adds_an_alternative_and_reports_it_in_the_detail()
    {
        var id = await CreateDecisionAsync();

        var added = await _service.AddAlternativeAsync(
            id,
            new CreateAlternativeRequest(
                "Stay in the current role",
                IsBaseline: true,
                EstimatedInitialCost: 0m,
                EstimatedRecurringCost: 1_500m));

        added.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(id);
        detail.Value.Alternatives.Should().HaveCount(1);
        detail.Value.Alternatives[0].Name.Should().Be("Stay in the current role");
        detail.Value.Alternatives[0].IsBaseline.Should().BeTrue();
        detail.Value.Alternatives[0].EstimatedRecurringCost.Should().Be(1_500m);
    }

    [Fact]
    public async Task Refuses_to_remove_an_alternative_that_a_scenario_still_uses()
    {
        var id = await CreateDecisionAsync();

        var alternative = await _service.AddAlternativeAsync(
            id,
            new CreateAlternativeRequest("Take the offer"));

        var scenarioService = new ScenarioService(_db);
        var scenario = await scenarioService.CreateAsync(
            id,
            new CreateScenarioRequest(
                "Offer accepted",
                ScenarioType.Expected,
                24,
                AlternativeId: alternative.Value));

        scenario.Succeeded.Should().BeTrue();

        var removed = await _service.RemoveAlternativeAsync(id, alternative.Value);

        removed.Succeeded.Should().BeFalse();
        removed.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Removes_an_unreferenced_alternative()
    {
        var id = await CreateDecisionAsync();

        var alternative = await _service.AddAlternativeAsync(
            id,
            new CreateAlternativeRequest("Unused option"));

        var removed = await _service.RemoveAlternativeAsync(id, alternative.Value);

        removed.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(id);
        detail.Value.Alternatives.Should().BeEmpty();
    }

    [Fact]
    public async Task Records_a_review_with_its_lessons()
    {
        var id = await CreateDecisionAsync();

        var added = await _service.AddReviewAsync(
            id,
            new CreateReviewRequest(
                _clock.UtcNow.AddMonths(3),
                ActualOutcome: "The new role paid less than expected.",
                ExpectedOutcome: "A 20 percent raise.",
                WhatWentWell: "The team was strong.",
                WhatWentWrong: "The bonus was not paid.",
                LessonsLearned: "Ask for the bonus terms in writing."));

        added.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(id);
        detail.Value.Reviews.Should().HaveCount(1);
        detail.Value.Reviews[0].LessonsLearned.Should().Be("Ask for the bonus terms in writing.");
    }

    [Fact]
    public async Task Updates_the_descriptive_fields_of_a_decision()
    {
        var id = await CreateDecisionAsync();

        var updated = await _service.UpdateAsync(
            id,
            new UpdateDecisionRequest(
                "Change career track, revised",
                Description: "Narrowed to two options.",
                Category: DecisionCategory.Career,
                Importance: ImportanceLevel.Critical,
                Horizon: DecisionHorizon.ShortTerm,
                RiskTolerance: RiskTolerance.High,
                Context: "Two offers on the table.",
                DesiredOutcome: "Higher net income within a year.",
                ConstraintsJson: null,
                TargetDecisionDate: _clock.UtcNow.AddDays(30)));

        updated.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(id);
        detail.Value.Title.Should().Be("Change career track, revised");
        detail.Value.Importance.Should().Be(ImportanceLevel.Critical);
        detail.Value.Context.Should().Be("Two offers on the table.");
        detail.Value.TargetDecisionDate.Should().Be(_clock.UtcNow.AddDays(30));
    }
}
