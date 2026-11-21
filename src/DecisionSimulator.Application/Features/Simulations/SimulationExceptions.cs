namespace DecisionSimulator.Application.Features.Simulations;

/// <summary>
/// Raised when a model needs an assumption the scenario does not define.
/// <para>
/// This lives in the application layer so that services can react to a missing
/// assumption without referencing the engine implementation. The infrastructure
/// adapter translates the engine's own exception into this one.
/// </para>
/// </summary>
public sealed class MissingAssumptionException(string key)
    : Exception($"The model requires the assumption '{key}', which this scenario does not define.")
{
    public string Key { get; } = key;
}

/// <summary>
/// Raised when an assumption's distribution parameters are unusable, for
/// example a normal distribution with no standard deviation.
/// </summary>
public sealed class InvalidAssumptionException(string message) : Exception(message);
