using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A change the AI wants to make to persisted state. Proposals are inert until
/// the user approves them; approval is what triggers the actual mutation.
/// </summary>
public class AIProposal : AuditableEntity, IAggregateRoot
{
    private AIProposal()
    {
    }

    public AIProposal(
        AIProposalType proposalType,
        string title,
        string description,
        string payloadJson,
        Guid? decisionId = null,
        string? evidenceJson = null,
        ConfidenceLevel confidence = ConfidenceLevel.Unknown)
    {
        ProposalType = proposalType;
        Title = title;
        Description = description;
        PayloadJson = payloadJson;
        DecisionId = decisionId;
        EvidenceJson = evidenceJson;
        Confidence = confidence;
        Status = AIProposalStatus.Pending;
    }

    public Guid? DecisionId { get; private set; }

    public AIProposalType ProposalType { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Structured payload describing the proposed change.</summary>
    public string PayloadJson { get; private set; } = "{}";

    /// <summary>Evidence and assumptions supporting the proposal.</summary>
    public string? EvidenceJson { get; private set; }

    /// <summary>
    /// Qualitative confidence. Never a probability, and never derived from the
    /// model's own self-assessment alone.
    /// </summary>
    public ConfidenceLevel Confidence { get; private set; }

    public AIProposalStatus Status { get; private set; }

    public DateTime? ApprovedAtUtc { get; private set; }

    public DateTime? RejectedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public void Approve()
    {
        if (Status != AIProposalStatus.Pending)
        {
            throw new InvalidStateTransitionException(nameof(AIProposal), Status.ToString(), nameof(AIProposalStatus.Approved));
        }

        Status = AIProposalStatus.Approved;
        ApprovedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Reject(string? reason)
    {
        if (Status != AIProposalStatus.Pending)
        {
            throw new InvalidStateTransitionException(nameof(AIProposal), Status.ToString(), nameof(AIProposalStatus.Rejected));
        }

        Status = AIProposalStatus.Rejected;
        RejectedAtUtc = DateTime.UtcNow;
        RejectionReason = reason;
        Touch();
    }

    public void MarkFailed()
    {
        Status = AIProposalStatus.Failed;
        Touch();
    }
}
