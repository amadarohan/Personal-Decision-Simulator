using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A durable fact or preference the assistant may use as context. Memories are
/// user-visible and user-editable; nothing is remembered silently.
/// </summary>
public class AIMemory : AuditableEntity, IAggregateRoot
{
    private AIMemory()
    {
    }

    public AIMemory(
        AIMemoryType memoryType,
        string content,
        string? sourceReference = null,
        ConfidenceLevel confidence = ConfidenceLevel.Unknown)
    {
        MemoryType = memoryType;
        Content = content;
        SourceReference = sourceReference;
        Confidence = confidence;
        IsActive = true;
    }

    public AIMemoryType MemoryType { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public string? SourceReference { get; private set; }

    public ConfidenceLevel Confidence { get; private set; }

    /// <summary>True once the user has confirmed the memory is accurate.</summary>
    public bool IsValidated { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string content, ConfidenceLevel confidence, string? sourceReference)
    {
        Content = content;
        Confidence = confidence;
        SourceReference = sourceReference;
        Touch();
    }

    public void Validate()
    {
        IsValidated = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
