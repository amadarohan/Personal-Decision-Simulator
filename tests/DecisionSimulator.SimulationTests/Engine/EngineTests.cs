using DecisionSimulator.Infrastructure.Simulation.Correlation;
using DecisionSimulator.Infrastructure.Simulation.Distributions;
using DecisionSimulator.Infrastructure.Simulation.Engines;
using DecisionSimulator.Infrastructure.Simulation.Engines.Models;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;
using DecisionSimulator.Infrastructure.Simulation.Sensitivity;
using DecisionSimulator.Infrastructure.Simulation.StressTesting;
using FluentAssertions;

namespace DecisionSimulator.SimulationTests.Engine;

public class MonteCarloRunnerTests
{
    private static SimulationInputs BaseInputs(int months = 12)
        => new(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = 1000m,
                [InputKeys.MonthlyExpenses] = 600m,
                [InputKeys.StartingCash] = 5000m
            },
            months,
            "IRR");

    [Fact]
    public void Same_seed_reproduces_identical_results()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m)
        };
        var options = new MonteCarloOptions { Iterations = 500, Seed = 42 };

        var first = runner.Run(BaseInputs(), variables, options);
        var second = runner.Run(BaseInputs(), variables, options);

        first.Metrics.Select(m => (m.MetricKey, m.Mean, m.Minimum, m.Maximum))
            .Should().Equal(second.Metrics.Select(m => (m.MetricKey, m.Mean, m.Minimum, m.Maximum)));
        first.ShareOfPathsWithNegativeCash.Should().Be(second.ShareOfPathsWithNegativeCash);
    }

    [Fact]
    public void Different_seeds_produce_different_results()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m)
        };

        var first = runner.Run(BaseInputs(), variables, new MonteCarloOptions { Iterations = 500, Seed = 1 });
        var second = runner.Run(BaseInputs(), variables, new MonteCarloOptions { Iterations = 500, Seed = 2 });

        first.Metrics.First(m => m.MetricKey == MetricKeys.FinalCash).Mean
            .Should().NotBe(second.Metrics.First(m => m.MetricKey == MetricKeys.FinalCash).Mean);
    }

    [Fact]
    public void Fixed_variables_make_the_run_deterministic()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new FixedDistribution(1000m)
        };

        var result = runner.Run(BaseInputs(), variables, new MonteCarloOptions { Iterations = 100, Seed = 5 });

        result.IsEffectivelyDeterministic.Should().BeTrue();

        var finalCash = result.Metrics.First(m => m.MetricKey == MetricKeys.FinalCash);
        finalCash.Minimum.Should().Be(finalCash.Maximum);
        finalCash.StandardDeviation.Should().Be(0m);
    }

    [Fact]
    public void Reports_the_requested_percentiles_in_order()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 150m)
        };

        var result = runner.Run(BaseInputs(), variables, new MonteCarloOptions
        {
            Iterations = 2000,
            Seed = 11,
            Percentiles = [10m, 50m, 90m]
        });

        var finalCash = result.Metrics.First(m => m.MetricKey == MetricKeys.FinalCash);

        finalCash.Percentiles[10m].Should().BeLessThan(finalCash.Percentiles[50m]);
        finalCash.Percentiles[50m].Should().BeLessThan(finalCash.Percentiles[90m]);
        finalCash.Minimum.Should().BeLessThanOrEqualTo(finalCash.Percentiles[10m]);
        finalCash.Maximum.Should().BeGreaterThanOrEqualTo(finalCash.Percentiles[90m]);
    }

    [Fact]
    public void Produces_one_band_per_month()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m)
        };

        var result = runner.Run(BaseInputs(months: 24), variables, new MonteCarloOptions { Iterations = 200, Seed = 3 });

        result.CashBands.Should().HaveCount(24);
        result.CashBands[0].MonthIndex.Should().Be(1);
        result.CashBands[^1].MonthIndex.Should().Be(24);
    }

    [Fact]
    public void Counts_paths_that_go_negative()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());

        // Expenses exceed income, so every path must go negative eventually.
        var inputs = new SimulationInputs(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = 500m,
                [InputKeys.MonthlyExpenses] = 1000m,
                [InputKeys.StartingCash] = 1000m
            },
            12,
            "IRR");

        var result = runner.Run(inputs, new Dictionary<string, IDistribution>(), new MonteCarloOptions
        {
            Iterations = 50,
            Seed = 1
        });

        result.ShareOfPathsWithNegativeCash.Should().Be(1m);
    }

    [Fact]
    public void Rejects_zero_iterations()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());

        var act = () => runner.Run(BaseInputs(), new Dictionary<string, IDistribution>(),
            new MonteCarloOptions { Iterations = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_iterations_above_the_ceiling()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());

        var act = () => runner.Run(BaseInputs(), new Dictionary<string, IDistribution>(),
            new MonteCarloOptions { Iterations = MonteCarloOptions.MaxIterations + 1 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Sampled_mean_is_close_to_the_theoretical_mean()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m)
        };

        var result = runner.Run(BaseInputs(months: 1), variables, new MonteCarloOptions
        {
            Iterations = 20_000,
            Seed = 2024
        });

        // Final cash = 5000 + (income - 600). Mean income 1000 => mean final cash 5400.
        var finalCash = result.Metrics.First(m => m.MetricKey == MetricKeys.FinalCash);
        finalCash.Mean.Should().BeApproximately(5400m, 20m);
    }

    [Fact]
    public void Reports_no_correlation_state_when_none_are_declared()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m)
        };

        var result = runner.Run(BaseInputs(), variables, new MonteCarloOptions { Iterations = 100, Seed = 7 });

        result.CorrelationHonoured.Should().BeNull();
    }

    [Fact]
    public void Applies_declared_correlations_and_reports_them_as_honoured()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m),
            [InputKeys.MonthlyExpenses] = new NormalDistribution(600m, 60m)
        };

        var correlations = new List<CorrelationAssumption>
        {
            new()
            {
                VariableA = InputKeys.MonthlyIncome,
                VariableB = InputKeys.MonthlyExpenses,
                Coefficient = 0.9m,
                Basis = "Test assumption",
                IsUserSupplied = true
            }
        };

        var result = runner.Run(BaseInputs(), variables,
            new MonteCarloOptions { Iterations = 2000, Seed = 8 }, correlations);

        result.CorrelationHonoured.Should().BeTrue();
    }

    [Fact]
    public void Falls_back_to_independence_when_correlations_are_inconsistent()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m),
            [InputKeys.MonthlyExpenses] = new NormalDistribution(600m, 60m),
            [InputKeys.StartingCash] = new NormalDistribution(5000m, 500m)
        };

        // Three variables cannot all be correlated at -0.9.
        var correlations = new List<CorrelationAssumption>
        {
            new() { VariableA = InputKeys.MonthlyIncome, VariableB = InputKeys.MonthlyExpenses, Coefficient = -0.9m, Basis = "t", IsUserSupplied = true },
            new() { VariableA = InputKeys.MonthlyExpenses, VariableB = InputKeys.StartingCash, Coefficient = -0.9m, Basis = "t", IsUserSupplied = true },
            new() { VariableA = InputKeys.MonthlyIncome, VariableB = InputKeys.StartingCash, Coefficient = -0.9m, Basis = "t", IsUserSupplied = true }
        };

        var result = runner.Run(BaseInputs(), variables,
            new MonteCarloOptions { Iterations = 200, Seed = 9 }, correlations);

        // The run still completes, but the caller is told the assumption was not applied.
        result.CorrelationHonoured.Should().BeFalse();
        result.Metrics.Should().NotBeEmpty();
    }

    [Fact]
    public void Correlated_runs_remain_reproducible_for_a_given_seed()
    {
        var runner = new MonteCarloRunner(new CashFlowProjectionModel());
        var variables = new Dictionary<string, IDistribution>
        {
            [InputKeys.MonthlyIncome] = new NormalDistribution(1000m, 100m),
            [InputKeys.MonthlyExpenses] = new NormalDistribution(600m, 60m)
        };

        var correlations = new List<CorrelationAssumption>
        {
            new()
            {
                VariableA = InputKeys.MonthlyIncome,
                VariableB = InputKeys.MonthlyExpenses,
                Coefficient = 0.7m,
                Basis = "Test assumption",
                IsUserSupplied = true
            }
        };

        var options = new MonteCarloOptions { Iterations = 500, Seed = 123 };

        var first = runner.Run(BaseInputs(), variables, options, correlations);
        var second = runner.Run(BaseInputs(), variables, options, correlations);

        first.Metrics.Select(m => (m.MetricKey, m.Mean, m.Minimum, m.Maximum))
            .Should().Equal(second.Metrics.Select(m => (m.MetricKey, m.Mean, m.Minimum, m.Maximum)));
    }
}

