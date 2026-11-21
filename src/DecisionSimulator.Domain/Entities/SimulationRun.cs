using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// One execution of a model against a scenario's assumptions. Runs are
/// immutable once completed: the input snapshot and model version are preserved
/// so the result stays reproducible and interpretable.
/// </summary>
public class SimulationRun : Entity
{
    private readonly List<SimulationResult> _results = [];
    private readonly List<SimulationTimePoint> _timePoints = [];

    private SimulationRun()
    {
    }

    internal SimulationRun(
        Guid scenarioId,
        Guid simulationModelId,
        string modelVersion,
        SimulationRunType runType,
        int iterationCount,
        int? randomSeed,
        string inputSnapshotJson,
        int assumptionSetVersion)
    {
        ScenarioId = scenarioId;
        SimulationModelId = simulationModelId;
        ModelVersion = modelVersion;
        RunType = runType;
        IterationCount = iterationCount;
        RandomSeed = randomSeed;
        InputSnapshotJson = inputSnapshotJson;
        AssumptionSetVersion = assumptionSetVersion;
        Status = SimulationRunStatus.Pending;
        StartedAtUtc = DateTime.UtcNow;
    }

    public Guid ScenarioId { get; private set; }

    public Guid SimulationModelId { get; private set; }

    /// <summary>Model version used. Preserved even if the model is later edited.</summary>
    public string ModelVersion { get; private set; } = string.Empty;

    public SimulationRunType RunType { get; private set; }

    /// <summary>Number of Monte Carlo iterations. 1 for deterministic runs.</summary>
    public int IterationCount { get; private set; }

    /// <summary>
    /// Seed used for sampling. Stored so that the same seed reproduces the same
    /// results for the same model version and inputs.
    /// </summary>
    public int? RandomSeed { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public SimulationRunStatus Status { get; private set; }

    /// <summary>Frozen copy of every resolved input used by this run.</summary>
    public string InputSnapshotJson { get; private set; } = "{}";

    public string? OutputSummaryJson { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>Scenario assumption-set version at the time of the run.</summary>
    public int AssumptionSetVersion { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public IReadOnlyCollection<SimulationResult> Results => _results;

    public IReadOnlyCollection<SimulationTimePoint> TimePoints => _timePoints;

    public void MarkRunning()
    {
        Status = SimulationRunStatus.Running;
    }

    public void Complete(string outputSummaryJson)
    {
        Status = SimulationRunStatus.Completed;
        OutputSummaryJson = outputSummaryJson;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = SimulationRunStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = SimulationRunStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }

    internal SimulationResult AddResult(
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
        var result = new SimulationResult(
            Id,
            metricName,
            metricUnit,
            meanValue,
            medianValue,
            minimumValue,
            maximumValue,
            standardDeviation,
            percentile10,
            percentile25,
            percentile75,
            percentile90,
            sampleCount,
            resultMetadataJson);

        _results.Add(result);
        return result;
    }

    internal SimulationTimePoint AddTimePoint(
        int timeIndex,
        DateTime date,
        string metricName,
        decimal value,
        decimal? lowerBound,
        decimal? upperBound)
    {
        var point = new SimulationTimePoint(Id, timeIndex, date, metricName, value, lowerBound, upperBound);
        _timePoints.Add(point);
        return point;
    }
}
