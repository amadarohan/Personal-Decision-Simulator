using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// One point on a simulated time series. Used to draw timelines and to compare
/// scenarios month by month.
/// </summary>
public class SimulationTimePoint : Entity
{
    private SimulationTimePoint()
    {
    }

    internal SimulationTimePoint(
        Guid simulationRunId,
        int timeIndex,
        DateTime date,
        string metricName,
        decimal value,
        decimal? lowerBound,
        decimal? upperBound)
    {
        SimulationRunId = simulationRunId;
        TimeIndex = timeIndex;
        Date = date;
        MetricName = metricName;
        Value = value;
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public Guid SimulationRunId { get; private set; }

    /// <summary>Zero-based month index from the start of the projection.</summary>
    public int TimeIndex { get; private set; }

    public DateTime Date { get; private set; }

    public string MetricName { get; private set; } = string.Empty;

    /// <summary>Central value: the deterministic path, or the median across iterations.</summary>
    public decimal Value { get; private set; }

    /// <summary>Lower percentile bound. Null for deterministic runs.</summary>
    public decimal? LowerBound { get; private set; }

    /// <summary>Upper percentile bound. Null for deterministic runs.</summary>
    public decimal? UpperBound { get; private set; }
}
