using DecisionSimulator.Infrastructure.Simulation.Engines;

namespace DecisionSimulator.Infrastructure.Simulation.Sensitivity;

/// <summary>One variable's influence on one metric.</summary>
public sealed record SensitivityEntry(
    string VariableKey,
    decimal LowValue,
    decimal HighValue,
    decimal MetricAtLow,
    decimal MetricAtHigh,
    decimal Swing,
    decimal BaselineMetric);

/// <summary>
/// The result of a one-at-a-time sensitivity analysis, ranked by influence.
/// </summary>
public sealed class SensitivityResult
{
    public required string MetricKey { get; init; }

    public required decimal BaselineMetric { get; init; }

    /// <summary>Entries ordered by descending absolute swing (a tornado ranking).</summary>
    public required IReadOnlyList<SensitivityEntry> Entries { get; init; }

    /// <summary>
    /// True when the metric moves in the same direction for both the low and
    /// high case, which means the relationship is monotonic over the tested range.
    /// </summary>
    public required bool IsMonotonic { get; init; }
}

/// <summary>A value at which a metric crosses a threshold.</summary>
public sealed record ThresholdCrossing(
    string VariableKey,
    decimal ThresholdValue,
    decimal? CrossingValue,
    bool IsReachable,
    string Explanation);

/// <summary>
/// One-at-a-time sensitivity analysis.
/// <para>
/// Each variable is moved to its low and high bound while every other variable
/// stays at its baseline. This answers "which assumption matters most?" without
/// claiming to capture interactions between variables, which one-at-a-time
/// analysis cannot see. That limitation is reported, not hidden.
/// </para>
/// </summary>
public sealed class SensitivityAnalyzer
{
    private readonly ISimulationModel _model;

    public SensitivityAnalyzer(ISimulationModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// Ranks variables by how much they move <paramref name="metricKey"/>.
    /// </summary>
    /// <param name="baseline">Baseline inputs.</param>
    /// <param name="ranges">
    /// Low and high values per variable key. Variables not present are not tested.
    /// </param>
    /// <param name="metricKey">The summary metric to analyse.</param>
    public SensitivityResult Analyze(
        SimulationInputs baseline,
        IReadOnlyDictionary<string, (decimal Low, decimal High)> ranges,
        string metricKey)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(ranges);

        var baselineOutput = _model.Evaluate(baseline);

        if (!baselineOutput.SummaryMetrics.TryGetValue(metricKey, out var baselineMetric))
        {
            throw new ArgumentException(
                $"The model does not report the metric '{metricKey}'.", nameof(metricKey));
        }

        var entries = new List<SensitivityEntry>(ranges.Count);
        var allSameDirection = true;

        foreach (var (key, range) in ranges)
        {
            var lowMetric = EvaluateWith(baseline, key, range.Low, metricKey);
            var highMetric = EvaluateWith(baseline, key, range.High, metricKey);

            var lowDelta = lowMetric - baselineMetric;
            var highDelta = highMetric - baselineMetric;

            // A monotonic variable pushes the metric the same way at both ends.
            if (lowDelta != 0m && highDelta != 0m && Math.Sign(lowDelta) != Math.Sign(highDelta))
            {
                allSameDirection = false;
            }

            entries.Add(new SensitivityEntry(
                key,
                range.Low,
                range.High,
                lowMetric,
                highMetric,
                Math.Abs(highMetric - lowMetric),
                baselineMetric));
        }

        entries.Sort((a, b) => b.Swing.CompareTo(a.Swing));

        return new SensitivityResult
        {
            MetricKey = metricKey,
            BaselineMetric = baselineMetric,
            Entries = entries,
            IsMonotonic = allSameDirection
        };
    }

    /// <summary>
    /// Finds the value of <paramref name="variableKey"/> at which
    /// <paramref name="metricKey"/> crosses <paramref name="threshold"/>.
    /// <para>
    /// Uses bisection over the supplied search range. If the metric does not
    /// change sign across the range, the crossing is reported as unreachable
    /// rather than guessed.
    /// </para>
    /// </summary>
    public ThresholdCrossing FindThreshold(
        SimulationInputs baseline,
        string variableKey,
        string metricKey,
        decimal threshold,
        decimal searchLow,
        decimal searchHigh,
        int maxIterations = 60)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        if (searchLow >= searchHigh)
        {
            throw new ArgumentException("The search range must have a low value below the high value.");
        }

        decimal Excess(decimal value)
            => EvaluateWith(baseline, variableKey, value, metricKey) - threshold;

        var lowExcess = Excess(searchLow);
        var highExcess = Excess(searchHigh);

        if (lowExcess == 0m)
        {
            return new ThresholdCrossing(variableKey, threshold, searchLow, true,
                $"At {variableKey} = {searchLow}, the metric already equals the threshold.");
        }

        if (highExcess == 0m)
        {
            return new ThresholdCrossing(variableKey, threshold, searchHigh, true,
                $"At {variableKey} = {searchHigh}, the metric already equals the threshold.");
        }

        if (Math.Sign(lowExcess) == Math.Sign(highExcess))
        {
            return new ThresholdCrossing(variableKey, threshold, null, false,
                $"Within the searched range ({searchLow} to {searchHigh}), the metric never crosses the threshold. " +
                "Widen the range to search further.");
        }

        var low = searchLow;
        var high = searchHigh;

        for (var i = 0; i < maxIterations; i++)
        {
            var mid = low + ((high - low) / 2m);

            if (mid == low || mid == high)
            {
                break;
            }

            var midExcess = Excess(mid);

            if (midExcess == 0m)
            {
                low = mid;
                high = mid;
                break;
            }

            if (Math.Sign(midExcess) == Math.Sign(lowExcess))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        var crossing = low + ((high - low) / 2m);

        return new ThresholdCrossing(variableKey, threshold, crossing, true,
            $"The metric crosses the threshold when {variableKey} is approximately {crossing}.");
    }

    private decimal EvaluateWith(SimulationInputs baseline, string key, decimal value, string metricKey)
    {
        var values = new Dictionary<string, decimal>(baseline.Values, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        };

        var inputs = new SimulationInputs(values, baseline.TimeHorizonMonths, baseline.CurrencyCode);
        var output = _model.Evaluate(inputs);

        return output.SummaryMetrics.TryGetValue(metricKey, out var metric)
            ? metric
            : throw new ArgumentException($"The model does not report the metric '{metricKey}'.", nameof(metricKey));
    }
}
