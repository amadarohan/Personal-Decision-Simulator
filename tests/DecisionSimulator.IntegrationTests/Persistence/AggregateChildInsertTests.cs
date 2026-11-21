using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// Regression coverage for a bug that only appears once an aggregate is already
/// persisted: adding a child to a tracked aggregate must produce an INSERT.
/// <para>
/// The domain assigns every primary key in the constructor. EF's convention
/// treats a Guid key as database-generated, so a child whose key was already set
/// was classified as <c>Modified</c> and EF issued an <c>UPDATE</c> against a row
/// that had never been inserted. The in-memory provider tolerated this; SQL
/// Server raised a concurrency error. These tests use the real provider so the
/// behaviour cannot regress silently.
/// </para>
/// </summary>
public sealed class AggregateChildInsertTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public AggregateChildInsertTests(SqlServerTestDatabase database) => _database = database;

    [Fact]
    public async Task Adding_an_alternative_to_a_saved_decision_inserts_a_row()
    {
        Guid decisionId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Add a child later", DecisionCategory.Career);
            db.Decisions.Add(decision);
            await db.SaveChangesAsync();
            decisionId = decision.Id;
        }

        Guid alternativeId;

        await using (var db = _database.CreateContext())
        {
            var decision = await db.Decisions
                .Include(d => d.Alternatives)
                .SingleAsync(d => d.Id == decisionId);

            var alternative = decision.AddAlternative(
                "Second option",
                "Added after the decision was saved.",
                isBaseline: false,
                estimatedInitialCost: 1_000m,
                estimatedRecurringCost: 250m,
                expectedBenefits: null,
                knownRisks: null);

            alternativeId = alternative.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var stored = await db.DecisionAlternatives
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == alternativeId);

            stored.Should().NotBeNull();
            stored!.Name.Should().Be("Second option");
            stored.DecisionId.Should().Be(decisionId);
        }
    }

    [Fact]
    public async Task Adding_a_review_to_a_saved_decision_inserts_a_row()
    {
        Guid decisionId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Review later", DecisionCategory.Finance);
            db.Decisions.Add(decision);
            await db.SaveChangesAsync();
            decisionId = decision.Id;
        }

        Guid reviewId;

        await using (var db = _database.CreateContext())
        {
            var decision = await db.Decisions
                .Include(d => d.Reviews)
                .SingleAsync(d => d.Id == decisionId);

            var review = decision.AddReview(
                DateTime.UtcNow.AddMonths(6),
                "The outcome was better than expected.",
                "A modest improvement.",
                "Kept the budget.",
                "Underestimated the timeline.",
                "Add a buffer to every estimate.");

            reviewId = review.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var stored = await db.DecisionReviews
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == reviewId);

            stored.Should().NotBeNull();
            stored!.LessonsLearned.Should().Be("Add a buffer to every estimate.");
        }
    }

    [Fact]
    public async Task Adding_a_scenario_and_its_variable_to_a_saved_decision_inserts_rows()
    {
        Guid decisionId;
        Guid definitionId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Scenario later", DecisionCategory.Business);
            db.Decisions.Add(decision);

            var definition = new VariableDefinition(
                "monthly_revenue",
                "Monthly revenue",
                VariableCategory.Business,
                VariableDataType.Currency,
                "IRR");

            db.VariableDefinitions.Add(definition);
            await db.SaveChangesAsync();

            decisionId = decision.Id;
            definitionId = definition.Id;
        }

        Guid scenarioId;
        Guid variableId;

        await using (var db = _database.CreateContext())
        {
            var decision = await db.Decisions
                .Include(d => d.Scenarios)
                .SingleAsync(d => d.Id == decisionId);

            var scenario = decision.AddScenario("Year one", ScenarioType.Expected, 12);
            scenarioId = scenario.Id;

            var variable = scenario.SetVariable(
                definitionId,
                value: 50_000_000m,
                distributionType: DistributionType.Normal,
                minimumValue: null,
                maximumValue: null,
                standardDeviation: 5_000_000m,
                confidence: ConfidenceLevel.Medium,
                sourceType: SourceType.UserInput,
                sourceReference: null,
                distributionBasis: DistributionBasis.UserDefined,
                isUserOverride: false);

            variableId = variable.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var storedScenario = await db.Scenarios
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == scenarioId);

            storedScenario.Should().NotBeNull();
            storedScenario!.Name.Should().Be("Year one");

            var storedVariable = await db.ScenarioVariables
                .AsNoTracking()
                .SingleOrDefaultAsync(v => v.Id == variableId);

            storedVariable.Should().NotBeNull();
            storedVariable!.Value.Should().Be(50_000_000m);
            storedVariable.StandardDeviation.Should().Be(5_000_000m);
            storedVariable.DistributionType.Should().Be(DistributionType.Normal);
        }
    }

    [Fact]
    public async Task A_variable_definition_in_use_cannot_be_deleted()
    {
        Guid definitionId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("FK guard", DecisionCategory.Finance);
            var scenario = decision.AddScenario("Base", ScenarioType.Baseline, 12);

            var definition = new VariableDefinition(
                "fk_guard_income",
                "FK guard income",
                VariableCategory.Financial,
                VariableDataType.Currency,
                "IRR");

            db.Decisions.Add(decision);
            db.VariableDefinitions.Add(definition);
            await db.SaveChangesAsync();

            scenario.SetVariable(
                definition.Id,
                1_000m,
                DistributionType.Fixed,
                null,
                null,
                null,
                ConfidenceLevel.Medium,
                SourceType.UserInput,
                null,
                DistributionBasis.UserDefined,
                false);

            await db.SaveChangesAsync();
            definitionId = definition.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var definition = await db.VariableDefinitions.SingleAsync(v => v.Id == definitionId);
            db.VariableDefinitions.Remove(definition);

            // The foreign key is required, so SQL Server refuses to orphan the
            // assumption. This is why the engine can rely on every stored value
            // having a definition to bind to.
            var act = async () => await db.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Adding_a_run_to_a_saved_scenario_inserts_a_row()
    {
        Guid scenarioId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Run later", DecisionCategory.Investment);
            var scenario = decision.AddScenario("Base", ScenarioType.Baseline, 24);
            db.Decisions.Add(decision);
            await db.SaveChangesAsync();
            scenarioId = scenario.Id;
        }

        Guid runId;

        await using (var db = _database.CreateContext())
        {
            var scenario = await db.Scenarios
                .Include(s => s.Runs)
                .SingleAsync(s => s.Id == scenarioId);

            var run = scenario.RegisterRun(
                simulationModelId: Guid.Empty,
                modelVersion: "1.0.0",
                runType: SimulationRunType.MonteCarlo,
                iterationCount: 500,
                randomSeed: 12345,
                inputSnapshotJson: "{}");

            runId = run.Id;

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var stored = await db.SimulationRuns
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == runId);

            stored.Should().NotBeNull();
            stored!.IterationCount.Should().Be(500);
            stored.RandomSeed.Should().Be(12345);
            stored.ScenarioId.Should().Be(scenarioId);
        }
    }
}
