using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// Aggregate statistics for one metric produced by a simulation run.
/// Statistical fields are nullable because not every model supports them: a
/// deterministic run has no standard deviation, and a single-path projection has
/// no percentiles.
/// </summary>
public class SimulationResult : Entity
{
    private SimulationResult()
    {
    }

    internal SimulationResult(
        Guid simulationRunId,
        string metricName,
        MetricUnit metricUnit,
        decimal? meanValue,
        decimal? medianValue,
        decimal? minimumValue,
        decimal? maximumValue,
        decimal? standardDeviation,
        decimal? percentile10,
        decimal? percentile25,
        decimal? percentile75,
        decimal? percentile90,
        int sampleCount,
        string? resultMetadataJson)
    {
        SimulationRunId = simulationRunId;
        MetricName = metricName;
        MetricUnit = metricUnit;
        MeanValue = meanValue;
        MedianValue = medianValue;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
        StandardDeviation = standardDeviation;
        Percentile10 = percentile10;
        Percentile25 = percentile25;
        Percentile75 = percentile75;
        Percentile90 = percentile90;
        SampleCount = sampleCount;
        ResultMetadataJson = resultMetadataJson;
    }

    public Guid SimulationRunId { get; private set; }

    public string MetricName { get; private set; } = string.Empty;

    public MetricUnit MetricUnit { get; private set; }

    public decimal? MeanValue { get; private set; }

    public decimal? MedianValue { get; private set; }

    public decimal? MinimumValue { get; private set; }

    public decimal? MaximumValue { get; private set; }

    public decimal? StandardDeviation { get; private set; }

    public decimal? Percentile10 { get; private set; }

    public decimal? Percentile25 { get; private set; }

    public decimal? Percentile75 { get; private set; }

    public decimal? Percentile90 { get; private set; }

    public int SampleCount { get; private set; }

    /// <summary>Extra context such as the distribution basis or a limitation note.</summary>
    public string? ResultMetadataJson { get; private set; }
}
