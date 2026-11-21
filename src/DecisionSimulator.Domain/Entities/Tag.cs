using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A free-form label that can be attached to decisions and scenarios.
/// </summary>
public class Tag : AuditableEntity
{
    private Tag()
    {
    }

    public Tag(string name, string? colorHex = null)
    {
        Name = name;
        ColorHex = colorHex;
    }

    public string Name { get; private set; } = string.Empty;

    public string? ColorHex { get; private set; }

    public void Update(string name, string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A tag must have a name.");
        }

        Name = name.Trim();
        ColorHex = colorHex;
        Touch();
    }
}

/// <summary>
/// Join entity linking a tag to any tagged entity. Kept generic so that new
/// taggable types do not require a schema change.
/// </summary>
public class EntityTag : Entity
{
    private EntityTag()
    {
    }

    public EntityTag(Guid tagId, string entityType, Guid entityId)
    {
        TagId = tagId;
        EntityType = entityType;
        EntityId = entityId;
    }

    public Guid TagId { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }
}
