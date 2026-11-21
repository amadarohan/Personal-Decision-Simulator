using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Decisions;
using DecisionSimulator.Application.Features.Scenarios;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class ScenarioServiceTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();
    private readonly ScenarioService _service;
    private readonly DecisionService _decisions;
    private readonly Guid _decisionId;
    private readonly Guid _incomeDefinitionId;
    private readonly Guid _expenseDefinitionId;

    public ScenarioServiceTests()
    {
        _service = new ScenarioService(_db);
        _decisions = new DecisionService(_db, new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var decision = new Decision("Test decision", DecisionCategory.Finance);
        _db.Decisions.Add(decision);

        var income = new VariableDefinition(
            "monthly_income",
            "Monthly income",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "IRR",
            defaultValue: 50_000_000m,
            minimumAllowedValue: 0m,
            maximumAllowedValue: 1_000_000_000m);

        var expenses = new VariableDefinition(
            "monthly_expenses",
            "Monthly expenses",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "IRR",
            defaultValue: 30_000_000m,
            minimumAllowedValue: 0m);

        _db.VariableDefinitions.AddRange(income, expenses);
        _db.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();

        _decisionId = decision.Id;
        _incomeDefinitionId = income.Id;
        _expenseDefinitionId = expenses.Id;
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> CreateScenarioAsync(string name = "Baseline") =>
        (await _service.CreateAsync(
            _decisionId,
            new CreateScenarioRequest(name, ScenarioType.Baseline, 12))).Value;

    [Fact]
    public async Task Creates_a_scenario_under_a_decision()
    {
        var result = await _service.CreateAsync(
            _decisionId,
            new CreateScenarioRequest(
                "Optimistic year",
                ScenarioType.Optimistic,
                24,
                Description: "Income grows faster than inflation."));

        result.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(result.Value);
        detail.Succeeded.Should().BeTrue();
        detail.Value.Name.Should().Be("Optimistic year");
        detail.Value.Type.Should().Be(ScenarioType.Optimistic);
        detail.Value.TimeHorizonMonths.Should().Be(24);
        detail.Value.DecisionId.Should().Be(_decisionId);
        detail.Value.AssumptionSetVersion.Should().Be(1);
        detail.Value.Variables.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_a_scenario_for_an_unknown_decision()
    {
        var result = await _service.CreateAsync(
            Guid.NewGuid(),
            new CreateScenarioRequest("Orphan", ScenarioType.Custom, 12));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Rejects_a_scenario_that_references_another_decisions_alternative()
    {
        var other = new Decision("Other decision", DecisionCategory.Career);
        _db.Decisions.Add(other);
        await _db.SaveChangesAsync(CancellationToken.None);

        var foreignAlternative = other.AddAlternative("Foreign", null, false, null, null, null, null);
        await _db.SaveChangesAsync(CancellationToken.None);

        var result = await _service.CreateAsync(
            _decisionId,
            new CreateScenarioRequest("Bad link", ScenarioType.Custom, 12, AlternativeId: foreignAlternative.Id));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Setting_a_variable_bumps_the_assumption_set_version()
    {
        var scenarioId = await CreateScenarioAsync();

        var before = await _service.GetAsync(scenarioId);
        before.Value.AssumptionSetVersion.Should().Be(1);

        var set = await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(_incomeDefinitionId, 60_000_000m));

        set.Succeeded.Should().BeTrue();

        var after = await _service.GetAsync(scenarioId);
        after.Value.AssumptionSetVersion.Should().Be(2);
        after.Value.Variables.Should().HaveCount(1);
        after.Value.Variables[0].VariableKey.Should().Be("monthly_income");
        after.Value.Variables[0].Value.Should().Be(60_000_000m);
        after.Value.Variables[0].SourceType.Should().Be(SourceType.UserInput);
    }

    [Fact]
    public async Task Setting_the_same_variable_twice_replaces_it_rather_than_duplicating()
    {
        var scenarioId = await CreateScenarioAsync();

        await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(_incomeDefinitionId, 60_000_000m));

        await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(_incomeDefinitionId, 70_000_000m));

        var detail = await _service.GetAsync(scenarioId);
        detail.Value.Variables.Should().HaveCount(1);
        detail.Value.Variables[0].Value.Should().Be(70_000_000m);
    }

    [Fact]
    public async Task Rejects_a_value_outside_the_definitions_allowed_range()
    {
        var scenarioId = await CreateScenarioAsync();

        var result = await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(_incomeDefinitionId, 2_000_000_000m));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        result.ErrorMessage.Should().Contain("cannot be above");
    }

    [Fact]
    public async Task Rejects_a_normal_distribution_without_a_standard_deviation()
    {
        var scenarioId = await CreateScenarioAsync();

        var result = await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(
                _incomeDefinitionId,
                60_000_000m,
                DistributionType.Normal));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Accepts_a_normal_distribution_with_a_standard_deviation()
    {
        var scenarioId = await CreateScenarioAsync();

        var result = await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(
                _incomeDefinitionId,
                60_000_000m,
                DistributionType.Normal,
                StandardDeviation: 6_000_000m,
                Confidence: ConfidenceLevel.Medium));

        result.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(scenarioId);
        detail.Value.Variables[0].DistributionType.Should().Be(DistributionType.Normal);
        detail.Value.Variables[0].StandardDeviation.Should().Be(6_000_000m);
        detail.Value.Variables[0].Confidence.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public async Task Rejects_a_variable_definition_that_does_not_exist()
    {
        var scenarioId = await CreateScenarioAsync();

        var result = await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(Guid.NewGuid(), 1m));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Removes_a_variable()
    {
        var scenarioId = await CreateScenarioAsync();

        await _service.SetVariableAsync(
            scenarioId,
            new SetScenarioVariableRequest(_incomeDefinitionId, 60_000_000m));

        var detail = await _service.GetAsync(scenarioId);
        var variableId = detail.Value.Variables[0].Id;

        var removed = await _service.RemoveVariableAsync(scenarioId, variableId);

        removed.Succeeded.Should().BeTrue();

        var after = await _service.GetAsync(scenarioId);
        after.Value.Variables.Should().BeEmpty();
    }

    [Fact]
    public async Task Setting_a_new_baseline_clears_the_previous_one()
    {
        var first = await CreateScenarioAsync("First");
        var second = await CreateScenarioAsync("Second");

        (await _service.SetBaselineAsync(first, true)).Succeeded.Should().BeTrue();
        (await _service.SetBaselineAsync(second, true)).Succeeded.Should().BeTrue();

        var firstDetail = await _service.GetAsync(first);
        var secondDetail = await _service.GetAsync(second);

        firstDetail.Value.IsBaseline.Should().BeFalse();
        secondDetail.Value.IsBaseline.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_a_probability_weight_outside_zero_to_one()
    {
        var scenarioId = await CreateScenarioAsync();

        var result = await _service.SetProbabilityWeightAsync(scenarioId, 1.5m);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Accepts_a_probability_weight_within_range()
    {
        var scenarioId = await CreateScenarioAsync();

        var result = await _service.SetProbabilityWeightAsync(scenarioId, 0.35m);

        result.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(scenarioId);
        detail.Value.ProbabilityWeight.Should().Be(0.35m);
    }

    [Fact]
    public async Task Rejects_a_dependency_on_a_scenario_from_another_decision()
    {
        var scenarioId = await CreateScenarioAsync();

        var other = new Decision("Other", DecisionCategory.Other);
        var foreignScenario = other.AddScenario("Foreign", ScenarioType.Custom, 12);
        _db.Decisions.Add(other);
        await _db.SaveChangesAsync(CancellationToken.None);

        var result = await _service.AddDependencyAsync(
            scenarioId,
            foreignScenario.Id,
            ScenarioRelationshipType.VariantOf);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Adds_and_removes_a_dependency_within_the_same_decision()
    {
        var parent = await CreateScenarioAsync("Parent");
        var child = await CreateScenarioAsync("Child");

        var added = await _service.AddDependencyAsync(
            child,
            parent,
            ScenarioRelationshipType.Inherits);

        added.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(child);
        detail.Value.Dependencies.Should().HaveCount(1);
        detail.Value.Dependencies[0].DependsOnScenarioId.Should().Be(parent);

        var removed = await _service.RemoveDependencyAsync(child, parent);
        removed.Succeeded.Should().BeTrue();

        var after = await _service.GetAsync(child);
        after.Value.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task Deletes_a_scenario()
    {
        var scenarioId = await CreateScenarioAsync();

        var deleted = await _service.DeleteAsync(scenarioId);
        deleted.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(scenarioId);
        detail.Succeeded.Should().BeFalse();
        detail.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Updates_the_descriptive_fields_of_a_scenario()
    {
        var scenarioId = await CreateScenarioAsync();

        var updated = await _service.UpdateAsync(
            scenarioId,
            new UpdateScenarioRequest("Renamed", "New description", ScenarioType.StressTest, 36));

        updated.Succeeded.Should().BeTrue();

        var detail = await _service.GetAsync(scenarioId);
        detail.Value.Name.Should().Be("Renamed");
        detail.Value.Description.Should().Be("New description");
        detail.Value.Type.Should().Be(ScenarioType.StressTest);
        detail.Value.TimeHorizonMonths.Should().Be(36);
    }
}
