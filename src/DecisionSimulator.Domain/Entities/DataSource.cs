using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A configured provider of external data. Sources are declarative: the
/// application never assumes a source is reachable or reliable.
/// </summary>
public class DataSource : AuditableEntity, IAggregateRoot
{
    private DataSource()
    {
    }

    public DataSource(
        string name,
        DataProviderType providerType,
        ReliabilityLevel reliabilityLevel,
        string? baseUrl = null,
        string? description = null,
        string? updateFrequency = null,
        bool requiresApiKey = false)
    {
        Name = name;
        ProviderType = providerType;
        ReliabilityLevel = reliabilityLevel;
        BaseUrl = baseUrl;
        Description = description;
        UpdateFrequency = updateFrequency;
        RequiresApiKey = requiresApiKey;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public DataProviderType ProviderType { get; private set; }

    public string? BaseUrl { get; private set; }

    public string? Description { get; private set; }

    public ReliabilityLevel ReliabilityLevel { get; private set; }

    /// <summary>Human-readable cadence, e.g. "daily", "monthly", "irregular".</summary>
    public string? UpdateFrequency { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// True when the source needs a credential. The credential itself is never
    /// stored in the database; it lives in user secrets or environment variables.
    /// </summary>
    public bool RequiresApiKey { get; private set; }

    public void Update(
        string name,
        string? baseUrl,
        string? description,
        ReliabilityLevel reliabilityLevel,
        string? updateFrequency,
        bool requiresApiKey)
    {
        Name = name;
        BaseUrl = baseUrl;
        Description = description;
        ReliabilityLevel = reliabilityLevel;
        UpdateFrequency = updateFrequency;
        RequiresApiKey = requiresApiKey;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
