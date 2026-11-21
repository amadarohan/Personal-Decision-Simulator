using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// The proposal workflow is the safety boundary for AI-driven change: nothing the
/// assistant suggests may reach persisted state without an explicit approval.
/// </summary>
public sealed class AIProposalServiceTests : IDisposable
{
    private readonly InMemoryDatabase _database = new();
    private readonly FixedClock _clock = new(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly AIProposalService _service;

    public AIProposalServiceTests()
    {
        _service = new AIProposalService(_database, _clock);
    }

    public void Dispose() => _database.Dispose();

    private static CreateProposalRequest Proposal(
        Guid? decisionId = null,
        AIProposalType type = AIProposalType.Scenario) =>
        new(
            type,
            "Add a pessimistic scenario",
            "A scenario where income falls by a third.",
            """{"name":"Pessimistic","type":"Pessimistic"}""",
            decisionId,
            """{"basis":"user stated a 30% income risk"}""");

    private async Task<Guid> CreateDecisionAsync()
    {
        var decision = new Decision("Take the offer", DecisionCategory.Career);
        _database.Context.Decisions.Add(decision);
        await _database.Context.SaveChangesAsync();
        return decision.Id;
    }

    [Fact]
    public async Task A_new_proposal_starts_pending()
    {
        var id = await _service.CreateAsync(Proposal());

        var proposal = await _service.GetAsync(id.Value);

        proposal.Value.Status.Should().Be(AIProposalStatus.Pending);
        proposal.Value.ApprovedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task A_proposal_without_a_payload_is_rejected()
    {
        var request = Proposal() with { PayloadJson = "  " };

        var result = await _service.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task A_proposal_for_an_unknown_decision_is_rejected()
    {
        var result = await _service.CreateAsync(Proposal(Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Approval_records_the_moment_it_happened()
    {
        var id = await _service.CreateAsync(Proposal());

        var result = await _service.ApproveAsync(id.Value);

        result.Succeeded.Should().BeTrue();
        var proposal = await _service.GetAsync(id.Value);
        proposal.Value.Status.Should().Be(AIProposalStatus.Approved);
        proposal.Value.ApprovedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_proposal_cannot_be_approved_twice()
    {
        var id = await _service.CreateAsync(Proposal());
        await _service.ApproveAsync(id.Value);

        var second = await _service.ApproveAsync(id.Value);

        second.Succeeded.Should().BeFalse();
        second.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task A_rejected_proposal_cannot_be_approved()
    {
        var id = await _service.CreateAsync(Proposal());
        await _service.RejectAsync(id.Value, new RejectProposalRequest("Not plausible."));

        var result = await _service.ApproveAsync(id.Value);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Rejection_keeps_the_reason()
    {
        var id = await _service.CreateAsync(Proposal());

        await _service.RejectAsync(id.Value, new RejectProposalRequest("Assumes a raise that was not offered."));

        var proposal = await _service.GetAsync(id.Value);
        proposal.Value.Status.Should().Be(AIProposalStatus.Rejected);
        proposal.Value.RejectionReason.Should().Contain("raise");
    }

    [Fact]
    public async Task An_unapproved_proposal_cannot_be_executed()
    {
        var id = await _service.CreateAsync(Proposal());

        var result = await _service.RecordExecutionAsync(
            id.Value,
            "scenario.create",
            "{}",
            "{}",
            AIExecutionStatus.Succeeded);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
        result.ErrorMessage.Should().Contain("Approve it first");
    }

    [Fact]
    public async Task An_approved_proposal_records_a_successful_execution()
    {
        var id = await _service.CreateAsync(Proposal());
        await _service.ApproveAsync(id.Value);

        var result = await _service.RecordExecutionAsync(
            id.Value,
            "scenario.create",
            """{"name":"Pessimistic"}""",
            """{"scenarioId":"abc"}""",
            AIExecutionStatus.Succeeded);

        result.Succeeded.Should().BeTrue();

        var logs = await _service.ListExecutionsAsync(id.Value);
        logs.Value.Should().ContainSingle();
        logs.Value[0].Status.Should().Be(AIExecutionStatus.Succeeded);
    }

    [Fact]
    public async Task A_failed_execution_marks_the_proposal_failed()
    {
        var id = await _service.CreateAsync(Proposal());
        await _service.ApproveAsync(id.Value);

        await _service.RecordExecutionAsync(
            id.Value,
            "scenario.create",
            "{}",
            null,
            AIExecutionStatus.Failed,
            "The scenario name was already taken.");

        var proposal = await _service.GetAsync(id.Value);
        proposal.Value.Status.Should().Be(AIProposalStatus.Failed);
    }

    [Fact]
    public async Task Proposals_can_be_filtered_by_status()
    {
        var pending = await _service.CreateAsync(Proposal());
        var approved = await _service.CreateAsync(Proposal());
        await _service.ApproveAsync(approved.Value);

        var result = await _service.ListAsync(status: AIProposalStatus.Pending);

        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(pending.Value);
    }

    [Fact]
    public async Task Proposals_can_be_filtered_by_decision()
    {
        var decisionId = await CreateDecisionAsync();
        await _service.CreateAsync(Proposal(decisionId));
        await _service.CreateAsync(Proposal());

        var result = await _service.ListAsync(decisionId: decisionId);

        result.Value.Should().ContainSingle();
        result.Value[0].DecisionId.Should().Be(decisionId);
    }
}

