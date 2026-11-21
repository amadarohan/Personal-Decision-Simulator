using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// Append-only audit trail for state changes that matter for accountability:
/// approvals, deletions, restores, and configuration changes.
/// </summary>
public class AuditLog : Entity
{
    private AuditLog()
    {
    }

    public AuditLog(
        string action,
        string entityType,
        string? entityId,
        string? detailsJson = null,
        string? actor = null)
    {
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        DetailsJson = detailsJson;
        Actor = actor ?? "local-user";
    }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? DetailsJson { get; private set; }

    public string Actor { get; private set; } = "local-user";

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
}
