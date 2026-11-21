using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Infrastructure.Simulation.Distributions;

/// <summary>
/// A sampled probability distribution. Implementations are pure functions of a
/// uniform random draw in [0,1), which makes them trivially testable and keeps
/// the random-number source swappable.
/// </summary>
public interface IDistribution
{
    DistributionType Type { get; }

    /// <summary>Draws one sample. <paramref name="u"/> must be in [0,1).</summary>
    decimal Sample(double u);

    /// <summary>Theoretical mean, when it exists. Null for distributions without one.</summary>
    decimal? Mean { get; }

    /// <summary>Theoretical standard deviation, when it exists.</summary>
    decimal? StandardDeviation { get; }
}

/// <summary>
/// A distribution with no uncertainty. Sampling always returns the same value.
/// </summary>
public sealed class FixedDistribution(decimal value) : IDistribution
{
    public decimal Value { get; } = value;

    public DistributionType Type => DistributionType.Fixed;

    public decimal Sample(double u) => Value;

    public decimal? Mean => Value;

    public decimal? StandardDeviation => 0m;
}

/// <summary>Uniform distribution over [minimum, maximum].</summary>
public sealed class UniformDistribution(decimal minimum, decimal maximum) : IDistribution
{
    public decimal Minimum { get; } = minimum;

    public decimal Maximum { get; } = maximum;

    public DistributionType Type => DistributionType.Uniform;

    public decimal Sample(double u)
    {
        var clamped = Math.Clamp(u, 0d, 1d);
        return Minimum + ((Maximum - Minimum) * (decimal)clamped);
    }

    public decimal? Mean => (Minimum + Maximum) / 2m;

    public decimal? StandardDeviation => (Maximum - Minimum) / (decimal)Math.Sqrt(12d);
}

/// <summary>
/// Normal distribution parameterised by mean and standard deviation. Sampling
/// uses the inverse error function so that a single uniform draw maps to a
/// single normal draw, which keeps sampling reproducible and order-independent.
/// </summary>
public sealed class NormalDistribution(decimal mean, decimal standardDeviation) : IDistribution
{
    public decimal MeanValue { get; } = mean;

    public decimal Sigma { get; } = standardDeviation;

    public DistributionType Type => DistributionType.Normal;

    public decimal Sample(double u) => MeanValue + (Sigma * (decimal)StandardNormalQuantile(u));

    public decimal? Mean => MeanValue;

    public decimal? StandardDeviation => Sigma;

    /// <summary>
    /// Maps a uniform draw in (0,1) to a standard normal quantile. Exposed
    /// statically so that other distributions (log-normal) reuse exactly the
    /// same numerics instead of duplicating them.
    /// </summary>
    public static double StandardNormalQuantile(double u)
    {
        // Guard against exactly 0 and 1, where the inverse CDF is unbounded.
        var p = Math.Clamp(u, 1e-12, 1d - 1e-12);
        return AcklamInverseNormal(p);
    }

