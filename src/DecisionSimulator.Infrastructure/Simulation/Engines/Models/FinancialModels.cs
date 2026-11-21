using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Infrastructure.Simulation.Engines.Models;

/// <summary>
/// Amortises a fixed-rate loan with equal monthly instalments and reports the
/// interest cost and the month the balance clears.
/// <para>
/// The instalment formula is the standard annuity formula
/// <c>P * r / (1 - (1 + r)^-n)</c>. When the interest rate is zero the formula
/// degenerates, so the simple division is used instead.
/// </para>
/// </summary>
public sealed class DebtAmortizationModel : ISimulationModel
{
    public const string Key = "debt.amortization";

    public string ModelKey => Key;

    public string ModelVersion => "1.0.0";

    public ModelType ModelType => ModelType.DebtAmortization;

    public IReadOnlyCollection<string> RequiredInputs { get; } =
    [
        InputKeys.LoanPrincipal,
        InputKeys.AnnualInterestRate,
        InputKeys.LoanTermMonths
    ];

    public IReadOnlyCollection<string> Assumptions { get; } =
    [
        "The interest rate is fixed for the entire term.",
        "Instalments are equal and paid at the end of each month.",
        "No fees, insurance, or early-repayment penalties are included.",
        "The nominal annual rate is divided by twelve to obtain the monthly rate; this is a nominal, not effective, conversion."
    ];

    public ModelOutput Evaluate(SimulationInputs inputs)
    {
        var principal = inputs.Get(InputKeys.LoanPrincipal);
        var annualRate = inputs.Get(InputKeys.AnnualInterestRate);
        var termMonths = (int)inputs.Get(InputKeys.LoanTermMonths);

        if (principal <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(inputs), "The loan principal must be positive.");
        }

        if (termMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputs), "The loan term must be at least one month.");
        }

        var monthlyRate = annualRate / 12m;
        var instalment = monthlyRate == 0m
            ? principal / termMonths
            : principal * monthlyRate / (1m - (decimal)Math.Pow((double)(1m + monthlyRate), -termMonths));

        var months = new List<MonthlyProjection>(termMonths);
        var warnings = new List<string>();
        var balance = principal;
        decimal totalInterest = 0m;
        var clearedMonth = -1;

        for (var month = 1; month <= termMonths; month++)
        {
            var interest = balance * monthlyRate;
            var principalPortion = instalment - interest;

            // The final instalment absorbs rounding so the balance lands on zero.
            if (month == termMonths || principalPortion > balance)
            {
                principalPortion = balance;
            }

            balance -= principalPortion;
            totalInterest += interest;

            if (balance <= 0m && clearedMonth < 0)
            {
                clearedMonth = month;
                balance = 0m;
            }

            months.Add(new MonthlyProjection(
                MonthIndex: month,
                Income: 0m,
                Expenses: principalPortion + interest,
                NetCashFlow: -(principalPortion + interest),
                CumulativeCash: -totalInterest,
                DebtBalance: balance));
        }

        if (clearedMonth < 0)
        {
            warnings.Add(
                "The loan balance does not reach zero within the stated term. " +
                "This usually means the instalment is smaller than the monthly interest.");
        }

        var summary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [MetricKeys.MonthlyInstalment] = instalment,
            [MetricKeys.TotalInterestPaid] = totalInterest,
            [MetricKeys.FinalCash] = -totalInterest
        };

        return new ModelOutput(months, summary, warnings);
    }
}

/// <summary>
/// Projects a small business from a starting cost, monthly revenue, and monthly
/// cost, and reports the break-even month and cumulative profit.
/// </summary>
public sealed class BusinessViabilityModel : ISimulationModel
{
    public const string Key = "business.viability";

    public string ModelKey => Key;

    public string ModelVersion => "1.0.0";

    public ModelType ModelType => ModelType.BusinessViability;

    public IReadOnlyCollection<string> RequiredInputs { get; } =
    [
        InputKeys.BusinessStartupCost,
        InputKeys.BusinessMonthlyRevenue,
        InputKeys.BusinessMonthlyCost
    ];

    public IReadOnlyCollection<string> Assumptions { get; } =
    [
        "Revenue and costs are received and paid evenly within each month.",
        "Revenue growth is a constant monthly compounding rate.",
        "Costs are treated as fixed and do not scale with revenue.",
        "No taxes, financing costs, or owner salary are modelled.",
        "Break-even means cumulative profit first reaches zero, not a single profitable month."
    ];