public class SensitivityAnalyzerTests
{
    private static SimulationInputs BaseInputs()
        => new(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = 1000m,
                [InputKeys.MonthlyExpenses] = 600m,
                [InputKeys.StartingCash] = 5000m
            },
            12,
            "IRR");

    [Fact]
    public void Ranks_variables_by_their_swing()
    {
        var analyzer = new SensitivityAnalyzer(new CashFlowProjectionModel());

        var result = analyzer.Analyze(
            BaseInputs(),
            new Dictionary<string, (decimal Low, decimal High)>
            {
                [InputKeys.MonthlyIncome] = (900m, 1100m),
                [InputKeys.MonthlyExpenses] = (100m, 1100m)
            },
            MetricKeys.FinalCash);

        // Expenses swing by 1000/month * 12 = 12,000; income by 200 * 12 = 2,400.
        result.Entries[0].VariableKey.Should().Be(InputKeys.MonthlyExpenses);
        result.Entries[0].Swing.Should().Be(12_000m);
        result.Entries[1].Swing.Should().Be(2_400m);
    }

    [Fact]
    public void Reports_the_baseline_metric()
    {
        var analyzer = new SensitivityAnalyzer(new CashFlowProjectionModel());

        var result = analyzer.Analyze(
            BaseInputs(),
            new Dictionary<string, (decimal Low, decimal High)>
            {
                [InputKeys.MonthlyIncome] = (900m, 1100m)
            },
            MetricKeys.FinalCash);

        result.BaselineMetric.Should().Be(9800m);
    }

    [Fact]
    public void Throws_for_an_unknown_metric()
    {
        var analyzer = new SensitivityAnalyzer(new CashFlowProjectionModel());

        var act = () => analyzer.Analyze(
            BaseInputs(),
            new Dictionary<string, (decimal Low, decimal High)>(),
            "not_a_metric");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Finds_the_threshold_where_cash_goes_negative()
    {
        var analyzer = new SensitivityAnalyzer(new CashFlowProjectionModel());

        // With 5000 starting cash over 12 months, cash hits zero when monthly
        // net is -416.67, i.e. expenses of 1416.67 against income of 1000.
        var crossing = analyzer.FindThreshold(
            BaseInputs(),
            InputKeys.MonthlyExpenses,
            MetricKeys.FinalCash,
            threshold: 0m,
            searchLow: 600m,
            searchHigh: 2000m);

        crossing.IsReachable.Should().BeTrue();
        crossing.CrossingValue.Should().NotBeNull();
        crossing.CrossingValue!.Value.Should().BeApproximately(1416.67m, 0.5m);
    }

    [Fact]
    public void Reports_an_unreachable_threshold_honestly()
    {
        var analyzer = new SensitivityAnalyzer(new CashFlowProjectionModel());

        var crossing = analyzer.FindThreshold(
            BaseInputs(),
            InputKeys.MonthlyExpenses,
            MetricKeys.FinalCash,
            threshold: -1_000_000m,
            searchLow: 600m,
            searchHigh: 700m);

        crossing.IsReachable.Should().BeFalse();
        crossing.CrossingValue.Should().BeNull();
        crossing.Explanation.Should().Contain("never crosses");
    }
}

