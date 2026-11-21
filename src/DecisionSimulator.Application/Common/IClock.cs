namespace DecisionSimulator.Application.Common;

/// <summary>
/// Abstraction over the current time. Injected rather than read from
/// <see cref="DateTime.UtcNow"/> directly so that freshness rules and review
/// scheduling can be tested without waiting for the clock.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
