using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;
using FluentAssertions;

namespace DecisionSimulator.SimulationTests.Distributions;

public class DistributionTests
{
    [Fact]
    public void Fixed_distribution_always_returns_the_same_value()
    {
        var distribution = new FixedDistribution(42m);

        distribution.Sample(0.0).Should().Be(42m);
        distribution.Sample(0.5).Should().Be(42m);
        distribution.Sample(0.999).Should().Be(42m);
    }

    [Fact]
    public void Uniform_distribution_stays_within_bounds()
    {
        var distribution = new UniformDistribution(10m, 20m);

        for (var i = 0; i < 1000; i++)
        {
            var sample = distribution.Sample(i / 1000d);
            sample.Should().BeGreaterThanOrEqualTo(10m).And.BeLessThanOrEqualTo(20m);
        }
    }

    [Fact]
    public void Uniform_distribution_mean_matches_the_midpoint()
    {
        new UniformDistribution(10m, 20m).Mean.Should().Be(15m);
    }

    [Fact]
    public void Normal_distribution_mean_and_sigma_are_reported()
    {
        var distribution = new NormalDistribution(100m, 15m);

        distribution.Mean.Should().Be(100m);
        distribution.StandardDeviation.Should().Be(15m);
    }

    [Fact]
    public void Normal_distribution_median_sample_is_close_to_the_mean()
    {
        var distribution = new NormalDistribution(100m, 15m);

        distribution.Sample(0.5).Should().BeApproximately(100m, 0.01m);
    }

    [Fact]
    public void Normal_distribution_is_symmetric_around_the_mean()
    {
        var distribution = new NormalDistribution(0m, 1m);

        var low = distribution.Sample(0.1);
        var high = distribution.Sample(0.9);

        (low + high).Should().BeApproximately(0m, 0.001m);
    }

    [Fact]
    public void Normal_distribution_quantiles_match_known_values()
    {
        // Standard normal: P10 = -1.2816, P90 = 1.2816, P97.5 = 1.9600.
        var distribution = new NormalDistribution(0m, 1m);

        distribution.Sample(0.10).Should().BeApproximately(-1.2816m, 0.002m);
        distribution.Sample(0.90).Should().BeApproximately(1.2816m, 0.002m);
        distribution.Sample(0.975).Should().BeApproximately(1.9600m, 0.002m);
    }

    [Fact]
    public void LogNormal_distribution_never_returns_a_negative_value()
    {
        var distribution = new LogNormalDistribution(100m, 0.5m);

        for (var i = 0; i < 1000; i++)
        {
            distribution.Sample(i / 1000d).Should().BeGreaterThan(0m);
        }
    }

    [Fact]
    public void LogNormal_median_sample_equals_the_median_parameter()
    {
        var distribution = new LogNormalDistribution(100m, 0.5m);

        distribution.Sample(0.5).Should().BeApproximately(100m, 0.01m);
    }

    [Fact]
    public void Triangular_distribution_respects_bounds_and_mode()
    {
        var distribution = new TriangularDistribution(0m, 10m, 20m);

        distribution.Sample(0.0).Should().Be(0m);
        distribution.Sample(1.0).Should().Be(20m);
        distribution.Sample(0.5).Should().BeApproximately(10m, 0.001m);
        distribution.Mean.Should().Be(10m);
    }

    [Fact]
    public void Discrete_distribution_returns_only_declared_outcomes()
    {
        var distribution = new DiscreteDistribution([(1m, 1m), (5m, 1m), (9m, 1m)]);

        for (var i = 0; i < 1000; i++)
        {
            distribution.Sample(i / 1000d).Should().BeOneOf(1m, 5m, 9m);
        }
    }

    [Fact]
    public void Discrete_distribution_normalises_weights()
    {
        // Weights sum to 10, so the first outcome should cover the first 80%.
        var distribution = new DiscreteDistribution([(100m, 8m), (200m, 2m)]);

        distribution.Sample(0.5).Should().Be(100m);
        distribution.Sample(0.85).Should().Be(200m);
    }

