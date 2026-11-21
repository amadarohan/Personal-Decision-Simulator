using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A calculation model. Built-in models are implemented in code; formula models
/// are evaluated by the safe expression engine. AI never supplies executable code.
/// </summary>
public class SimulationModel : AuditableEntity, IAggregateRoot
{
    private SimulationModel()
    {
    }

    public SimulationModel(
        string name,
        ModelType modelType,
        string modelVersion,
        string? description = null,
        string? formulaDefinitionJson = null,
        string? requiredVariablesJson = null,
        string? outputDefinitionsJson = null,
        bool isBuiltIn = false)
    {
        Name = name;
        ModelType = modelType;
        ModelVersion = modelVersion;
        Description = description;
        FormulaDefinitionJson = formulaDefinitionJson;
        RequiredVariablesJson = requiredVariablesJson;
        OutputDefinitionsJson = outputDefinitionsJson;
        IsBuiltIn = isBuiltIn;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ModelType ModelType { get; private set; }

    /// <summary>
    /// Semantic version of the model. Simulation runs store this so that a
    /// historical result can always be interpreted with the model that produced it.
    /// </summary>
    public string ModelVersion { get; private set; } = "1.0.0";

    /// <summary>Declarative formula definition. Never executable code.</summary>
    public string? FormulaDefinitionJson { get; private set; }

    public string? RequiredVariablesJson { get; private set; }

    public string? OutputDefinitionsJson { get; private set; }

    public bool IsBuiltIn { get; private set; }

    public void UpdateDefinition(
        string name,
        string? description,
        string modelVersion,
        string? formulaDefinitionJson,
        string? requiredVariablesJson,
        string? outputDefinitionsJson)
    {
        Name = name;
        Description = description;
        ModelVersion = modelVersion;
        FormulaDefinitionJson = formulaDefinitionJson;
        RequiredVariablesJson = requiredVariablesJson;
        OutputDefinitionsJson = outputDefinitionsJson;
        Touch();
    }
}
