using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Simulations;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Persistence;
using DecisionSimulator.Infrastructure.Simulation.Adapters;
using DecisionSimulator.IntegrationTests.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionSimulator.IntegrationTests.Simulation;

/// <summary>
/// Exercises the whole path a user actually walks: the seeded catalogue supplies
/// the definitions, the scenario carries the assumptions, and the real engine
/// produces a result.
/// <para>
/// The unit tests substitute a fake engine, so they cannot catch a mismatch
/// between the catalogue keys and the keys the real models bind to. That
/// mismatch is exactly the failure this test exists to prevent: it would surface
/// to the user as "missing input" on every run, with no obvious cause.
/// </para>
/// </summary>
public sealed class SimulationEndToEndTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public SimulationEndToEndTests(SqlServerTestDatabase database) => _database = database;

    private sealed class StubPreferences : IUserPreferencesAccessor
    {
        public Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(UserPreferences.Default);
    }

    [Fact]
    public async Task Runs_the_cash_flow_model_from_the_seeded_catalogue()
    {
        await using var db = _database.CreateContext();

        await new BuiltInDataSeeder(db, NullLogger<BuiltInDataSeeder>.Instance).SeedAsync();

        var definitions = await db.VariableDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Key, StringComparer.OrdinalIgnoreCase);

        var decision = new Decision("Emergency fund", DecisionCategory.Finance);
        var scenario = decision.AddScenario("Expected", ScenarioType.Expected, 12);

        void Assume(string key, decimal value) =>
            scenario.SetVariable(
                definitions[key].Id,
                value,
                DistributionType.Fixed,
                minimumValue: null,
                maximumValue: null,
                standardDeviation: null,
                confidence: ConfidenceLevel.Medium,
                sourceType: SourceType.UserInput,
                sourceReference: null,
                distributionBasis: DistributionBasis.UserDefined,
                isUserOverride: false);

        Assume("monthly_income", 100_000_000m);
        Assume("monthly_expenses", 70_000_000m);
        Assume("starting_cash", 300_000_000m);

        db.Decisions.Add(decision);
        await db.SaveChangesAsync();

        var service = new SimulationService(
            db,
            new SimulationEngineAdapter(),
            new StubPreferences());

        var result = await service.RunAsync(
            new RunSimulationRequest(scenario.Id, "cashflow.projection", Iterations: 200, Seed: 42));

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        result.Value.Should().NotBeNull();

        var run = result.Value!;

        run.MissingInputs.Should().BeEmpty();
        run.Metrics.Should().NotBeEmpty();
        run.Bands.Should().HaveCount(12);

        // 30M net per month over 12 months on top of 300M starting cash.
        var finalCash = run.Metrics.Single(m => m.MetricKey == "final_cash");
        finalCash.Mean.Should().Be(660_000_000m);

        // Every assumption is fixed, so the run must be reported as deterministic
        // rather than dressed up as a distribution.
        run.IsEffectivelyDeterministic.Should().BeTrue();
    }

    [Fact]
    public async Task Reports_missing_inputs_instead_of_inventing_them()
    {
        await using var db = _database.CreateContext();

        await new BuiltInDataSeeder(db, NullLogger<BuiltInDataSeeder>.Instance).SeedAsync();

        var definitions = await db.VariableDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Key, StringComparer.OrdinalIgnoreCase);

        var decision = new Decision("Partial assumptions", DecisionCategory.Finance);
        var scenario = decision.AddScenario("Incomplete", ScenarioType.Custom, 12);

        // Deliberately omit monthly_expenses and starting_cash.
        scenario.SetVariable(
            definitions["monthly_income"].Id,
            100_000_000m,
            DistributionType.Fixed,
            null,
            null,
            null,
            ConfidenceLevel.Medium,
            SourceType.UserInput,
            null,
            DistributionBasis.UserDefined,
            false);

        db.Decisions.Add(decision);
        await db.SaveChangesAsync();

        var service = new SimulationService(
            db,
            new SimulationEngineAdapter(),
            new StubPreferences());

        var result = await service.RunAsync(
            new RunSimulationRequest(scenario.Id, "cashflow.projection", Iterations: 10, Seed: 1));

        // The service reports an incomplete run as a successful call carrying the
        // missing keys, so the UI can name them. It never substitutes a default
        // and never returns a number the user did not supply.
        result.Succeeded.Should().BeTrue(result.ErrorMessage);

        var run = result.Value!;

        run.Status.Should().Be(SimulationRunStatus.Failed);
        run.MissingInputs.Should().BeEquivalentTo(["monthly_expenses", "starting_cash"]);
        run.Metrics.Should().BeEmpty();
        run.ErrorMessage.Should().Contain("monthly_expenses");
    }
}
