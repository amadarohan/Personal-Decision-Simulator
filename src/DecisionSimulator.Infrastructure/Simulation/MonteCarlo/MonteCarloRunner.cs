using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Simulation.Correlation;
using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.Engines;

namespace DecisionSimulator.Infrastructure.Simulation.MonteCarlo;

/// <summary>Configuration for a Monte Carlo run.</summary>
public sealed class MonteCarloOptions
{
    /// <summary>Number of sampled paths. Must be at least 1.</summary>
    public int Iterations { get; init; } = 1_000;

    /// <summary>
    /// Seed for the random generator. Stored with the run so the same seed,
    /// model version, and inputs reproduce the same output exactly.
    /// </summary>
    public int Seed { get; init; }

    /// <summary>Percentiles to report, in [0,100].</summary>
    public IReadOnlyList<decimal> Percentiles { get; init; } = [10m, 25m, 50m, 75m, 90m];

    /// <summary>Hard ceiling to keep a runaway request from freezing the app.</summary>
    public const int MaxIterations = 200_000;
}

/// <summary>Distribution summary for one metric across all sampled paths.</summary>
public sealed record MetricDistribution(
    string MetricKey,
    decimal Mean,
    decimal? StandardDeviation,
    decimal Minimum,
    decimal Maximum,
    IReadOnlyDictionary<decimal, decimal> Percentiles,
    int SampleCount);

/// <summary>Per-month percentile band, used to draw the fan chart.</summary>
public sealed record TimeSeriesBand(
    int MonthIndex,
    IReadOnlyDictionary<decimal, decimal> Percentiles,
    decimal Mean);

/// <summary>The complete output of a Monte Carlo run.</summary>
public sealed class MonteCarloResult
{
    public required int Iterations { get; init; }

    public required int Seed { get; init; }

    public required string ModelKey { get; init; }

    public required string ModelVersion { get; init; }

    public required IReadOnlyList<MetricDistribution> Metrics { get; init; }

    public required IReadOnlyList<TimeSeriesBand> CashBands { get; init; }

    /// <summary>
    /// Observed share of sampled paths in which cumulative cash went negative at
    /// least once. This is a frequency in the sample, not a probability claim
    /// about the real world, and the UI must label it that way.
    /// </summary>
    public required decimal ShareOfPathsWithNegativeCash { get; init; }

    /// <summary>Distinct warnings raised by the model, with how often they occurred.</summary>
    public required IReadOnlyDictionary<string, int> WarningCounts { get; init; }

    /// <summary>True when every sampled variable was fixed, so sampling added nothing.</summary>
    public required bool IsEffectivelyDeterministic { get; init; }

    /// <summary>
    /// Null when no correlations were declared. True when the declared
    /// correlations were applied, false when they were mutually inconsistent
    /// and sampling fell back to independence. The UI must surface a false
    /// value: the output then does not match the stated assumptions.
    /// </summary>
    public bool? CorrelationHonoured { get; init; }
}

/// <summary>
/// Runs a model many times, resampling uncertain variables on each path.
/// <para>
/// The runner is deliberately single-threaded and allocation-light: a run of a
/// few thousand paths over a few dozen months completes in milliseconds, and
/// determinism is easier to guarantee without parallelism.
/// </para>
/// </summary>
public sealed class MonteCarloRunner
{
    private readonly ISimulationModel _model;

    public MonteCarloRunner(ISimulationModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// Executes the run.
    /// </summary>
    /// <param name="baseInputs">Resolved input values used as the deterministic baseline.</param>
    /// <param name="variables">
    /// Variables to sample, keyed by input key. Variables absent from this map
    /// are held at their baseline value.
    /// </param>
    /// <param name="options">Run configuration.</param>
    /// <param name="correlations">
    /// Declared correlations between sampled variables. When supplied, draws are
    /// generated with Iman-Conover rank correlation so that the marginals are
    /// preserved exactly while the stated dependence is reproduced. When null or
    /// empty, every variable is sampled independently.
    /// </param>
    public MonteCarloResult Run(
        SimulationInputs baseInputs,
        IReadOnlyDictionary<string, IDistribution> variables,
        MonteCarloOptions options,
        IReadOnlyList<CorrelationAssumption>? correlations = null)
    {
        ArgumentNullException.ThrowIfNull(baseInputs);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "A run needs at least one iteration.");
        }

