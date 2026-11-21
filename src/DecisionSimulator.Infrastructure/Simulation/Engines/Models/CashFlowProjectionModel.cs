using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Infrastructure.Simulation.Engines.Models;

/// <summary>
/// Projects monthly cash flow with independent growth rates for income and
/// expenses, plus an optional inflation adjustment applied to expenses.
/// <para>
/// The model is intentionally simple and fully documented so that its output can
/// be explained line by line. It does not model taxes, irregular income, or
/// investment returns; those belong in separate models.
/// </para>
/// </summary>
public sealed class CashFlowProjectionModel : ISimulationModel
{
    public const string Key = "cashflow.projection";

    public string ModelKey => Key;

    public string ModelVersion => "1.0.0";

    public ModelType ModelType => ModelType.CashFlowProjection;

    public IReadOnlyCollection<string> RequiredInputs { get; } =
    [
        InputKeys.MonthlyIncome,
        InputKeys.MonthlyExpenses,
        InputKeys.StartingCash
    ];

    public IReadOnlyCollection<string> Assumptions { get; } =
    [
        "Income and expenses are received and paid evenly within each month.",
        "Growth rates are applied as constant monthly compounding rates, not annual rates.",
        "Inflation is applied to expenses only; income growth is specified separately.",
        "No taxes, bonuses, or irregular income are modelled.",
        "Cash is not invested, so it earns no return and loses no nominal value."
    ];

    public ModelOutput Evaluate(SimulationInputs inputs)
    {
        var income = inputs.Get(InputKeys.MonthlyIncome);
        var expenses = inputs.Get(InputKeys.MonthlyExpenses);
        var startingCash = inputs.Get(InputKeys.StartingCash);
        var incomeGrowth = inputs.GetOrDefault(InputKeys.MonthlyIncomeGrowth, 0m);
        var expenseGrowth = inputs.GetOrDefault(InputKeys.MonthlyExpenseGrowth, 0m);
        var inflation = inputs.GetOrDefault(InputKeys.MonthlyInflation, 0m);
        var oneOffCost = inputs.GetOrDefault(InputKeys.OneOffInitialCost, 0m);

        var months = new List<MonthlyProjection>(inputs.TimeHorizonMonths);
        var warnings = new List<string>();

        var cash = startingCash - oneOffCost;
        var currentIncome = income;
        var currentExpenses = expenses;
        var minimumCash = cash;
        var monthsToDepletion = -1;

        for (var month = 1; month <= inputs.TimeHorizonMonths; month++)
        {
            // Growth is applied from month 2 onward so that month 1 reflects the
            // user's stated current values exactly.
            if (month > 1)
            {
                currentIncome *= 1m + incomeGrowth;
                currentExpenses *= (1m + expenseGrowth) * (1m + inflation);
            }

            var net = currentIncome - currentExpenses;
            cash += net;

            if (cash < minimumCash)
            {
                minimumCash = cash;
            }

            if (cash < 0m && monthsToDepletion < 0)
            {
                monthsToDepletion = month;
            }

            months.Add(new MonthlyProjection(month, currentIncome, currentExpenses, net, cash));
        }

        if (monthsToDepletion > 0)
        {
            warnings.Add(
                $"Projected cash becomes negative in month {monthsToDepletion}. " +
                "This path is not financially survivable without additional income, savings, or credit.");
        }

        var final = months.Count > 0 ? months[^1] : null;

        var summary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [MetricKeys.FinalCash] = final?.CumulativeCash ?? cash,
            [MetricKeys.MinimumCash] = minimumCash,
            [MetricKeys.TotalNetCashFlow] = months.Sum(m => m.NetCashFlow),
            [MetricKeys.MonthsToDepletion] = monthsToDepletion < 0 ? 0m : monthsToDepletion,
            [MetricKeys.FinalMonthlyIncome] = final?.Income ?? income,
            [MetricKeys.FinalMonthlyExpenses] = final?.Expenses ?? expenses
        };

        return new ModelOutput(months, summary, warnings);
    }
}

/// <summary>
/// Stable input keys shared by models and the variable mapping layer.
/// </summary>
public static class InputKeys
{
    public const string MonthlyIncome = "monthly_income";
    public const string MonthlyExpenses = "monthly_expenses";
    public const string StartingCash = "starting_cash";
    public const string MonthlyIncomeGrowth = "monthly_income_growth";
    public const string MonthlyExpenseGrowth = "monthly_expense_growth";
    public const string MonthlyInflation = "monthly_inflation";
    public const string OneOffInitialCost = "one_off_initial_cost";

    public const string LoanPrincipal = "loan_principal";
    public const string AnnualInterestRate = "annual_interest_rate";
    public const string LoanTermMonths = "loan_term_months";

    public const string BusinessMonthlyRevenue = "business_monthly_revenue";
    public const string BusinessMonthlyCost = "business_monthly_cost";
    public const string BusinessStartupCost = "business_startup_cost";
    public const string BusinessRevenueGrowth = "business_revenue_growth";

    public const string DestinationMonthlyIncome = "destination_monthly_income";
    public const string DestinationMonthlyExpenses = "destination_monthly_expenses";
    public const string RelocationOneOffCost = "relocation_one_off_cost";
}

/// <summary>
/// Stable metric keys used in persisted results and in the UI.
/// </summary>
public static class MetricKeys
{
    public const string FinalCash = "final_cash";
    public const string MinimumCash = "minimum_cash";
    public const string TotalNetCashFlow = "total_net_cash_flow";
    public const string MonthsToDepletion = "months_to_depletion";
    public const string FinalMonthlyIncome = "final_monthly_income";
    public const string FinalMonthlyExpenses = "final_monthly_expenses";

    public const string TotalInterestPaid = "total_interest_paid";
    public const string MonthlyInstalment = "monthly_instalment";

    public const string BusinessBreakEvenMonth = "business_break_even_month";
    public const string BusinessCumulativeProfit = "business_cumulative_profit";

    public const string RelocationNetBenefit = "relocation_net_benefit";
}
