namespace DecisionSimulator.Domain.Exceptions;

/// <summary>Base type for domain rule violations that map to a 4xx-style outcome.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Raised when an entity cannot be found by its identifier.</summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, Guid id)
        : base($"{entityName} with id '{id}' was not found.")
    {
        EntityName = entityName;
        EntityId = id;
    }

    public string EntityName { get; }

    public Guid EntityId { get; }
}

/// <summary>Raised when a requested state transition is not permitted.</summary>
public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string entityName, string from, string to)
        : base($"{entityName} cannot transition from '{from}' to '{to}'.")
    {
    }
}

/// <summary>
/// Raised when a scenario dependency would create a cycle. Cycles would make
/// variable resolution non-terminating, so they are rejected at the domain level.
/// </summary>
public sealed class CircularDependencyException : DomainException
{
    public CircularDependencyException(Guid scenarioId, Guid dependsOnScenarioId)
        : base($"Scenario '{scenarioId}' cannot depend on '{dependsOnScenarioId}' because it would create a cycle.")
    {
    }
}

/// <summary>Raised when a distribution's parameters are mathematically invalid.</summary>
public sealed class InvalidDistributionException : DomainException
{
    public InvalidDistributionException(string message) : base(message)
    {
    }
}

/// <summary>
/// Raised when a formula cannot be parsed or evaluated by the safe expression
/// engine. Never used to surface arbitrary runtime exceptions to the user.
/// </summary>
public sealed class FormulaEvaluationException : DomainException
{
    public FormulaEvaluationException(string message) : base(message)
    {
    }
}
