using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.Engines;
using DecisionSimulator.Infrastructure.Simulation.Engines.Models;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;
using FluentAssertions;

namespace DecisionSimulator.SimulationTests.Engines;

public class CashFlowProjectionModelTests
{
    private static SimulationInputs Inputs(
        decimal income = 1000m,
        decimal expenses = 600m,
        decimal startingCash = 5000m,
        int months = 12,
        decimal incomeGrowth = 0m,
        decimal expenseGrowth = 0m,
        decimal inflation = 0m,
        decimal oneOff = 0m)
    {
        return new SimulationInputs(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = income,
                [InputKeys.MonthlyExpenses] = expenses,
                [InputKeys.StartingCash] = startingCash,
                [InputKeys.MonthlyIncomeGrowth] = incomeGrowth,
                [InputKeys.MonthlyExpenseGrowth] = expenseGrowth,
                [InputKeys.MonthlyInflation] = inflation,
                [InputKeys.OneOffInitialCost] = oneOff
            },
            months,
            "IRR");
    }

    [Fact]
    public void Projects_constant_cash_flow_without_growth()
    {
        var model = new CashFlowProjectionModel();

        var output = model.Evaluate(Inputs());

        output.Months.Should().HaveCount(12);
        output.Months[0].NetCashFlow.Should().Be(400m);
        output.Months[11].CumulativeCash.Should().Be(5000m + (400m * 12));
        output.SummaryMetrics[MetricKeys.FinalCash].Should().Be(9800m);
    }

    [Fact]
    public void Applies_income_growth_from_the_second_month()
    {
        var model = new CashFlowProjectionModel();

        var output = model.Evaluate(Inputs(incomeGrowth: 0.10m));

        output.Months[0].Income.Should().Be(1000m);
        output.Months[1].Income.Should().Be(1100m);
        output.Months[2].Income.Should().Be(1210m);
    }

    [Fact]
    public void Applies_inflation_to_expenses_only()
    {
        var model = new CashFlowProjectionModel();

        var output = model.Evaluate(Inputs(inflation: 0.05m));

        output.Months[0].Expenses.Should().Be(600m);
        output.Months[1].Expenses.Should().Be(630m);
        output.Months[1].Income.Should().Be(1000m);
    }

    [Fact]
    public void Subtracts_a_one_off_cost_from_starting_cash()
    {
        var model = new CashFlowProjectionModel();

        var output = model.Evaluate(Inputs(oneOff: 2000m));

        output.Months[0].CumulativeCash.Should().Be(5000m - 2000m + 400m);
    }

    [Fact]
    public void Detects_the_month_cash_turns_negative()
    {
        var model = new CashFlowProjectionModel();

        // 1000 starting cash, losing 400 per month: negative in month 3.
        var output = model.Evaluate(Inputs(income: 600m, expenses: 1000m, startingCash: 1000m));

        output.SummaryMetrics[MetricKeys.MonthsToDepletion].Should().Be(3m);
        output.Warnings.Should().ContainSingle(w => w.Contains("month 3"));
    }

    [Fact]
    public void Reports_no_depletion_when_cash_stays_positive()
    {
        var model = new CashFlowProjectionModel();

        var output = model.Evaluate(Inputs());

        output.SummaryMetrics[MetricKeys.MonthsToDepletion].Should().Be(0m);
        output.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Tracks_the_minimum_cash_balance()
    {
        var model = new CashFlowProjectionModel();

        var output = model.Evaluate(Inputs(income: 600m, expenses: 1000m, startingCash: 1000m));

        output.SummaryMetrics[MetricKeys.MinimumCash].Should().Be(1000m - (400m * 12));
    }

    [Fact]
    public void Throws_when_a_required_input_is_missing()
    {
        var model = new CashFlowProjectionModel();
        var inputs = new SimulationInputs(
            new Dictionary<string, decimal> { [InputKeys.MonthlyIncome] = 100m },
            12,
            "IRR");

        var act = () => model.Evaluate(inputs);

        act.Should().Throw<MissingInputException>().WithMessage("*monthly_expenses*");
    }

    [Fact]
    public void Declares_its_assumptions()
    {
        var model = new CashFlowProjectionModel();

        model.Assumptions.Should().NotBeEmpty();
        model.ModelVersion.Should().NotBeNullOrWhiteSpace();
        model.RequiredInputs.Should().Contain(InputKeys.MonthlyIncome);
    }
}

public class DebtAmortizationModelTests
{
    private static SimulationInputs Inputs(decimal principal, decimal annualRate, int months)
        => new(
            new Dictionary<string, decimal>
            {
                [InputKeys.LoanPrincipal] = principal,
                [InputKeys.AnnualInterestRate] = annualRate,
                [InputKeys.LoanTermMonths] = months
            },
            months,
            "IRR");

    [Fact]
    public void Computes_the_standard_annuity_instalment()
    {
        var model = new DebtAmortizationModel();

        // 100,000 at 12% nominal annual over 12 months: instalment is 8,884.88.
        var output = model.Evaluate(Inputs(100_000m, 0.12m, 12));

        output.SummaryMetrics[MetricKeys.MonthlyInstalment].Should().BeApproximately(8884.88m, 0.01m);
    }

    [Fact]
    public void Clears_the_balance_exactly_at_the_end_of_the_term()
    {
        var model = new DebtAmortizationModel();

        var output = model.Evaluate(Inputs(100_000m, 0.12m, 12));

        output.Months.Should().HaveCount(12);
        output.Months[^1].DebtBalance.Should().Be(0m);
    }

