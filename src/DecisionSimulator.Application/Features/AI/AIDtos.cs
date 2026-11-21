using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.AI;

public sealed record AIMessageDto(
    Guid Id,
    AIMessageRole Role,
    string Content,
    string? Provider,
    string? Model,
    int? TokenUsage,
    DateTime CreatedAtUtc);

public sealed record AIConversationDto(
    Guid Id,
    string Title,
    AIContextType ContextType,
    Guid? DecisionId,
    int MessageCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AIConversationDetailDto(
    Guid Id,
    string Title,
    AIContextType ContextType,
    Guid? DecisionId,
    IReadOnlyList<AIMessageDto> Messages,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateConversationRequest(
    string Title,
    AIContextType ContextType,
    Guid? DecisionId);

public sealed record RenameConversationRequest(string Title);

/// <summary>
/// A user turn. <see cref="Sensitivity"/> is supplied by the caller because only
/// the caller knows what it put in the prompt; the router trusts it to decide
/// whether a remote provider may see the request.
/// </summary>
public sealed record SendMessageRequest(
    string Content,
    AIRole Role = AIRole.DecisionAnalyst,
    DataSensitivity Sensitivity = DataSensitivity.Internal,
    string? ContextSummary = null);

/// <summary>
/// The result of a user turn. <see cref="AssistantMessage"/> is null when no
/// provider could answer; <see cref="UnavailableReason"/> then explains why and
/// the UI must show that instead of a reply.
/// </summary>
public sealed record SendMessageResult(
    AIMessageDto UserMessage,
    AIMessageDto? AssistantMessage,
    string? UnavailableReason);

public sealed record AIProposalDto(
    Guid Id,
    Guid? DecisionId,
    AIProposalType ProposalType,
    string Title,
    string Description,
    string PayloadJson,
    string? EvidenceJson,
    ConfidenceLevel Confidence,
    AIProposalStatus Status,
    DateTime? ApprovedAtUtc,
    DateTime? RejectedAtUtc,
    string? RejectionReason,
    DateTime CreatedAtUtc);

public sealed record CreateProposalRequest(
    AIProposalType ProposalType,
    string Title,
    string Description,
    string PayloadJson,
    Guid? DecisionId,
    string? EvidenceJson,
    ConfidenceLevel Confidence = ConfidenceLevel.Unknown);

public sealed record RejectProposalRequest(string? Reason);

public sealed record AIExecutionLogDto(
    Guid Id,
    Guid AIProposalId,
    string Operation,
    AIExecutionStatus Status,
    string? ErrorMessage,
    DateTime CreatedAtUtc);

public sealed record AIMemoryDto(
    Guid Id,
    AIMemoryType MemoryType,
    string Content,
    string? SourceReference,
    ConfidenceLevel Confidence,
    bool IsValidated,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record CreateMemoryRequest(
    AIMemoryType MemoryType,
    string Content,
    string? SourceReference,
    ConfidenceLevel Confidence = ConfidenceLevel.Unknown);

public sealed record UpdateMemoryRequest(
    string Content,
    string? SourceReference,
    ConfidenceLevel Confidence);

/// <summary>
/// The state of the AI subsystem as the UI needs to show it.
/// </summary>
public sealed record AIStatusDto(
    bool IsAvailable,
    string ProviderName,
    AIProviderKind ProviderKind,
    bool IsRemote,
    DataSensitivity MaxRemoteSensitivity,
    bool AllowRemoteProviders,
    string? UnavailableReason);
