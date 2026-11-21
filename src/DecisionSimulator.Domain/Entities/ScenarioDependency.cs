using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// Declares that one scenario inherits from, varies, or stresses another.
/// </summary>
public class ScenarioDependency : Entity
{
    private ScenarioDependency()
    {
    }

    internal ScenarioDependency(Guid scenarioId, Guid dependsOnScenarioId, ScenarioRelationshipType relationshipType)
    {
        ScenarioId = scenarioId;
        DependsOnScenarioId = dependsOnScenarioId;
        RelationshipType = relationshipType;
    }

    public Guid ScenarioId { get; private set; }

    public Guid DependsOnScenarioId { get; private set; }

    public ScenarioRelationshipType RelationshipType { get; private set; }
}