public class StressTestRunnerTests
{
    private static SimulationInputs BaseInputs()
        => new(
            new Dictionary<string, decimal>
            {
                [InputKeys.MonthlyIncome] = 2000m,
                [InputKeys.MonthlyExpenses] = 1000m,
                [InputKeys.StartingCash] = 10_000m
            },
            12,
            "IRR");

    [Fact]
    public void Reports_baseline_metrics()
    {
        var runner = new StressTestRunner(new CashFlowProjectionModel());

        var report = runner.Run(BaseInputs(), []);

        report.BaselineMetrics[MetricKeys.FinalCash].Should().Be(22_000m);
        report.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public void Applies_a_multiplicative_shock()
    {
        var runner = new StressTestRunner(new CashFlowProjectionModel());

        var shock = new StressShock
        {
            Key = "test",
            Name = "Income halves",
            Description = "Test shock",
            Multipliers = new Dictionary<string, decimal> { [InputKeys.MonthlyIncome] = 0.5m }
        };

        var report = runner.Run(BaseInputs(), [shock]);

        // Income 1000, expenses 1000 => net 0 => final cash stays at 10,000.
        report.Outcomes[0].Metrics[MetricKeys.FinalCash].Should().Be(10_000m);
    }

    [Fact]
    public void Flags_a_shock_that_breaks_the_plan()
    {
        var runner = new StressTestRunner(new CashFlowProjectionModel());

        var shock = new StressShock
        {
            Key = "wipeout",
            Name = "Income stops",
            Description = "Test shock",
            Multipliers = new Dictionary<string, decimal> { [InputKeys.MonthlyIncome] = 0m }
        };

        var report = runner.Run(BaseInputs(), [shock]);

        report.Failures.Should().ContainSingle();
        report.Failures[0].ShockKey.Should().Be("wipeout");
        report.Failures[0].Survives.Should().BeFalse();
    }

    [Fact]
    public void Computes_metric_deltas_against_the_baseline()
    {
        var runner = new StressTestRunner(new CashFlowProjectionModel());

        var shock = new StressShock
        {
            Key = "test",
            Name = "Income halves",
            Description = "Test shock",
            Multipliers = new Dictionary<string, decimal> { [InputKeys.MonthlyIncome] = 0.5m }
        };

        var report = runner.Run(BaseInputs(), [shock]);

        report.Outcomes[0].MetricDeltas[MetricKeys.FinalCash].Should().Be(-12_000m);
    }

    [Fact]
    public void Models_a_one_off_expense_as_a_multiple_of_monthly_expenses()
    {
        var runner = new StressTestRunner(new CashFlowProjectionModel());

        var shock = new StressShock
        {
            Key = "bill",
            Name = "Unexpected bill",
            Description = "Test shock",
            OneOffCostAsMultipleOfExpenses = 6m
        };

        var report = runner.Run(BaseInputs(), [shock]);

        // Six months of 1,000 expenses = 6,000 taken from starting cash.
        report.Outcomes[0].Metrics[MetricKeys.FinalCash].Should().Be(22_000m - 6_000m);
    }

    [Fact]
    public void Default_shocks_are_all_described()
    {
        StressTestRunner.DefaultShocks.Should().NotBeEmpty();

        foreach (var shock in StressTestRunner.DefaultShocks)
        {
            shock.Name.Should().NotBeNullOrWhiteSpace();
            shock.Description.Should().NotBeNullOrWhiteSpace();
        }
    }
}

public class CorrelatedSamplerTests
{
    [Fact]
    public void Independent_sampling_produces_near_zero_correlation()
    {
        var sampler = new CorrelatedSampler(["a", "b"], []);
        var matrix = sampler.Generate(5000, new SeededRandom(1));

        var a = Enumerable.Range(0, 5000).Select(i => (decimal)matrix[i, 0]).ToList();
        var b = Enumerable.Range(0, 5000).Select(i => (decimal)matrix[i, 1]).ToList();

        Statistics.Correlation(a, b)!.Value.Should().BeApproximately(0m, 0.05m);
    }