        if (options.Iterations > MonteCarloOptions.MaxIterations)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Iterations cannot exceed {MonteCarloOptions.MaxIterations:N0}.");
        }

        var percentiles = options.Percentiles.Distinct().OrderBy(p => p).ToArray();
        var random = new SeededRandom(options.Seed);

        // Metric samples are accumulated per metric key.
        var metricSamples = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
        var cashByMonth = new List<decimal>[baseInputs.TimeHorizonMonths];
        for (var i = 0; i < cashByMonth.Length; i++)
        {
            cashByMonth[i] = new List<decimal>(options.Iterations);
        }

        var warningCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var negativeCashPaths = 0;
        var sampledKeys = variables.Keys.ToArray();

        // Draws are generated up front when correlations are declared, because
        // Iman-Conover reorders across the whole sample and cannot be done one
        // iteration at a time.
        var correlatedDraws = BuildCorrelatedDraws(sampledKeys, correlations, options.Iterations, random);
        bool? correlationHonoured = correlations is { Count: > 0 } ? correlatedDraws is not null : null;

        for (var iteration = 0; iteration < options.Iterations; iteration++)
        {
            var values = new Dictionary<string, decimal>(baseInputs.Values, StringComparer.OrdinalIgnoreCase);

            for (var k = 0; k < sampledKeys.Length; k++)
            {
                var key = sampledKeys[k];
                var u = correlatedDraws is null ? random.NextDouble() : correlatedDraws[iteration, k];
                values[key] = variables[key].Sample(u);
            }

            var inputs = new SimulationInputs(values, baseInputs.TimeHorizonMonths, baseInputs.CurrencyCode);
            var output = _model.Evaluate(inputs);

            foreach (var (metricKey, value) in output.SummaryMetrics)
            {
                if (!metricSamples.TryGetValue(metricKey, out var list))
                {
                    list = new List<decimal>(options.Iterations);
                    metricSamples[metricKey] = list;
                }

                list.Add(value);
            }

            var sawNegative = false;
            for (var m = 0; m < output.Months.Count && m < cashByMonth.Length; m++)
            {
                var cash = output.Months[m].CumulativeCash;
                cashByMonth[m].Add(cash);

                if (cash < 0m)
                {
                    sawNegative = true;
                }
            }

            if (sawNegative)
            {
                negativeCashPaths++;
            }

            foreach (var warning in output.Warnings)
            {
                warningCounts[warning] = warningCounts.GetValueOrDefault(warning) + 1;
            }
        }

        var metrics = new List<MetricDistribution>(metricSamples.Count);

        foreach (var (metricKey, samples) in metricSamples)
        {
            samples.Sort();
            var percentileMap = new Dictionary<decimal, decimal>(percentiles.Length);

            foreach (var p in percentiles)
            {
                percentileMap[p] = Statistics.Percentile(samples, p);
            }

            metrics.Add(new MetricDistribution(
                metricKey,
                Statistics.Mean(samples),
                Statistics.StandardDeviation(samples),
                samples[0],
                samples[^1],
                percentileMap,
                samples.Count));
        }

        var bands = new List<TimeSeriesBand>(cashByMonth.Length);

        for (var m = 0; m < cashByMonth.Length; m++)
        {
            var samples = cashByMonth[m];
            samples.Sort();
            var percentileMap = new Dictionary<decimal, decimal>(percentiles.Length);

            foreach (var p in percentiles)
            {
                percentileMap[p] = Statistics.Percentile(samples, p);
            }

            bands.Add(new TimeSeriesBand(m + 1, percentileMap, Statistics.Mean(samples)));
        }

        var isDeterministic = variables.Values.All(v => v.Type == DistributionType.Fixed);

        return new MonteCarloResult
        {
            Iterations = options.Iterations,
            Seed = options.Seed,
            ModelKey = _model.ModelKey,
            ModelVersion = _model.ModelVersion,
            Metrics = metrics,
            CashBands = bands,
            ShareOfPathsWithNegativeCash = (decimal)negativeCashPaths / options.Iterations,
            WarningCounts = warningCounts,
            IsEffectivelyDeterministic = isDeterministic,
            CorrelationHonoured = correlationHonoured
        };
    }

    /// <summary>
    /// Produces the full matrix of correlated uniforms, or null when no
    /// correlations were declared or they could not be honoured.
    /// </summary>
    private static double[,]? BuildCorrelatedDraws(
        string[] keys,
        IReadOnlyList<CorrelationAssumption>? correlations,
        int iterations,
        SeededRandom random)
    {
        if (correlations is not { Count: > 0 } || keys.Length < 2)
        {
            return null;
        }

        var sampler = new CorrelatedSampler(keys, correlations);

        // An inconsistent correlation matrix is reported, never faked: the
        // caller falls back to independent sampling and CorrelationHonoured
        // tells the UI to warn the user.
        return sampler.IsCorrelationHonoured ? sampler.Generate(iterations, random) : null;
    }

    /// <summary>
    /// Builds the sampling map from persisted scenario variables, keeping only
    /// those whose key the model actually consumes.
    /// </summary>
    public static Dictionary<string, IDistribution> BuildSamplingMap(
        IEnumerable<(string Key, ScenarioVariable Variable)> variables)
    {
        var map = new Dictionary<string, IDistribution>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, variable) in variables)
        {
            map[key] = DistributionFactory.Create(variable);
        }

        return map;
    }
}
