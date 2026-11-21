using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A conversation with the AI assistant. Conversations are scoped to a decision
/// when they are about one, so context can be assembled deterministically.
/// </summary>
public class AIConversation : AuditableEntity, IAggregateRoot
{
    private readonly List<AIMessage> _messages = [];

    private AIConversation()
    {
    }

    public AIConversation(string title, AIContextType contextType, Guid? decisionId = null)
    {
        Title = title;
        ContextType = contextType;
        DecisionId = decisionId;
    }

    public Guid? DecisionId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public AIContextType ContextType { get; private set; }

    public IReadOnlyCollection<AIMessage> Messages => _messages;

    public AIMessage AddMessage(
        AIMessageRole role,
        string content,
        string? provider = null,
        string? model = null,
        int? tokenUsage = null)
    {
        var message = new AIMessage(Id, role, content, provider, model, tokenUsage);
        _messages.Add(message);
        Touch();
        return message;
    }

    public void Rename(string title)
    {
        Title = title;
        Touch();
    }
}
