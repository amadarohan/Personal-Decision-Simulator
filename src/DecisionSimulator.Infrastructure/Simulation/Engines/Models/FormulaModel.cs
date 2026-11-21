using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using DecisionSimulator.Infrastructure.Simulation.Expressions;

namespace DecisionSimulator.Infrastructure.Simulation.Engines.Models;

/// <summary>
/// A model whose monthly net cash flow is defined by a user-authored formula.
/// <para>
/// The formula is parsed once by <see cref="ExpressionEvaluator"/>, which is a
/// closed arithmetic grammar with no reflection, I/O, or CLR access. This is the
/// only place user-supplied expressions are executed, and it is deliberately
/// incapable of doing anything except arithmetic on declared variables.
/// </para>
/// </summary>
public sealed class FormulaModel : ISimulationModel
{
    public const string Key = "formula.custom";

    private readonly ExpressionEvaluator _netCashFlowFormula;
    private readonly ExpressionEvaluator? _expensesFormula;

    /// <param name="netCashFlowFormula">
    /// Expression for monthly net cash flow. May reference any input key plus
    /// <c>month</c> and <c>cumulative_cash</c>.
    /// </param>
    /// <param name="expensesFormula">
    /// Optional expression for monthly expenses, used only for display.
    /// </param>
    public FormulaModel(string netCashFlowFormula, string? expensesFormula = null)
    {
        if (string.IsNullOrWhiteSpace(netCashFlowFormula))
        {
            throw new FormulaEvaluationException("A formula model requires a net cash flow expression.");
        }

        _netCashFlowFormula = ExpressionEvaluator.Parse(netCashFlowFormula);
        _expensesFormula = string.IsNullOrWhiteSpace(expensesFormula)
            ? null
            : ExpressionEvaluator.Parse(expensesFormula);

        RequiredInputs = _netCashFlowFormula.ReferencedVariables
            .Concat(_expensesFormula?.ReferencedVariables ?? [])
            .Where(v => !BuiltInVariables.Contains(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Variables the engine supplies itself and that need no user input.</summary>
    private static readonly HashSet<string> BuiltInVariables =
        new(StringComparer.OrdinalIgnoreCase) { "month", "cumulative_cash" };

    public string ModelKey => Key;

    public string ModelVersion => "1.0.0";

    public ModelType ModelType => ModelType.Formula;

    public IReadOnlyCollection<string> RequiredInputs { get; }

    public IReadOnlyCollection<string> Assumptions { get; } =
    [
        "The formula is evaluated once per month with the same input values.",
        "Only arithmetic operators and the built-in functions min, max, abs, round, floor, ceiling, sqrt, and clamp are available.",
        "The formula cannot read files, call services, or access the network.",
        "A formula is a model, not a forecast: its output is only as meaningful as its inputs."
    ];

    public ModelOutput Evaluate(SimulationInputs inputs)
    {
        var months = new List<MonthlyProjection>(inputs.TimeHorizonMonths);
        var warnings = new List<string>();

        var cumulative = 0m;
        var minimumCash = 0m;
        var monthsToDepletion = -1;

        for (var month = 1; month <= inputs.TimeHorizonMonths; month++)
        {
            var scope = new Dictionary<string, decimal>(inputs.Values, StringComparer.OrdinalIgnoreCase)
            {
                ["month"] = month,
                ["cumulative_cash"] = cumulative
            };

            var net = _netCashFlowFormula.Evaluate(scope);
            var expenses = _expensesFormula?.Evaluate(scope) ?? 0m;

            cumulative += net;

            if (cumulative < minimumCash)
            {
                minimumCash = cumulative;
            }

            if (cumulative < 0m && monthsToDepletion < 0)
            {
                monthsToDepletion = month;
            }

            months.Add(new MonthlyProjection(
                month,
                Income: net + expenses,
                Expenses: expenses,
                NetCashFlow: net,
                CumulativeCash: cumulative));
        }

        if (monthsToDepletion > 0)
        {
            warnings.Add($"The formula drives cumulative cash negative in month {monthsToDepletion}.");
        }

        var summary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [MetricKeys.FinalCash] = cumulative,
            [MetricKeys.MinimumCash] = minimumCash,
            [MetricKeys.TotalNetCashFlow] = months.Sum(m => m.NetCashFlow),
            [MetricKeys.MonthsToDepletion] = monthsToDepletion < 0 ? 0m : monthsToDepletion
        };

        return new ModelOutput(months, summary, warnings);
    }
}