    public ModelOutput Evaluate(SimulationInputs inputs)
    {
        var startupCost = inputs.Get(InputKeys.BusinessStartupCost);
        var revenue = inputs.Get(InputKeys.BusinessMonthlyRevenue);
        var cost = inputs.Get(InputKeys.BusinessMonthlyCost);
        var growth = inputs.GetOrDefault(InputKeys.BusinessRevenueGrowth, 0m);

        var months = new List<MonthlyProjection>(inputs.TimeHorizonMonths);
        var warnings = new List<string>();

        var cumulative = -startupCost;
        var currentRevenue = revenue;
        var breakEvenMonth = -1;
        var minimumCumulative = cumulative;

        for (var month = 1; month <= inputs.TimeHorizonMonths; month++)
        {
            if (month > 1)
            {
                currentRevenue *= 1m + growth;
            }

            var net = currentRevenue - cost;
            cumulative += net;

            if (cumulative < minimumCumulative)
            {
                minimumCumulative = cumulative;
            }

            if (cumulative >= 0m && breakEvenMonth < 0)
            {
                breakEvenMonth = month;
            }

            months.Add(new MonthlyProjection(month, currentRevenue, cost, net, cumulative));
        }

        if (breakEvenMonth < 0)
        {
            warnings.Add(
                $"Cumulative profit does not recover the startup cost within {inputs.TimeHorizonMonths} months. " +
                "The venture is not self-funding over this horizon under these assumptions.");
        }

        var summary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [MetricKeys.BusinessBreakEvenMonth] = breakEvenMonth < 0 ? 0m : breakEvenMonth,
            [MetricKeys.BusinessCumulativeProfit] = cumulative,
            [MetricKeys.MinimumCash] = minimumCumulative,
            [MetricKeys.FinalCash] = cumulative
        };

        return new ModelOutput(months, summary, warnings);
    }
}

/// <summary>
/// Compares staying put against relocating, using the difference in monthly
/// surplus net of the one-off relocation cost.
/// </summary>
public sealed class RelocationComparisonModel : ISimulationModel
{
    public const string Key = "relocation.comparison";

    public string ModelKey => Key;

    public string ModelVersion => "1.0.0";

    public ModelType ModelType => ModelType.RelocationComparison;

    public IReadOnlyCollection<string> RequiredInputs { get; } =
    [
        InputKeys.MonthlyIncome,
        InputKeys.MonthlyExpenses,
        InputKeys.DestinationMonthlyIncome,
        InputKeys.DestinationMonthlyExpenses,
        InputKeys.RelocationOneOffCost
    ];

    public IReadOnlyCollection<string> Assumptions { get; } =
    [
        "Both locations are compared in the same currency, with no exchange-rate movement.",
        "The relocation cost is incurred once, in month one.",
        "Income and expenses are constant in each location; no growth is applied.",
        "Non-financial factors such as family proximity, language, and legal status are not valued here.",
        "The comparison reports a financial difference only; it does not recommend a choice."
    ];

    public ModelOutput Evaluate(SimulationInputs inputs)
    {
        var currentSurplus = inputs.Get(InputKeys.MonthlyIncome) - inputs.Get(InputKeys.MonthlyExpenses);
        var destinationSurplus =
            inputs.Get(InputKeys.DestinationMonthlyIncome) - inputs.Get(InputKeys.DestinationMonthlyExpenses);
        var relocationCost = inputs.Get(InputKeys.RelocationOneOffCost);

        var months = new List<MonthlyProjection>(inputs.TimeHorizonMonths);
        var warnings = new List<string>();

        var stayCumulative = 0m;
        var moveCumulative = -relocationCost;
        var breakEvenMonth = -1;

        for (var month = 1; month <= inputs.TimeHorizonMonths; month++)
        {
            stayCumulative += currentSurplus;
            moveCumulative += destinationSurplus;

            if (moveCumulative >= stayCumulative && breakEvenMonth < 0)
            {
                breakEvenMonth = month;
            }

            months.Add(new MonthlyProjection(
                month,
                Income: destinationSurplus,
                Expenses: 0m,
                NetCashFlow: destinationSurplus - currentSurplus,
                CumulativeCash: moveCumulative - stayCumulative));
        }

        if (breakEvenMonth < 0)
        {
            warnings.Add(
                $"Relocating does not recover its one-off cost within {inputs.TimeHorizonMonths} months " +
                "at the stated income and expense levels.");
        }

        var summary = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [MetricKeys.RelocationNetBenefit] = moveCumulative - stayCumulative,
            [MetricKeys.BusinessBreakEvenMonth] = breakEvenMonth < 0 ? 0m : breakEvenMonth,
            [MetricKeys.FinalCash] = moveCumulative - stayCumulative
        };

        return new ModelOutput(months, summary, warnings);
    }
}