    [Fact]
    public void Handles_a_zero_interest_loan()
    {
        var model = new DebtAmortizationModel();

        var output = model.Evaluate(Inputs(12_000m, 0m, 12));

        output.SummaryMetrics[MetricKeys.MonthlyInstalment].Should().Be(1000m);
        output.SummaryMetrics[MetricKeys.TotalInterestPaid].Should().Be(0m);
    }

    [Fact]
    public void Total_interest_exceeds_zero_for_a_positive_rate()
    {
        var model = new DebtAmortizationModel();

        var output = model.Evaluate(Inputs(100_000m, 0.12m, 12));

        output.SummaryMetrics[MetricKeys.TotalInterestPaid].Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Rejects_a_non_positive_principal()
    {
        var model = new DebtAmortizationModel();

        var act = () => model.Evaluate(Inputs(0m, 0.12m, 12));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public class BusinessViabilityModelTests
{
    private static SimulationInputs Inputs(
        decimal startup, decimal revenue, decimal cost, decimal growth = 0m, int months = 24)
        => new(
            new Dictionary<string, decimal>
            {
                [InputKeys.BusinessStartupCost] = startup,
                [InputKeys.BusinessMonthlyRevenue] = revenue,
                [InputKeys.BusinessMonthlyCost] = cost,
                [InputKeys.BusinessRevenueGrowth] = growth
            },
            months,
            "IRR");

    [Fact]
    public void Finds_the_break_even_month()
    {
        var model = new BusinessViabilityModel();

        // 10,000 startup, 1,000 monthly profit: break-even in month 10.
        var output = model.Evaluate(Inputs(10_000m, 3_000m, 2_000m));

        output.SummaryMetrics[MetricKeys.BusinessBreakEvenMonth].Should().Be(10m);
    }

    [Fact]
    public void Reports_failure_to_break_even()
    {
        var model = new BusinessViabilityModel();

        var output = model.Evaluate(Inputs(10_000m, 1_000m, 2_000m));

        output.SummaryMetrics[MetricKeys.BusinessBreakEvenMonth].Should().Be(0m);
        output.Warnings.Should().ContainSingle(w => w.Contains("does not recover"));
    }

    [Fact]
    public void Growth_brings_break_even_forward()
    {
        var model = new BusinessViabilityModel();

        var flat = model.Evaluate(Inputs(10_000m, 2_500m, 2_000m));
        var growing = model.Evaluate(Inputs(10_000m, 2_500m, 2_000m, growth: 0.05m));

        growing.SummaryMetrics[MetricKeys.BusinessBreakEvenMonth]
            .Should().BeLessThan(flat.SummaryMetrics[MetricKeys.BusinessBreakEvenMonth]);
    }
}

public class RelocationComparisonModelTests
{
    [Fact]
    public void Reports_the_net_financial_difference()
    {
        var model = new RelocationComparisonModel();

        var inputs = new SimulationInputs(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = 2_000m,
                [InputKeys.MonthlyExpenses] = 1_500m,
                [InputKeys.DestinationMonthlyIncome] = 3_000m,
                [InputKeys.DestinationMonthlyExpenses] = 1_800m,
                [InputKeys.RelocationOneOffCost] = 5_000m
            },
            24,
            "IRR");

        var output = model.Evaluate(inputs);

        // Stay: 500/month. Move: 1,200/month minus 5,000 once.
        // Over 24 months: stay 12,000, move 28,800 - 5,000 = 23,800. Difference 11,800.
        output.SummaryMetrics[MetricKeys.RelocationNetBenefit].Should().Be(11_800m);
    }

    [Fact]
    public void Warns_when_relocation_never_pays_back()
    {
        var model = new RelocationComparisonModel();

        var inputs = new SimulationInputs(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = 2_000m,
                [InputKeys.MonthlyExpenses] = 1_500m,
                [InputKeys.DestinationMonthlyIncome] = 2_000m,
                [InputKeys.DestinationMonthlyExpenses] = 1_600m,
                [InputKeys.RelocationOneOffCost] = 5_000m
            },
            12,
            "IRR");

        var output = model.Evaluate(inputs);

        output.Warnings.Should().ContainSingle(w => w.Contains("does not recover"));
    }
}

public class FormulaModelTests
{
    [Fact]
    public void Evaluates_a_user_formula_per_month()
    {
        var model = new FormulaModel("income - expenses");

        var inputs = new SimulationInputs(
            new Dictionary<string, decimal> { ["income"] = 1000m, ["expenses"] = 400m },
            6,
            "IRR");

        var output = model.Evaluate(inputs);

        output.Months.Should().HaveCount(6);
        output.Months[0].NetCashFlow.Should().Be(600m);
        output.SummaryMetrics[MetricKeys.FinalCash].Should().Be(3600m);
    }

    [Fact]
    public void Exposes_month_and_cumulative_cash_as_built_ins()
    {
        var model = new FormulaModel("month * 100");

        var inputs = new SimulationInputs(new Dictionary<string, decimal>(), 3, "IRR");

        var output = model.Evaluate(inputs);

        output.Months.Select(m => m.NetCashFlow).Should().Equal(100m, 200m, 300m);
        model.RequiredInputs.Should().BeEmpty();
    }

    [Fact]
    public void Reports_only_user_supplied_variables_as_required()
    {
        var model = new FormulaModel("income - expenses + month");

        model.RequiredInputs.Should().BeEquivalentTo(["income", "expenses"]);
    }

    [Fact]
    public void Rejects_an_empty_formula()
    {
        var act = () => new FormulaModel("  ");

        act.Should().Throw<DecisionSimulator.Domain.Exceptions.FormulaEvaluationException>();
    }
}
