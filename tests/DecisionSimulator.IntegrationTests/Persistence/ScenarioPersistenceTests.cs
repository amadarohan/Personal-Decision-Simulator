using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// Verifies the scenario aggregate, its variables with full provenance, and the
/// simulation run history that must survive scenario edits.
/// </summary>
public sealed class ScenarioPersistenceTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public ScenarioPersistenceTests(SqlServerTestDatabase database) => _database = database;

    private static VariableDefinition NewVariableDefinition(string key) =>
        new(key, key.Replace('_', ' '), VariableCategory.Financial, VariableDataType.Currency, "IRR");

    [Fact]
    public async Task Persists_scenario_variables_with_provenance_and_distribution()
    {
        Guid scenarioId;

        await using (var db = _database.CreateContext())
        {
            var definition = NewVariableDefinition("monthly_income");
            db.VariableDefinitions.Add(definition);

            var decision = new Decision("Income modelling", DecisionCategory.Career);
            var scenario = decision.AddScenario("Expected", ScenarioType.Expected, 24);

            scenario.SetVariable(
                definition.Id,
                value: 85_000_000m,
                distributionType: DistributionType.Normal,
                minimumValue: null,
                maximumValue: null,
                standardDeviation: 8_500_000m,
                confidence: ConfidenceLevel.Medium,
                sourceType: SourceType.UserInput,
                sourceReference: "Payslip, last 6 months",
                distributionBasis: DistributionBasis.UserDefined,
                isUserOverride: true);

            db.Decisions.Add(decision);
            await db.SaveChangesAsync();

            scenarioId = scenario.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var scenario = await db.Scenarios
                .Include(s => s.Variables)
                .SingleAsync(s => s.Id == scenarioId);

            var variable = scenario.Variables.Should().ContainSingle().Subject;

            variable.Value.Should().Be(85_000_000m);
            variable.StandardDeviation.Should().Be(8_500_000m);
            variable.DistributionType.Should().Be(DistributionType.Normal);
            variable.Confidence.Should().Be(ConfidenceLevel.Medium);
            variable.SourceType.Should().Be(SourceType.UserInput);
            variable.SourceReference.Should().Be("Payslip, last 6 months");
            variable.DistributionBasis.Should().Be(DistributionBasis.UserDefined);
            variable.IsUserOverride.Should().BeTrue();

            // Setting a variable bumps the assumption version, which is what
            // makes old simulation runs interpretable.
            scenario.AssumptionSetVersion.Should().Be(2);
        }
    }

    [Fact]
    public async Task Rejects_two_values_for_the_same_variable_in_one_scenario()
    {
        await using var db = _database.CreateContext();

        var definition = NewVariableDefinition("monthly_expenses");
        db.VariableDefinitions.Add(definition);

        var decision = new Decision("Expense modelling", DecisionCategory.Finance);
        var scenario = decision.AddScenario("Baseline", ScenarioType.Baseline, 12);

        scenario.SetVariable(definition.Id, 30_000_000m, DistributionType.Fixed, null, null, null,
            ConfidenceLevel.High, SourceType.UserInput, null, DistributionBasis.UserDefined, false);

        db.Decisions.Add(decision);
        await db.SaveChangesAsync();

        // Insert a duplicate row directly, bypassing the domain's replace logic.
        // ExecuteSqlRawAsync surfaces the provider exception directly; only
        // SaveChanges wraps failures in DbUpdateException.
        var act = async () => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ScenarioVariables
                (Id, ScenarioId, VariableDefinitionId, Value, DistributionType, Confidence,
                 SourceType, DistributionBasis, IsUserOverride, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (NEWID(), {0}, {1}, 1, 'Fixed', 'High', 'UserInput', 'UserDefined', 0, SYSUTCDATETIME(), SYSUTCDATETIME())
            """,
            scenario.Id,
            definition.Id);

        await act.Should().ThrowAsync<SqlException>()
            .Where(ex => ex.Message.Contains("IX_ScenarioVariables_ScenarioId_VariableDefinitionId"));
    }

    [Fact]
    public async Task Simulation_run_keeps_its_frozen_inputs_and_results()
    {
        Guid runId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Run history", DecisionCategory.Investment);
            var scenario = decision.AddScenario("Monte Carlo", ScenarioType.MonteCarlo, 36);

            var run = scenario.RegisterRun(
                simulationModelId: Guid.NewGuid(),
                modelVersion: "1.0.0",
                runType: SimulationRunType.MonteCarlo,
                iterationCount: 1000,
                randomSeed: 42,
                inputSnapshotJson: """{"monthly_income":85000000}""");

            run.MarkRunning();
            run.Complete("""{"metrics":["net_cash_flow"]}""");

            db.Decisions.Add(decision);
            await db.SaveChangesAsync();

            runId = run.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var run = await db.SimulationRuns.SingleAsync(r => r.Id == runId);

            run.ModelVersion.Should().Be("1.0.0");
            run.IterationCount.Should().Be(1000);
            run.RandomSeed.Should().Be(42);
            run.Status.Should().Be(SimulationRunStatus.Completed);
            run.InputSnapshotJson.Should().Contain("monthly_income");
            run.OutputSummaryJson.Should().Contain("net_cash_flow");
            run.CompletedAtUtc.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Rejects_editing_a_frozen_run_input()
    {
        await using var db = _database.CreateContext();

        var decision = new Decision("Frozen inputs", DecisionCategory.Investment);
        var scenario = decision.AddScenario("Baseline", ScenarioType.Baseline, 12);

        var run = scenario.RegisterRun(Guid.NewGuid(), "1.0.0", SimulationRunType.Deterministic, 1, null, "{}");
        run.Complete("{}");

        db.Decisions.Add(decision);
        await db.SaveChangesAsync();

        // The seed is part of what makes the run reproducible; changing it after
        // the fact would silently invalidate the stored output.
        db.Entry(run).Property(nameof(SimulationRun.RandomSeed)).CurrentValue = 999;

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*frozen input snapshot*");
    }

    [Fact]
    public async Task Rejects_deleting_an_append_only_audit_row()
    {
        await using var db = _database.CreateContext();

        var log = new AuditLog("Decision.Created", nameof(Decision), Guid.NewGuid().ToString());
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        db.AuditLogs.Remove(log);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }
}
