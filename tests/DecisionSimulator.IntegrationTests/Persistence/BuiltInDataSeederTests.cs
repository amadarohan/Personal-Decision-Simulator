using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Persistence;
using DecisionSimulator.Infrastructure.Simulation.Adapters;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// Verifies the built-in variable catalogue that makes the simulation models
/// reachable from the UI.
/// <para>
/// The catalogue is not cosmetic. A model declares its inputs by key, and the
/// user can only supply an assumption for a key that has a definition. If the
/// catalogue drifts from the models, every run fails with a missing input and
/// the failure surfaces far away from the cause. These tests pin the two
/// together.
/// </para>
/// </summary>
public sealed class BuiltInDataSeederTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public BuiltInDataSeederTests(SqlServerTestDatabase database) => _database = database;

    private static BuiltInDataSeeder NewSeeder(DecisionSimulatorDbContext db) =>
        new(db, NullLogger<BuiltInDataSeeder>.Instance);

    [Fact]
    public async Task Seeds_a_definition_for_every_input_the_models_require()
    {
        await using var db = _database.CreateContext();

        await NewSeeder(db).SeedAsync();

        var seededKeys = await db.VariableDefinitions
            .AsNoTracking()
            .Select(v => v.Key)
            .ToListAsync();

        var engine = new SimulationEngineAdapter();

        // The formula model's inputs are whatever its expression references, so
        // it is excluded: it is driven by a user-authored formula, not by the
        // built-in catalogue.
        var requiredKeys = engine.AvailableModels
            .Where(m => m.ModelType != ModelType.Formula)
            .SelectMany(m => m.RequiredInputs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        requiredKeys.Should().NotBeEmpty();

        seededKeys.Should().Contain(
            requiredKeys,
            "every model input must have a definition or the user cannot supply it");
    }

    [Fact]
    public async Task Marks_every_seeded_definition_as_built_in()
    {
        await using var db = _database.CreateContext();

        await NewSeeder(db).SeedAsync();

        var definitions = await db.VariableDefinitions.AsNoTracking().ToListAsync();

        definitions.Should().NotBeEmpty();
        definitions.Should().OnlyContain(d => d.IsBuiltIn);
    }

    [Fact]
    public async Task Is_idempotent_across_repeated_runs()
    {
        await using var db = _database.CreateContext();

        // The fixture shares one database across the class, so the first run may
        // legitimately insert nothing. The invariant under test is that a second
        // run never adds anything and never changes the total.
        await NewSeeder(db).SeedAsync();

        var countAfterFirst = await db.VariableDefinitions.AsNoTracking().CountAsync();
        countAfterFirst.Should().BeGreaterThan(0);

        var second = await NewSeeder(db).SeedAsync();

        second.Should().Be(0);

        var countAfterSecond = await db.VariableDefinitions.AsNoTracking().CountAsync();
        countAfterSecond.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task Does_not_overwrite_a_definition_the_user_edited()
    {
        await using (var db = _database.CreateContext())
        {
            await NewSeeder(db).SeedAsync();

            var definition = await db.VariableDefinitions
                .SingleAsync(v => v.Key == "monthly_income");

            definition.Update(
                "Household take-home pay",
                "Edited by the user.",
                VariableCategory.Financial,
                VariableDataType.Currency,
                "per month",
                defaultValue: null,
                minimumAllowedValue: 0m,
                maximumAllowedValue: null,
                isExternalDataCandidate: false,
                isSensitive: true);

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            await NewSeeder(db).SeedAsync();

            var definition = await db.VariableDefinitions
                .AsNoTracking()
                .SingleAsync(v => v.Key == "monthly_income");

            definition.Name.Should().Be("Household take-home pay");
            definition.Description.Should().Be("Edited by the user.");
        }
    }

    [Fact]
    public async Task Restores_a_built_in_definition_the_user_deleted()
    {
        await using (var db = _database.CreateContext())
        {
            await NewSeeder(db).SeedAsync();

            var definition = await db.VariableDefinitions
                .SingleAsync(v => v.Key == "starting_cash");

            db.VariableDefinitions.Remove(definition);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var inserted = await NewSeeder(db).SeedAsync();

            inserted.Should().Be(1);

            var restored = await db.VariableDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(v => v.Key == "starting_cash");

            restored.Should().NotBeNull();
            restored!.IsBuiltIn.Should().BeTrue();
        }
    }
}
