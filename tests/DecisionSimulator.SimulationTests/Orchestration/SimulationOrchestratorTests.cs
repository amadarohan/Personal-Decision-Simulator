using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Simulation.Engines.Models;
using DecisionSimulator.Infrastructure.Simulation.MonteCarlo;
using DecisionSimulator.Infrastructure.Simulation.Orchestration;
using FluentAssertions;

namespace DecisionSimulator.SimulationTests.Orchestration;

public class SimulationOrchestratorTests
{
    private static Scenario NewScenario(int months = 12)
    {
        var decision = new Decision("Test decision", DecisionCategory.Career);
        return decision.AddScenario(
            name: "Baseline",
            type: ScenarioType.Baseline,
            timeHorizonMonths: months,
            alternativeId: null,
            parentScenarioId: null,
            description: null);
    }

    private static ResolvedVariable Variable(
        Scenario scenario,
        string key,
        decimal value,
        DistributionType type = DistributionType.Fixed)
    {
        var definition = new VariableDefinition(
            key,
            key.Replace('_', ' '),
            VariableCategory.Financial,
            VariableDataType.Currency,
            "IRR");

        // A normal distribution needs a sigma; the domain rejects it otherwise.
        var sigma = type == DistributionType.Normal ? value * 0.1m : (decimal?)null;

        var scenarioVariable = scenario.SetVariable(
            definition.Id,
            value,
            type,
            minimumValue: null,
            maximumValue: null,
            standardDeviation: sigma,
            confidence: ConfidenceLevel.Medium,
            sourceType: SourceType.UserInput,
            sourceReference: null,
            distributionBasis: DistributionBasis.UserDefined,
            isUserOverride: true);

        return new ResolvedVariable(key, scenarioVariable);
    }

    private static SimulationRequest Request(
        Scenario scenario,
        IReadOnlyList<ResolvedVariable> variables,
        int iterations = 100,
        int seed = 1)
        => new()
        {
            Scenario = scenario,
            Variables = variables,
            Options = new MonteCarloOptions { Iterations = iterations, Seed = seed },
            CurrencyCode = "IRR"
        };

    [Fact]
    public void Runs_a_complete_scenario_and_records_the_run()
    {
        var scenario = NewScenario();
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var outcome = orchestrator.Run(Request(scenario,
        [
            Variable(scenario, InputKeys.MonthlyIncome, 1000m),
            Variable(scenario, InputKeys.MonthlyExpenses, 600m),
            Variable(scenario, InputKeys.StartingCash, 5000m)
        ]));

        outcome.MissingInputs.Should().BeEmpty();
        outcome.Run.Status.Should().Be(SimulationRunStatus.Completed);
        outcome.Run.OutputSummaryJson.Should().NotBeNullOrWhiteSpace();
        outcome.Result.Metrics.Should().NotBeEmpty();
        scenario.Runs.Should().ContainSingle();
        scenario.Status.Should().Be(ScenarioStatus.Simulated);
    }

    [Fact]
    public void Reports_missing_inputs_instead_of_defaulting_them()
    {
        var scenario = NewScenario();
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var outcome = orchestrator.Run(Request(scenario,
        [
            Variable(scenario, InputKeys.MonthlyIncome, 1000m)
        ]));

        outcome.MissingInputs.Should().BeEquivalentTo([InputKeys.MonthlyExpenses, InputKeys.StartingCash]);
        outcome.Run.Status.Should().Be(SimulationRunStatus.Failed);
        outcome.Run.ErrorMessage.Should().Contain(InputKeys.MonthlyExpenses);
        outcome.Result.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void FindMissingInputs_does_not_execute_the_model()
    {
        var scenario = NewScenario();
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var missing = orchestrator.FindMissingInputs(Request(scenario,
        [
            Variable(scenario, InputKeys.MonthlyIncome, 1000m),
            Variable(scenario, InputKeys.MonthlyExpenses, 600m),
            Variable(scenario, InputKeys.StartingCash, 5000m)
        ]));

        missing.Should().BeEmpty();
        scenario.Runs.Should().BeEmpty();
    }

    [Fact]
    public void Snapshot_records_every_input_and_the_seed()
    {
        var scenario = NewScenario();
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var outcome = orchestrator.Run(Request(scenario,
        [
            Variable(scenario, InputKeys.MonthlyIncome, 1000m),
            Variable(scenario, InputKeys.MonthlyExpenses, 600m),
            Variable(scenario, InputKeys.StartingCash, 5000m)
        ], iterations: 250, seed: 987));

        outcome.Run.InputSnapshotJson.Should().Contain("\"seed\":987");
        outcome.Run.InputSnapshotJson.Should().Contain(InputKeys.MonthlyIncome);
        outcome.Run.InputSnapshotJson.Should().Contain("\"iterations\":250");
        outcome.Run.RandomSeed.Should().Be(987);
        outcome.Run.IterationCount.Should().Be(250);
    }

    [Fact]
    public void Same_seed_and_inputs_reproduce_the_same_summary()
    {
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var firstScenario = NewScenario();
        var first = orchestrator.Run(Request(firstScenario,
        [
            Variable(firstScenario, InputKeys.MonthlyIncome, 1000m, DistributionType.Normal),
            Variable(firstScenario, InputKeys.MonthlyExpenses, 600m),
            Variable(firstScenario, InputKeys.StartingCash, 5000m)
        ], iterations: 300, seed: 55));

        var secondScenario = NewScenario();
        var second = orchestrator.Run(Request(secondScenario,
        [
            Variable(secondScenario, InputKeys.MonthlyIncome, 1000m, DistributionType.Normal),
            Variable(secondScenario, InputKeys.MonthlyExpenses, 600m),
            Variable(secondScenario, InputKeys.StartingCash, 5000m)
        ], iterations: 300, seed: 55));

        first.Run.OutputSummaryJson.Should().Be(second.Run.OutputSummaryJson);
    }

    [Fact]
    public void Exposes_the_model_assumptions_with_every_outcome()
    {
        var scenario = NewScenario();
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var outcome = orchestrator.Run(Request(scenario,
        [
            Variable(scenario, InputKeys.MonthlyIncome, 1000m),
            Variable(scenario, InputKeys.MonthlyExpenses, 600m),
            Variable(scenario, InputKeys.StartingCash, 5000m)
        ]));

        outcome.ModelAssumptions.Should().NotBeEmpty();
        outcome.ModelAssumptions.Should().BeEquivalentTo(orchestrator.Model.Assumptions);
    }

    [Fact]
    public void Ignores_variables_the_model_does_not_consume()
    {
        var scenario = NewScenario();
        var orchestrator = new SimulationOrchestrator(new CashFlowProjectionModel());

        var outcome = orchestrator.Run(Request(scenario,
        [
            Variable(scenario, InputKeys.MonthlyIncome, 1000m),
            Variable(scenario, InputKeys.MonthlyExpenses, 600m),
            Variable(scenario, InputKeys.StartingCash, 5000m),
            Variable(scenario, "unrelated_variable", 42m, DistributionType.Normal)
        ]));

        // The unused variable must not inflate the reported uncertainty.
        outcome.Result.IsEffectivelyDeterministic.Should().BeTrue();
    }
}

