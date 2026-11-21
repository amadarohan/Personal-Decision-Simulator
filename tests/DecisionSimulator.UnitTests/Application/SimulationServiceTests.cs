using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Simulations;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// A deterministic stand-in for the real engine. It records what it was asked to
/// do so the tests can assert on the boundary between persisted assumptions and
/// the engine, without depending on the numerical models.
/// </summary>
internal sealed class FakeSimulationEngine : ISimulationEngine
{
    public const string ModelKey = "fake.model";

    public List<EngineRunRequest> ReceivedRequests { get; } = [];

    public List<string> ReceivedModelKeys { get; } = [];

    public IReadOnlyList<string> MissingInputs { get; set; } = [];

    public Exception? ThrowOnRun { get; set; }

    public IReadOnlyList<SimulationModelInfoDto> AvailableModels { get; } =
    [
        new SimulationModelInfoDto(
            ModelKey,
            "Fake model",
            "1.0.0",
            ModelType.CashFlowProjection,
            ["monthly_income", "monthly_expenses"],
            ["A fake assumption for testing."])
    ];

    public SimulationModelInfoDto? FindModel(string modelKey) =>
        string.Equals(modelKey, ModelKey, StringComparison.OrdinalIgnoreCase)
            ? AvailableModels[0]
            : null;

    public EngineRunResult Run(string modelKey, EngineRunRequest request)
    {
        ReceivedModelKeys.Add(modelKey);
        ReceivedRequests.Add(request);

        if (ThrowOnRun is not null)
        {
            throw ThrowOnRun;
        }

        return new EngineRunResult(
            ModelKey,
            "1.0.0",
            [
                new EngineMetric(
                    "final_cash",
                    1_000m,
                    100m,
                    800m,
                    1_200m,
                    new Dictionary<decimal, decimal> { [10] = 850m, [50] = 1_000m, [90] = 1_150m },
                    request.Iterations)
            ],
            [new EngineBand(0, 1_000m, new Dictionary<decimal, decimal> { [50] = 1_000m })],
            0.25m,
            new Dictionary<string, int> { ["cash_negative"] = 3 },
            false,
            true,
            ["Fake assumption."],
            MissingInputs);
    }

    public EngineSensitivityResult AnalyzeSensitivity(
        string modelKey,
        EngineRunRequest request,
        IReadOnlyDictionary<string, (decimal Low, decimal High)> ranges,
        string metricKey) =>
        new(
            metricKey,
            1_000m,
            [
                new EngineSensitivityEntry(
                    ranges.Keys.First(),
                    ranges.Values.First().Low,
                    ranges.Values.First().High,
                    900m,
                    1_100m,
                    200m)
            ],
            true);

    public EngineStressReport RunStressTests(string modelKey, EngineRunRequest request) =>
        new(
            new Dictionary<string, decimal> { ["final_cash"] = 1_000m },
            [
                new EngineStressOutcome(
                    "income-drop",
                    "Income drops 30 percent",
                    "A test shock.",
                    new Dictionary<string, decimal> { ["final_cash"] = 700m },
                    new Dictionary<string, decimal> { ["final_cash"] = -300m },
                    ["Cash falls below the baseline."],
                    true)
            ]);
}

