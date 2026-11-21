using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A modelled future under a specific set of assumptions. A scenario belongs to
/// a decision and optionally to one alternative.
/// </summary>
public class Scenario : AuditableEntity, IConcurrencyGuarded
{
    private readonly List<ScenarioVariable> _variables = [];
    private readonly List<ScenarioDependency> _dependencies = [];
    private readonly List<SimulationRun> _runs = [];

    private Scenario()
    {
    }

    internal Scenario(
        Guid decisionId,
        string name,
        ScenarioType type,
        int timeHorizonMonths,
        Guid? alternativeId,
        Guid? parentScenarioId,
        string? description)
    {
        DecisionId = decisionId;
        SetName(name);
        Type = type;
        SetTimeHorizon(timeHorizonMonths);
        AlternativeId = alternativeId;
        ParentScenarioId = parentScenarioId;
        Description = description;
        Status = ScenarioStatus.Draft;
        AssumptionSetVersion = 1;
    }

    public Guid DecisionId { get; private set; }

    public Guid? AlternativeId { get; private set; }

    /// <summary>Scenario this one inherits from or varies. Null for root scenarios.</summary>
    public Guid? ParentScenarioId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ScenarioType Type { get; private set; }

    public int TimeHorizonMonths { get; private set; }

    public ScenarioStatus Status { get; private set; }

    /// <summary>
    /// Optional relative weight used when aggregating across scenarios. Null
    /// means "no probability claim", which is the default and the honest state
    /// when no defensible basis exists.
    /// </summary>
    public decimal? ProbabilityWeight { get; private set; }

    /// <summary>
    /// Incremented whenever assumptions change. Simulation runs record the
    /// version they used so old results stay interpretable.
    /// </summary>
    public int AssumptionSetVersion { get; private set; }

    public bool IsBaseline { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ScenarioVariable> Variables => _variables;

    public IReadOnlyCollection<ScenarioDependency> Dependencies => _dependencies;

    public IReadOnlyCollection<SimulationRun> Runs => _runs;

    public void UpdateDetails(string name, string? description, ScenarioType type, int timeHorizonMonths)
    {
        SetName(name);
        Description = description;
        Type = type;
        SetTimeHorizon(timeHorizonMonths);
        Touch();
    }

    public void SetProbabilityWeight(decimal? weight)
    {
        if (weight is < 0m or > 1m)
        {
            throw new DomainException("A probability weight must be between 0 and 1, or null when no probability claim is made.");
        }

        ProbabilityWeight = weight;
        Touch();
    }

    public void MarkBaseline(bool isBaseline)
    {
        IsBaseline = isBaseline;
        Touch();
    }

    public void MarkReady()
    {
        Status = ScenarioStatus.Ready;
        Touch();
    }

    public void MarkSimulated()
    {
        Status = ScenarioStatus.Simulated;
        Touch();
    }

    public void Archive()
    {
        Status = ScenarioStatus.Archived;
        Touch();
    }

    /// <summary>
    /// Adds or replaces the value of a variable in this scenario. Replacing an
    /// existing value bumps the assumption-set version.
    /// </summary>
    public ScenarioVariable SetVariable(
        Guid variableDefinitionId,
        decimal value,
        DistributionType distributionType,
        decimal? minimumValue,
        decimal? maximumValue,
        decimal? standardDeviation,
        ConfidenceLevel confidence,
        SourceType sourceType,
        string? sourceReference,
        DistributionBasis distributionBasis,
        bool isUserOverride)
    {
        var existing = _variables.FirstOrDefault(v => v.VariableDefinitionId == variableDefinitionId);

        if (existing is not null)
        {
            existing.Update(
                value,
                distributionType,
                minimumValue,
                maximumValue,
                standardDeviation,
                confidence,
                sourceType,
                sourceReference,
                distributionBasis,
                isUserOverride);

            BumpAssumptionVersion();
            return existing;
        }

        var variable = new ScenarioVariable(
            Id,
            variableDefinitionId,
            value,
            distributionType,
            minimumValue,
            maximumValue,
            standardDeviation,
            confidence,
            sourceType,
            sourceReference,
            distributionBasis,
            isUserOverride);

        _variables.Add(variable);
        BumpAssumptionVersion();
        return variable;
    }

    public void RemoveVariable(Guid scenarioVariableId)
    {
        var variable = _variables.FirstOrDefault(v => v.Id == scenarioVariableId)
            ?? throw new EntityNotFoundException(nameof(ScenarioVariable), scenarioVariableId);

        _variables.Remove(variable);
        BumpAssumptionVersion();
    }

    /// <summary>
    /// Declares that this scenario depends on another. Self-dependencies and
    /// cycles are rejected because variable resolution must terminate.
    /// </summary>
    public void AddDependency(Guid dependsOnScenarioId, ScenarioRelationshipType relationshipType)
    {
        if (dependsOnScenarioId == Id)
        {
            throw new CircularDependencyException(Id, dependsOnScenarioId);
        }

        if (_dependencies.Any(d => d.DependsOnScenarioId == dependsOnScenarioId))
        {
            return;
        }

        _dependencies.Add(new ScenarioDependency(Id, dependsOnScenarioId, relationshipType));
        Touch();
    }

    public void RemoveDependency(Guid dependsOnScenarioId)
    {
        var dependency = _dependencies.FirstOrDefault(d => d.DependsOnScenarioId == dependsOnScenarioId);
        if (dependency is not null)
        {
            _dependencies.Remove(dependency);
            Touch();
        }
    }

    /// <summary>
    /// Records a new simulation run against this scenario. The run starts in
    /// <see cref="SimulationRunStatus.Pending"/> and captures the current
    /// assumption-set version so the result stays interpretable after later edits.
    /// </summary>
    public SimulationRun RegisterRun(
        Guid simulationModelId,
        string modelVersion,
        SimulationRunType runType,
        int iterationCount,
        int? randomSeed,
        string inputSnapshotJson)
    {
        var run = new SimulationRun(
            Id,
            simulationModelId,
            modelVersion,
            runType,
            iterationCount,
            randomSeed,
            inputSnapshotJson,
            AssumptionSetVersion);

        _runs.Add(run);
        Touch();
        return run;
    }

    private void BumpAssumptionVersion()
    {
        AssumptionSetVersion++;
        Touch();
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A scenario must have a name.");
        }

        Name = name.Trim();
    }

    private void SetTimeHorizon(int months)
    {
        if (months is < 1 or > 600)
        {
            throw new DomainException("A scenario time horizon must be between 1 and 600 months.");
        }

        TimeHorizonMonths = months;
    }
}
