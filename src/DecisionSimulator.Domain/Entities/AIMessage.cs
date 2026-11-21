using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// One message in an AI conversation. Provider and model are recorded so that
/// the audit trail shows exactly which system produced a statement.
/// </summary>
public class AIMessage : Entity
{
    private AIMessage()
    {
    }

    internal AIMessage(
        Guid conversationId,
        AIMessageRole role,
        string content,
        string? provider,
        string? model,
        int? tokenUsage)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Provider = provider;
        Model = model;
        TokenUsage = tokenUsage;
    }

    public Guid ConversationId { get; private set; }

    public AIMessageRole Role { get; private set; }

    public string Content { get; private set; } = string.Empty;

    /// <summary>Provider identifier that produced the message. Null for user messages.</summary>
    public string? Provider { get; private set; }

    public string? Model { get; private set; }

    public int? TokenUsage { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
}
