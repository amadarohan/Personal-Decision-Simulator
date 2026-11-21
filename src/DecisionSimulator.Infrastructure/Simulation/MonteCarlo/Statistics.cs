namespace DecisionSimulator.Infrastructure.Simulation.MonteCarlo;

/// <summary>
/// Descriptive statistics used to summarise simulation output.
/// <para>
/// Percentiles use linear interpolation between order statistics (the same
/// definition as <c>numpy.percentile</c> with <c>linear</c> interpolation), so
/// results are comparable with standard tooling and are documented rather than
/// implicit.
/// </para>
/// </summary>
public static class Statistics
{
    /// <summary>
    /// Computes a percentile from an already-sorted ascending array.
    /// </summary>
    /// <param name="sortedValues">Values sorted ascending. Must not be empty.</param>
    /// <param name="percentile">Percentile in [0,100].</param>
    public static decimal Percentile(IReadOnlyList<decimal> sortedValues, decimal percentile)
    {
        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("Cannot compute a percentile of an empty set.", nameof(sortedValues));
        }

        if (percentile is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "A percentile must be between 0 and 100.");
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var rank = (double)percentile / 100d * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var weight = (decimal)(rank - lowerIndex);
        return sortedValues[lowerIndex] + ((sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight);
    }

    public static decimal Mean(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("Cannot compute a mean of an empty set.", nameof(values));
        }

        decimal sum = 0m;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum / values.Count;
    }

    /// <summary>
    /// Sample standard deviation (n-1 denominator). Returns null for fewer than
    /// two values, because a single observation has no dispersion to report.
    /// </summary>
    public static decimal? StandardDeviation(IReadOnlyList<decimal> values)
    {
        if (values.Count < 2)
        {
            return null;
        }

        var mean = Mean(values);
        decimal sumSquares = 0m;

        for (var i = 0; i < values.Count; i++)
        {
            var delta = values[i] - mean;
            sumSquares += delta * delta;
        }

        var variance = sumSquares / (values.Count - 1);
        return (decimal)Math.Sqrt((double)variance);
    }

    /// <summary>
    /// Fraction of values at or below <paramref name="threshold"/>. This is an
    /// observed frequency in the sample, not a probability claim about reality.
    /// </summary>
    public static decimal ProportionAtOrBelow(IReadOnlyList<decimal> values, decimal threshold)
    {
        if (values.Count == 0)
        {
            return 0m;
        }

        var count = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] <= threshold)
            {
                count++;
            }
        }

        return (decimal)count / values.Count;
    }

    /// <summary>
    /// Pearson correlation coefficient. Returns null when either series has no
    /// variance, because correlation is undefined in that case.
    /// </summary>
    public static decimal? Correlation(IReadOnlyList<decimal> x, IReadOnlyList<decimal> y)
    {
        if (x.Count != y.Count || x.Count < 2)
        {
            return null;
        }

        var meanX = Mean(x);
        var meanY = Mean(y);
        decimal covariance = 0m;
        decimal varianceX = 0m;
        decimal varianceY = 0m;

        for (var i = 0; i < x.Count; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            covariance += dx * dy;
            varianceX += dx * dx;
            varianceY += dy * dy;
        }

        if (varianceX == 0m || varianceY == 0m)
        {
            return null;
        }

        return covariance / ((decimal)Math.Sqrt((double)(varianceX * varianceY)));
    }
}
