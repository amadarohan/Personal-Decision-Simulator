namespace DecisionSimulator.Domain.Enums;

public enum ScenarioType
{
    Baseline = 0,
    Optimistic = 1,
    Expected = 2,
    Pessimistic = 3,
    StressTest = 4,
    Custom = 5,
    MonteCarlo = 6
}

public enum ScenarioStatus
{
    Draft = 0,
    Ready = 1,
    Simulated = 2,
    Archived = 3
}

/// <summary>
/// Probability distribution used to sample a variable during Monte Carlo runs.
/// </summary>
public enum DistributionType
{
    /// <summary>No uncertainty: the value is used as-is.</summary>
    Fixed = 0,

    /// <summary>Uniform between <c>MinimumValue</c> and <c>MaximumValue</c>.</summary>
    Uniform = 1,

    /// <summary>Normal with mean = <c>Value</c> and standard deviation = <c>StandardDeviation</c>.</summary>
    Normal = 2,

    /// <summary>Log-normal parameterised by the median (<c>Value</c>) and sigma.</summary>
    LogNormal = 3,

    /// <summary>Triangular with min, mode (<c>Value</c>) and max.</summary>
    Triangular = 4,

    /// <summary>Discrete outcomes supplied as weighted pairs.</summary>
    Discrete = 5,

    /// <summary>User-supplied empirical samples.</summary>
    Custom = 6
}

/// <summary>
/// Where a variable's value came from. This is surfaced in the UI so that
/// assumptions are never mistaken for observations.
/// </summary>
public enum SourceType
{
    /// <summary>Entered directly by the user.</summary>
    UserInput = 0,

    /// <summary>Derived from an external observation with provenance.</summary>
    ExternalObservation = 1,

    /// <summary>Proposed by the AI and approved by the user.</summary>
    AIProposed = 2,

    /// <summary>Copied from a variable definition default.</summary>
    DefinitionDefault = 3,

    /// <summary>Imported from a file.</summary>
    Imported = 4,

    /// <summary>Computed by another scenario or model.</summary>
    Derived = 5
}

/// <summary>
/// Epistemic status of a distribution's parameters. Prevents hypothetical
/// distributions from being presented as empirically measured ones.
/// </summary>
public enum DistributionBasis
{
    /// <summary>Parameters estimated from observed data.</summary>
    Empirical = 0,

    /// <summary>Parameters chosen by the user.</summary>
    UserDefined = 1,

    /// <summary>Parameters based on a stated expert judgement.</summary>
    ExpertAssumption = 2,

    /// <summary>Exploratory parameters with no evidential basis. Must be labelled.</summary>
    Exploratory = 3
}

public enum ScenarioRelationshipType
{
    /// <summary>This scenario inherits variables from the parent unless overridden.</summary>
    Inherits = 0,

    /// <summary>This scenario is a variant of the parent.</summary>
    VariantOf = 1,

    /// <summary>This scenario is a stress test applied on top of the parent.</summary>
    StressOf = 2
}