public sealed class SimulationServiceTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();
    private readonly FakeSimulationEngine _engine = new();
    private readonly SimulationService _service;
    private readonly Guid _scenarioId;
    private readonly Guid _incomeDefinitionId;
    private readonly Guid _expenseDefinitionId;

    public SimulationServiceTests()
    {
        _service = new SimulationService(
            _db,
            _engine,
            new StubPreferences(UserPreferences.Default));

        var decision = new Decision("Simulation test", DecisionCategory.Finance);
        var scenario = decision.AddScenario("Baseline", ScenarioType.Baseline, 12);

        var income = new VariableDefinition(
            "monthly_income",
            "Monthly income",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "IRR");

        var expenses = new VariableDefinition(
            "monthly_expenses",
            "Monthly expenses",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "IRR");

        _db.Decisions.Add(decision);
        _db.VariableDefinitions.AddRange(income, expenses);
        _db.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();

        _scenarioId = scenario.Id;
        _incomeDefinitionId = income.Id;
        _expenseDefinitionId = expenses.Id;
    }

    public void Dispose() => _db.Dispose();

    private async Task SetVariableAsync(
        Guid definitionId,
        decimal value,
        decimal? min = null,
        decimal? max = null)
    {
        var scenario = await _db.Scenarios.FindAsync(_scenarioId);

        // A fixed distribution has no range by definition, so a ranged assumption
        // must be uniform. The domain enforces this by clearing min/max on Fixed.
        var distribution = min is not null && max is not null
            ? DistributionType.Uniform
            : DistributionType.Fixed;

        scenario!.SetVariable(
            definitionId,
            value,
            distribution,
            min,
            max,
            null,
            ConfidenceLevel.Medium,
            SourceType.UserInput,
            null,
            DistributionBasis.UserDefined,
            false);

        await _db.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public void Exposes_the_models_the_engine_reports()
    {
        _service.AvailableModels.Should().HaveCount(1);
        _service.AvailableModels[0].ModelKey.Should().Be(FakeSimulationEngine.ModelKey);
    }

    [Fact]
    public async Task Rejects_an_unknown_model_key()
    {
        var result = await _service.RunAsync(new RunSimulationRequest(_scenarioId, "does.not.exist"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Rejects_a_run_with_no_iterations()
    {
        var result = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 0));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Rejects_a_run_for_an_unknown_scenario()
    {
        var result = await _service.RunAsync(
            new RunSimulationRequest(Guid.NewGuid(), FakeSimulationEngine.ModelKey));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Passes_the_scenarios_assumptions_to_the_engine_by_variable_key()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);
        await SetVariableAsync(_expenseDefinitionId, 30_000_000m);

        var result = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 250, Seed: 42));

        result.Succeeded.Should().BeTrue();

        var request = _engine.ReceivedRequests.Should().ContainSingle().Subject;
        request.Iterations.Should().Be(250);
        request.Seed.Should().Be(42);
        request.TimeHorizonMonths.Should().Be(12);
        request.Variables.Should().HaveCount(2);
        request.Variables.Should().Contain(v => v.Key == "monthly_income" && v.Value == 50_000_000m);
        request.Variables.Should().Contain(v => v.Key == "monthly_expenses" && v.Value == 30_000_000m);
    }

    [Fact]
    public async Task Records_a_completed_run_with_its_seed_and_assumption_version()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        var result = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 100, Seed: 7));

        result.Succeeded.Should().BeTrue();
        result.Value.Status.Should().Be(SimulationRunStatus.Completed);
        result.Value.Seed.Should().Be(7);
        result.Value.Iterations.Should().Be(100);
        result.Value.AssumptionSetVersion.Should().Be(2);
        result.Value.Metrics.Should().HaveCount(1);
        result.Value.Metrics[0].MetricKey.Should().Be("final_cash");
        result.Value.Metrics[0].Unit.Should().Be(MetricUnit.Currency);
        result.Value.ShareOfPathsWithNegativeCash.Should().Be(0.25m);
        result.Value.CorrelationHonoured.Should().BeTrue();
    }

    [Fact]
    public async Task Marks_the_scenario_as_simulated_after_a_successful_run()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 10));

        var scenario = await _db.Scenarios.FindAsync(_scenarioId);
        scenario!.Status.Should().Be(ScenarioStatus.Simulated);
    }

    [Fact]
    public async Task Records_a_failed_run_when_required_assumptions_are_missing()
    {
        _engine.MissingInputs = ["monthly_income"];

        var result = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 10));

        // The run is still returned so the UI can show which assumption is missing.
        result.Succeeded.Should().BeTrue();
        result.Value.Status.Should().Be(SimulationRunStatus.Failed);
        result.Value.MissingInputs.Should().Contain("monthly_income");
        result.Value.ErrorMessage.Should().Contain("monthly_income");

        var stored = await _db.SimulationRuns.FindAsync(result.Value.RunId);
        stored!.Status.Should().Be(SimulationRunStatus.Failed);
    }

    [Fact]
    public async Task Records_a_failed_run_when_the_engine_rejects_an_assumption()
    {
        _engine.ThrowOnRun = new InvalidAssumptionException(
            "The variable uses a normal distribution but no standard deviation was supplied.");

        var result = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 10));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        result.ErrorMessage.Should().Contain("standard deviation");

        var runs = await _db.SimulationRuns.ToListAsync();
        runs.Should().ContainSingle();
        runs[0].Status.Should().Be(SimulationRunStatus.Failed);
    }

    [Fact]
    public async Task Stores_a_frozen_input_snapshot_with_the_run()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        var result = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 10, Seed: 99));

        var run = await _db.SimulationRuns.FindAsync(result.Value.RunId);

        run!.InputSnapshotJson.Should().Contain("monthly_income");
        run.InputSnapshotJson.Should().Contain("50000000");
        run.InputSnapshotJson.Should().Contain("99");
        run.RandomSeed.Should().Be(99);
    }

    [Fact]
    public async Task Reloads_a_recorded_run()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        var created = await _service.RunAsync(
            new RunSimulationRequest(_scenarioId, FakeSimulationEngine.ModelKey, Iterations: 10, Seed: 5));

        var loaded = await _service.GetRunAsync(created.Value.RunId);

        loaded.Succeeded.Should().BeTrue();
        loaded.Value.RunId.Should().Be(created.Value.RunId);
        loaded.Value.Seed.Should().Be(5);
        loaded.Value.Status.Should().Be(SimulationRunStatus.Completed);
    }

    [Fact]
    public async Task Returns_not_found_for_an_unknown_run()
    {
        var result = await _service.GetRunAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Sensitivity_requires_at_least_one_variable_with_a_range()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        var result = await _service.AnalyzeSensitivityAsync(
            _scenarioId,
            FakeSimulationEngine.ModelKey,
            "final_cash");

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.MissingAssumptions);
    }

    [Fact]
    public async Task Sensitivity_uses_the_declared_ranges()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m, min: 40_000_000m, max: 60_000_000m);

        var result = await _service.AnalyzeSensitivityAsync(
            _scenarioId,
            FakeSimulationEngine.ModelKey,
            "final_cash");

        result.Succeeded.Should().BeTrue();
        result.Value.MetricKey.Should().Be("final_cash");
        result.Value.Entries.Should().ContainSingle();
        result.Value.Entries[0].VariableKey.Should().Be("monthly_income");
        result.Value.Entries[0].LowValue.Should().Be(40_000_000m);
        result.Value.Entries[0].HighValue.Should().Be(60_000_000m);
        result.Value.Entries[0].Swing.Should().Be(200m);
    }

    [Fact]
    public async Task Stress_tests_report_the_change_against_the_baseline()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        var result = await _service.RunStressTestsAsync(
            _scenarioId,
            FakeSimulationEngine.ModelKey,
            "final_cash");

        result.Succeeded.Should().BeTrue();
        result.Value.BaselineMetricValue.Should().Be(1_000m);
        result.Value.Outcomes.Should().ContainSingle();

        var outcome = result.Value.Outcomes[0];
        outcome.ShockKey.Should().Be("income-drop");
        outcome.StressedMetricValue.Should().Be(700m);
        outcome.Change.Should().Be(-300m);
        outcome.ChangePercentage.Should().Be(-30m);
    }

    [Fact]
    public async Task Stress_tests_reject_a_metric_the_model_does_not_report()
    {
        await SetVariableAsync(_incomeDefinitionId, 50_000_000m);

        var result = await _service.RunStressTestsAsync(
            _scenarioId,
            FakeSimulationEngine.ModelKey,
            "not_a_metric");

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }
}
