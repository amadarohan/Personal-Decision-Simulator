using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A named, reusable set of assumptions that can be applied to a scenario. Kept
/// separate from <see cref="Scenario"/> so that a user can save "the 2024
/// baseline assumptions" once and reuse them.
/// </summary>
public class AssumptionSet : AuditableEntity, IAggregateRoot
{
    private AssumptionSet()
    {
    }

    public AssumptionSet(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Serialised variable values keyed by variable definition id.</summary>
    public string ValuesJson { get; private set; } = "{}";

    public void Update(string name, string? description, string valuesJson)
    {
        Name = name;
        Description = description;
        ValuesJson = valuesJson;
        Touch();
    }
}
