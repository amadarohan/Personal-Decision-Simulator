namespace DecisionSimulator.Domain.Enums;

public enum AIMessageRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3
}

public enum AIContextType
{
    Global = 0,
    Decision = 1,
    Scenario = 2,
    SimulationRun = 3,
    EconomicContext = 4
}

/// <summary>
/// The conceptual AI role. Roles are implemented through prompt composition and
/// tool allow-lists, not through separate autonomous agents.
/// </summary>
public enum AIRole
{
    DecisionAnalyst = 0,
    ScenarioArchitect = 1,
    RiskAnalyst = 2,
    ResearchAnalyst = 3,
    SimulationInterpreter = 4,
    StrategicAdvisor = 5
}

public enum AIProposalType
{
    DecisionAlternative = 0,
    Scenario = 1,
    VariableValues = 2,
    StressTest = 3,
    ResearchPlan = 4,
    DecisionReview = 5,
    DecisionUpdate = 6
}

public enum AIProposalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Expired = 3,
    Failed = 4
}

public enum AIExecutionStatus
{
    Succeeded = 0,
    Failed = 1,
    Skipped = 2
}

public enum AIMemoryType
{
    Preference = 0,
    Constraint = 1,
    Fact = 2,
    Lesson = 3
}

/// <summary>
/// Data sensitivity classification used by the provider router. Anything above
/// <see cref="Internal"/> requires explicit cloud permission.
/// </summary>
public enum DataSensitivity
{
    Public = 0,
    Internal = 1,
    Personal = 2,
    Financial = 3,
    HighlySensitive = 4
}

public enum AIProviderKind
{
    /// <summary>Deterministic offline provider used when no model is configured.</summary>
    None = 0,

    /// <summary>Any OpenAI-compatible chat-completions endpoint (cloud or self-hosted).</summary>
    OpenAICompatible = 1,

    /// <summary>Ollama native API.</summary>
    Ollama = 2
}
