using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A persisted application setting. Used for values the user changes at runtime
/// (AI provider preferences, privacy switches, UI preferences) so they survive
/// restarts without editing configuration files.
/// </summary>
public class AppSetting : AuditableEntity
{
    private AppSetting()
    {
    }

    public AppSetting(string key, string value, string? description = null)
    {
        Key = key;
        Value = value;
        Description = description;
    }

    /// <summary>Unique setting key, e.g. "ai.cloud.enabled".</summary>
    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public void SetValue(string value)
    {
        Value = value;
        Touch();
    }
}