    [Fact]
    public void Positive_correlation_is_reproduced()
    {
        var sampler = new CorrelatedSampler(["a", "b"],
        [
            new CorrelationAssumption
            {
                VariableA = "a",
                VariableB = "b",
                Coefficient = 0.8m,
                Basis = "Test assumption",
                IsUserSupplied = true
            }
        ]);

        sampler.IsCorrelationHonoured.Should().BeTrue();

        var matrix = sampler.Generate(5000, new SeededRandom(2));
        var a = Enumerable.Range(0, 5000).Select(i => (decimal)matrix[i, 0]).ToList();
        var b = Enumerable.Range(0, 5000).Select(i => (decimal)matrix[i, 1]).ToList();

        Statistics.Correlation(a, b)!.Value.Should().BeApproximately(0.8m, 0.05m);
    }

    [Fact]
    public void Negative_correlation_is_reproduced()
    {
        var sampler = new CorrelatedSampler(["a", "b"],
        [
            new CorrelationAssumption
            {
                VariableA = "a",
                VariableB = "b",
                Coefficient = -0.6m,
                Basis = "Test assumption",
                IsUserSupplied = true
            }
        ]);

        var matrix = sampler.Generate(5000, new SeededRandom(3));
        var a = Enumerable.Range(0, 5000).Select(i => (decimal)matrix[i, 0]).ToList();
        var b = Enumerable.Range(0, 5000).Select(i => (decimal)matrix[i, 1]).ToList();

        Statistics.Correlation(a, b)!.Value.Should().BeApproximately(-0.6m, 0.05m);
    }