    /// <summary>
    /// Acklam's rational approximation of the inverse normal CDF, refined with
    /// one Halley step. Relative accuracy is better than 1e-15 after refinement,
    /// which is far beyond what financial projections require.
    /// <para>
    /// A Newton iteration on the error function was tried first and rejected:
    /// it diverges in the tails, producing values that overflow
    /// <see cref="decimal"/>.
    /// </para>
    /// </summary>
    private static double AcklamInverseNormal(double p)
    {
        double[] a =
        [
            -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
            1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00
        ];
        double[] b =
        [
            -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
            6.680131188771972e+01, -1.328068155288572e+01
        ];
        double[] c =
        [
            -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
            -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00
        ];
        double[] d =
        [
            7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
            3.754408661907416e+00
        ];

        const double pLow = 0.02425d;
        const double pHigh = 1d - pLow;
        double x;

        if (p < pLow)
        {
            var q = Math.Sqrt(-2d * Math.Log(p));
            x = ((((((c[0] * q) + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5])
                / (((((d[0] * q) + d[1]) * q + d[2]) * q + d[3]) * q + 1d);
        }
        else if (p <= pHigh)
        {
            var q = p - 0.5d;
            var r = q * q;
            x = ((((((a[0] * r) + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q
                / ((((((b[0] * r) + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1d);
        }
        else
        {
            var q = Math.Sqrt(-2d * Math.Log(1d - p));
            x = -((((((c[0] * q) + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5])
                / (((((d[0] * q) + d[1]) * q + d[2]) * q + d[3]) * q + 1d);
        }

        // One Halley refinement step against the true CDF.
        var e = 0.5d * Erfc(-x / Math.Sqrt(2d)) - p;
        var u = e * Math.Sqrt(2d * Math.PI) * Math.Exp(x * x / 2d);
        return x - (u / (1d + (x * u / 2d)));
    }

    /// <summary>Complementary error function, via the A&amp;S 7.1.26 series.</summary>
    private static double Erfc(double x) => 1d - Erf(x);

    /// <summary>Error function via the Abramowitz and Stegun 7.1.26 series.</summary>
    private static double Erf(double x)
    {
        var sign = x < 0 ? -1d : 1d;
        var a = Math.Abs(x);
        const double p = 0.3275911d;
        const double a1 = 0.254829592d;
        const double a2 = -0.284496736d;
        const double a3 = 1.421413741d;
        const double a4 = -1.453152027d;
        const double a5 = 1.061405429d;

        var t = 1d / (1d + (p * a));
        var y = 1d - ((((((a5 * t) + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-a * a);
        return sign * y;
    }
}

/// <summary>
/// Log-normal distribution parameterised by its median and sigma. Used for
/// quantities that cannot go negative and are right-skewed, such as income or
/// prices under multiplicative shocks.
/// </summary>
public sealed class LogNormalDistribution(decimal median, decimal sigma) : IDistribution
{
    public decimal Median { get; } = median;

    public decimal Sigma { get; } = sigma;

    public DistributionType Type => DistributionType.LogNormal;

    public decimal Sample(double u)
    {
        var z = NormalDistribution.StandardNormalQuantile(u);
        var mu = Math.Log((double)Median);
        return (decimal)Math.Exp(mu + ((double)Sigma * z));
    }

    public decimal? Mean => Median * (decimal)Math.Exp((double)(Sigma * Sigma) / 2d);

    public decimal? StandardDeviation
    {
        get
        {
            var s2 = (double)(Sigma * Sigma);
            var variance = (double)(Median * Median) * Math.Exp(s2) * (Math.Exp(s2) - 1d);
            return (decimal)Math.Sqrt(variance);
        }
    }
}

/// <summary>Triangular distribution with an explicit mode.</summary>
public sealed class TriangularDistribution(decimal minimum, decimal mode, decimal maximum) : IDistribution
{
    public decimal Minimum { get; } = minimum;

    public decimal Mode { get; } = mode;

    public decimal Maximum { get; } = maximum;

    public DistributionType Type => DistributionType.Triangular;

    public decimal Sample(double u)
    {
        var p = Math.Clamp(u, 0d, 1d);
        var span = Maximum - Minimum;

        if (span == 0m)
        {
            return Minimum;
        }

        var split = (double)((Mode - Minimum) / span);

        return p < split
            ? Minimum + (decimal)Math.Sqrt(p * split) * span
            : Maximum - (decimal)Math.Sqrt((1d - p) * (1d - split)) * span;
    }

    public decimal? Mean => (Minimum + Mode + Maximum) / 3m;

    public decimal? StandardDeviation
    {
        get
        {
            var a = Minimum;
            var b = Maximum;
            var c = Mode;
            var variance = ((a * a) + (b * b) + (c * c) - (a * b) - (a * c) - (b * c)) / 18m;
            return variance <= 0m ? 0m : (decimal)Math.Sqrt((double)variance);
        }
    }
}

/// <summary>
/// A discrete distribution over explicit weighted outcomes. Weights are
/// normalised, so they do not have to sum to one.
/// </summary>
public sealed class DiscreteDistribution : IDistribution
{
    private readonly decimal[] _values;
    private readonly double[] _cumulative;

    public DiscreteDistribution(IReadOnlyList<(decimal Value, decimal Weight)> outcomes)
    {
        if (outcomes.Count == 0)
        {
            throw new ArgumentException("A discrete distribution needs at least one outcome.", nameof(outcomes));
        }

        var total = outcomes.Sum(o => o.Weight);
        if (total <= 0m)
        {
            throw new ArgumentException("Discrete outcome weights must sum to a positive number.", nameof(outcomes));
        }

        _values = outcomes.Select(o => o.Value).ToArray();
        _cumulative = new double[outcomes.Count];

        var running = 0d;
        for (var i = 0; i < outcomes.Count; i++)
        {
            running += (double)(outcomes[i].Weight / total);
            _cumulative[i] = running;
        }

        // Force the last bucket to 1 so floating-point drift cannot fall through.
        _cumulative[^1] = 1d;
    }

    public DistributionType Type => DistributionType.Discrete;

    public decimal Sample(double u)
    {
        var p = Math.Clamp(u, 0d, 1d);

        for (var i = 0; i < _cumulative.Length; i++)
        {
            if (p <= _cumulative[i])
            {
                return _values[i];
            }
        }

        return _values[^1];
    }

    public decimal? Mean => null;

    public decimal? StandardDeviation => null;
}