    [Fact]
    public void Discrete_distribution_rejects_empty_outcomes()
    {
        var act = () => new DiscreteDistribution([]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Discrete_distribution_rejects_non_positive_total_weight()
    {
        var act = () => new DiscreteDistribution([(1m, 0m), (2m, 0m)]);

        act.Should().Throw<ArgumentException>();
    }
}

public class SeededRandomTests
{
    [Fact]
    public void Same_seed_produces_the_same_sequence()
    {
        var first = new SeededRandom(12345);
        var second = new SeededRandom(12345);

        for (var i = 0; i < 100; i++)
        {
            first.NextDouble().Should().Be(second.NextDouble());
        }
    }

    [Fact]
    public void Different_seeds_produce_different_sequences()
    {
        var first = new SeededRandom(1);
        var second = new SeededRandom(2);

        var firstValues = Enumerable.Range(0, 20).Select(_ => first.NextDouble()).ToArray();
        var secondValues = Enumerable.Range(0, 20).Select(_ => second.NextDouble()).ToArray();

        firstValues.Should().NotEqual(secondValues);
    }

    [Fact]
    public void Values_stay_within_the_unit_interval()
    {
        var random = new SeededRandom(7);

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextDouble();
            value.Should().BeGreaterThanOrEqualTo(0d).And.BeLessThan(1d);
        }
    }

    [Fact]
    public void Distribution_is_roughly_uniform()
    {
        var random = new SeededRandom(99);
        var buckets = new int[10];

        for (var i = 0; i < 100_000; i++)
        {
            buckets[(int)(random.NextDouble() * 10)]++;
        }

        // Each bucket should hold roughly 10% of the draws.
        foreach (var count in buckets)
        {
            count.Should().BeInRange(9_000, 11_000);
        }
    }
}

public class StatisticsTests
{
    [Fact]
    public void Percentile_interpolates_between_order_statistics()
    {
        var values = new List<decimal> { 1m, 2m, 3m, 4m, 5m };

        Statistics.Percentile(values, 50m).Should().Be(3m);
        Statistics.Percentile(values, 0m).Should().Be(1m);
        Statistics.Percentile(values, 100m).Should().Be(5m);
        Statistics.Percentile(values, 25m).Should().Be(2m);
        Statistics.Percentile(values, 75m).Should().Be(4m);
    }

    [Fact]
    public void Percentile_handles_a_single_value()
    {
        Statistics.Percentile([7m], 50m).Should().Be(7m);
    }

    [Fact]
    public void Percentile_rejects_an_empty_set()
    {
        var act = () => Statistics.Percentile([], 50m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Mean_is_the_arithmetic_average()
    {
        Statistics.Mean([2m, 4m, 6m]).Should().Be(4m);
    }

    [Fact]
    public void Standard_deviation_uses_the_sample_denominator()
    {
        // Sample stddev of {2,4,4,4,5,5,7,9} is 2.13809...
        var values = new List<decimal> { 2m, 4m, 4m, 4m, 5m, 5m, 7m, 9m };

        Statistics.StandardDeviation(values)!.Value.Should().BeApproximately(2.1381m, 0.001m);
    }

    [Fact]
    public void Standard_deviation_is_undefined_for_a_single_value()
    {
        Statistics.StandardDeviation([5m]).Should().BeNull();
    }

    [Fact]
    public void Proportion_at_or_below_counts_correctly()
    {
        Statistics.ProportionAtOrBelow([1m, 2m, 3m, 4m], 2m).Should().Be(0.5m);
    }

    [Fact]
    public void Correlation_detects_a_perfect_linear_relationship()
    {
        var x = new List<decimal> { 1m, 2m, 3m, 4m };
        var y = new List<decimal> { 2m, 4m, 6m, 8m };

        Statistics.Correlation(x, y)!.Value.Should().BeApproximately(1m, 0.0001m);
    }

    [Fact]
    public void Correlation_detects_a_perfect_inverse_relationship()
    {
        var x = new List<decimal> { 1m, 2m, 3m, 4m };
        var y = new List<decimal> { 8m, 6m, 4m, 2m };

        Statistics.Correlation(x, y)!.Value.Should().BeApproximately(-1m, 0.0001m);
    }

    [Fact]
    public void Correlation_is_undefined_when_a_series_has_no_variance()
    {
        Statistics.Correlation([1m, 1m, 1m], [1m, 2m, 3m]).Should().BeNull();
    }
}