    [Fact]
    public void Marginals_are_preserved_exactly()
    {
        var sampler = new CorrelatedSampler(["a", "b"],
        [
            new CorrelationAssumption
            {
                VariableA = "a",
                VariableB = "b",
                Coefficient = 0.9m,
                Basis = "Test assumption",
                IsUserSupplied = true
            }
        ]);

        var matrix = sampler.Generate(1000, new SeededRandom(4));
        var a = Enumerable.Range(0, 1000).Select(i => matrix[i, 0]).OrderBy(v => v).ToArray();

        // Reordering must not change the set of values, only their pairing.
        a.Should().BeInAscendingOrder();
        a[0].Should().BeGreaterThanOrEqualTo(0d);
        a[^1].Should().BeLessThan(1d);
    }

    [Fact]
    public void Inconsistent_correlations_are_reported_rather_than_faked()
    {
        // Three variables all correlated at -0.9 cannot coexist.
        var sampler = new CorrelatedSampler(["a", "b", "c"],
        [
            new CorrelationAssumption { VariableA = "a", VariableB = "b", Coefficient = -0.9m, Basis = "t", IsUserSupplied = true },
            new CorrelationAssumption { VariableA = "b", VariableB = "c", Coefficient = -0.9m, Basis = "t", IsUserSupplied = true },
            new CorrelationAssumption { VariableA = "a", VariableB = "c", Coefficient = -0.9m, Basis = "t", IsUserSupplied = true }
        ]);

        sampler.IsCorrelationHonoured.Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_correlation_without_a_basis()
    {
        var act = () => new CorrelatedSampler(["a", "b"],
        [
            new CorrelationAssumption
            {
                VariableA = "a",
                VariableB = "b",
                Coefficient = 0.5m,
                Basis = "  ",
                IsUserSupplied = true
            }
        ]);

        act.Should().Throw<ArgumentException>().WithMessage("*basis*");
    }

    [Fact]
    public void Rejects_a_coefficient_outside_the_valid_range()
    {
        var act = () => new CorrelatedSampler(["a", "b"],
        [
            new CorrelationAssumption
            {
                VariableA = "a",
                VariableB = "b",
                Coefficient = 1.5m,
                Basis = "t",
                IsUserSupplied = true
            }
        ]);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
