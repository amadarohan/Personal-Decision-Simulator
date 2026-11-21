using DecisionSimulator.Infrastructure.Simulation.Engines;
using DecisionSimulator.Infrastructure.Simulation.Engines.Models;

namespace DecisionSimulator.Infrastructure.Simulation.StressTesting;

/// <summary>
/// A named adverse condition applied to one or more inputs.
/// <para>
/// Shocks are expressed as multipliers or absolute overrides so that the same
/// definition works for any currency and any baseline. Every shock carries a
/// description because a stress result without its scenario is meaningless.
/// </para>
/// </summary>
public sealed class StressShock
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Multiplicative adjustments applied to input keys.</summary>
    public IReadOnlyDictionary<string, decimal> Multipliers { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Absolute overrides applied after multipliers.</summary>
    public IReadOnlyDictionary<string, decimal> Overrides { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a one-off cost expressed as a multiple of the baseline monthly
    /// expenses. This lets a shock say "a bill equal to six months of expenses"
    /// without hardcoding an amount that only makes sense for one user.
    /// </summary>
    public decimal? OneOffCostAsMultipleOfExpenses { get; init; }

    /// <summary>
    /// Optional month range over which the shock applies. Null means the whole
    /// horizon. Used for temporary shocks such as a period of unemployment.
    /// </summary>
    public int? StartMonth { get; init; }

    public int? EndMonth { get; init; }
}

/// <summary>The outcome of applying one shock.</summary>
public sealed record StressTestOutcome(
    string ShockKey,
    string ShockName,
    string Description,
    IReadOnlyDictionary<string, decimal> Metrics,
    IReadOnlyDictionary<string, decimal> MetricDeltas,
    IReadOnlyList<string> Warnings,
    bool Survives);

/// <summary>The full stress test report.</summary>
public sealed class StressTestReport
{
    public required IReadOnlyDictionary<string, decimal> BaselineMetrics { get; init; }

    public required IReadOnlyList<StressTestOutcome> Outcomes { get; init; }

    /// <summary>Shocks that break the plan, ordered by severity.</summary>
    public required IReadOnlyList<StressTestOutcome> Failures { get; init; }
}

/// <summary>
/// Applies adverse conditions to a baseline and reports which ones break it.
/// <para>
/// The default shock set is a starting point, not a claim about what will
/// happen. Users can edit, remove, or add shocks, and the report always shows
/// the shock definition next to its result.
/// </para>
/// </summary>
public sealed class StressTestRunner
{
    private readonly ISimulationModel _model;

    public StressTestRunner(ISimulationModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// A conservative default set of shocks. These are illustrative adverse
    /// conditions, not forecasts, and the UI must present them as such.
    /// </summary>
    public static IReadOnlyList<StressShock> DefaultShocks { get; } =
    [
        new StressShock
        {
            Key = "income-drop-30",
            Name = "Income falls 30%",
            Description = "A sustained 30% reduction in monthly income, for example from reduced hours or a lower-paying role.",
            Multipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [InputKeys.MonthlyIncome] = 0.70m,
                [InputKeys.DestinationMonthlyIncome] = 0.70m,
                [InputKeys.BusinessMonthlyRevenue] = 0.70m
            }
        },
        new StressShock
        {
            Key = "expenses-rise-40",
            Name = "Expenses rise 40%",
            Description = "A sustained 40% increase in monthly expenses, for example from inflation or a change in household needs.",
            Multipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [InputKeys.MonthlyExpenses] = 1.40m,
                [InputKeys.DestinationMonthlyExpenses] = 1.40m,
                [InputKeys.BusinessMonthlyCost] = 1.40m
            }
        },
        new StressShock
        {
            Key = "currency-halves",
            Name = "Currency loses half its value",
            Description = "A 50% depreciation against the currency in which imported costs are priced, doubling the local cost of imports.",
            Multipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [InputKeys.MonthlyExpenses] = 2.00m,
                [InputKeys.BusinessMonthlyCost] = 2.00m
            }
        },
        new StressShock
        {
            Key = "unemployment-6m",
            Name = "Six months without income",
            Description = "Income stops entirely for six months while expenses continue unchanged.",
            Multipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [InputKeys.MonthlyIncome] = 0m
            },
            StartMonth = 1,
            EndMonth = 6
        },
        new StressShock
        {
            Key = "unexpected-expense",
            Name = "Large unexpected expense",
            Description = "A one-off cost equal to six months of current expenses, for example a medical or repair bill.",
            OneOffCostAsMultipleOfExpenses = 6m
        }
    ];

    public StressTestReport Run(SimulationInputs baseline, IEnumerable<StressShock> shocks)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(shocks);

        var baselineOutput = _model.Evaluate(baseline);
        var baselineMetrics = new Dictionary<string, decimal>(
            baselineOutput.SummaryMetrics, StringComparer.OrdinalIgnoreCase);

        var outcomes = new List<StressTestOutcome>();

        foreach (var shock in shocks)
        {
            var values = new Dictionary<string, decimal>(baseline.Values, StringComparer.OrdinalIgnoreCase);

            foreach (var (key, multiplier) in shock.Multipliers)
            {
                if (values.TryGetValue(key, out var current))
                {
                    values[key] = current * multiplier;
                }
            }

            foreach (var (key, absolute) in shock.Overrides)
            {
                values[key] = absolute;
            }

            if (shock.OneOffCostAsMultipleOfExpenses is { } multiple)
            {
                var monthlyExpenses = baseline.GetOrDefault(InputKeys.MonthlyExpenses, 0m);
                var existing = values.GetValueOrDefault(InputKeys.OneOffInitialCost, 0m);
                values[InputKeys.OneOffInitialCost] = existing + (monthlyExpenses * multiple);
            }

            // A temporary shock is modelled by scaling the affected inputs by the
            // fraction of the horizon it covers. This keeps the model interface
            // simple while still reflecting that a six-month gap is not permanent.
            if (shock.StartMonth is not null && shock.EndMonth is not null)
            {
                var affectedMonths = Math.Max(0, shock.EndMonth.Value - shock.StartMonth.Value + 1);
                var fraction = baseline.TimeHorizonMonths == 0
                    ? 0m
                    : Math.Min(1m, (decimal)affectedMonths / baseline.TimeHorizonMonths);

                foreach (var (key, multiplier) in shock.Multipliers)
                {
                    if (!values.TryGetValue(key, out var shocked))
                    {
                        continue;
                    }

                    var original = baseline.GetOrDefault(key, shocked);
                    values[key] = (shocked * fraction) + (original * (1m - fraction));
                }
            }

            var inputs = new SimulationInputs(values, baseline.TimeHorizonMonths, baseline.CurrencyCode);
            var output = _model.Evaluate(inputs);

            var deltas = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var (metricKey, value) in output.SummaryMetrics)
            {
                if (baselineMetrics.TryGetValue(metricKey, out var baseValue))
                {
                    deltas[metricKey] = value - baseValue;
                }
            }

            var survives = output.SummaryMetrics.TryGetValue(MetricKeys.MinimumCash, out var minCash)
                ? minCash >= 0m
                : true;

            outcomes.Add(new StressTestOutcome(
                shock.Key,
                shock.Name,
                shock.Description,
                new Dictionary<string, decimal>(output.SummaryMetrics, StringComparer.OrdinalIgnoreCase),
                deltas,
                output.Warnings,
                survives));
        }

        var failures = outcomes
            .Where(o => !o.Survives)
            .OrderBy(o => o.Metrics.TryGetValue(MetricKeys.MinimumCash, out var m) ? m : 0m)
            .ToList();

        return new StressTestReport
        {
            BaselineMetrics = baselineMetrics,
            Outcomes = outcomes,
            Failures = failures
        };
    }
}
