using System.Text.Json;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using DecisionSimulator.Infrastructure.Simulation.Distributions;

namespace DecisionSimulator.Infrastructure.Simulation.Engines;

/// <summary>
/// Builds <see cref="IDistribution"/> instances from persisted
/// <see cref="ScenarioVariable"/> rows. All validation happens here so the
/// engine can assume well-formed distributions.
/// </summary>
public static class DistributionFactory
{
    public static IDistribution Create(ScenarioVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        return Create(
            variable.DistributionType,
            variable.Value,
            variable.MinimumValue,
            variable.MaximumValue,
            variable.StandardDeviation,
            variable.DiscreteOutcomesJson);
    }

    /// <summary>
    /// Builds a distribution from primitives. The entity overload delegates here
    /// so that validation lives in exactly one place, and callers that do not
    /// have a persisted entity (such as the application-layer adapter) still get
    /// the same checks.
    /// </summary>
    public static IDistribution Create(
        DistributionType distributionType,
        decimal value,
        decimal? minimumValue,
        decimal? maximumValue,
        decimal? standardDeviation,
        string? discreteOutcomesJson)
    {
        return distributionType switch
        {
            DistributionType.Fixed => new FixedDistribution(value),

            DistributionType.Uniform => new UniformDistribution(
                minimumValue ?? throw Missing("minimum"),
                maximumValue ?? throw Missing("maximum")),

            DistributionType.Normal => new NormalDistribution(
                value,
                standardDeviation ?? throw Missing("standard deviation")),

            DistributionType.LogNormal => new LogNormalDistribution(
                value,
                standardDeviation ?? throw Missing("sigma")),

            DistributionType.Triangular => new TriangularDistribution(
                minimumValue ?? throw Missing("minimum"),
                value,
                maximumValue ?? throw Missing("maximum")),

            DistributionType.Discrete => BuildDiscrete(discreteOutcomesJson),

            DistributionType.Custom => BuildCustom(discreteOutcomesJson),

            _ => throw new InvalidDistributionException(
                $"Distribution type '{distributionType}' is not supported by the engine.")
        };

        InvalidDistributionException Missing(string parameter) => new(
            $"The variable uses a {distributionType} distribution but no {parameter} was supplied.");
    }

    private static DiscreteDistribution BuildDiscrete(string? discreteOutcomesJson)
    {
        if (string.IsNullOrWhiteSpace(discreteOutcomesJson))
        {
            throw new InvalidDistributionException(
                "A discrete distribution requires at least one weighted outcome.");
        }

        List<DiscreteOutcomeDto>? outcomes;

        try
        {
            outcomes = JsonSerializer.Deserialize<List<DiscreteOutcomeDto>>(discreteOutcomesJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidDistributionException($"The discrete outcomes payload is not valid JSON: {ex.Message}");
        }

        if (outcomes is null || outcomes.Count == 0)
        {
            throw new InvalidDistributionException("A discrete distribution requires at least one weighted outcome.");
        }

        return new DiscreteDistribution(outcomes.Select(o => (o.Value, o.Weight)).ToList());
    }

    /// <summary>
    /// A custom distribution is treated as an empirical sample set: the engine
    /// resamples from the supplied values. This is the honest representation of
    /// "here are the observations I have" without fitting a parametric curve.
    /// </summary>
    private static DiscreteDistribution BuildCustom(string? discreteOutcomesJson)
    {
        if (string.IsNullOrWhiteSpace(discreteOutcomesJson))
        {
            throw new InvalidDistributionException(
                "A custom distribution requires a list of observed values.");
        }

        List<DiscreteOutcomeDto>? samples;

        try
        {
            samples = JsonSerializer.Deserialize<List<DiscreteOutcomeDto>>(discreteOutcomesJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidDistributionException($"The custom sample payload is not valid JSON: {ex.Message}");
        }

        if (samples is null || samples.Count == 0)
        {
            throw new InvalidDistributionException("A custom distribution requires at least one observed value.");
        }

        // Equal weights turn the sample set into an empirical distribution.
        return new DiscreteDistribution(samples.Select(s => (s.Value, 1m)).ToList());
    }

    private sealed record DiscreteOutcomeDto(decimal Value, decimal Weight);
}
