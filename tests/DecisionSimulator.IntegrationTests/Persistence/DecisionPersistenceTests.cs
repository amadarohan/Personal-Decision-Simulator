using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.ValueObjects;
using DecisionSimulator.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// Proves that the EF Core model round-trips the real domain aggregates: private
/// setters, backing-field collections, value conversions, and concurrency tokens.
/// A mapping that compiles but loses data is worse than one that fails loudly.
/// </summary>
public sealed class DecisionPersistenceTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public DecisionPersistenceTests(SqlServerTestDatabase database) => _database = database;

    [Fact]
    public async Task Persists_and_reloads_a_decision_with_its_children()
    {
        var decisionId = Guid.NewGuid();

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision(
                "Move to a smaller city",
                DecisionCategory.Relocation,
                "Consider cost of living against career options.",
                ImportanceLevel.High,
                DecisionHorizon.LongTerm,
                RiskTolerance.Low);

            decision.AddAlternative("Stay in the capital", "Keep the current job.", isBaseline: true, 0m, 45_000_000m, "Stability", "Rent inflation");
            decision.AddAlternative("Relocate", "Move and rent for a year.", isBaseline: false, 120_000_000m, 22_000_000m, "Lower costs", "Loss of network");

            var scenario = decision.AddScenario("Baseline 12 months", ScenarioType.Baseline, 12);
            scenario.MarkBaseline(true);

            decision.AddReview(DateTime.UtcNow.AddMonths(12), "Pending", "Costs fall by 20%", null, null, null);

            db.Decisions.Add(decision);
            await db.SaveChangesAsync();

            decisionId = decision.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var loaded = await db.Decisions
                .Include(d => d.Alternatives)
                .Include(d => d.Scenarios)
                .Include(d => d.Reviews)
                .SingleAsync(d => d.Id == decisionId);

            loaded.Title.Should().Be("Move to a smaller city");
            loaded.Category.Should().Be(DecisionCategory.Relocation);
            loaded.Importance.Should().Be(ImportanceLevel.High);
            loaded.RiskTolerance.Should().Be(RiskTolerance.Low);
            loaded.Alternatives.Should().HaveCount(2);
            loaded.Alternatives.Single(a => a.IsBaseline).Name.Should().Be("Stay in the capital");
            loaded.Alternatives.Single(a => a.IsBaseline).EstimatedRecurringCost.Should().Be(45_000_000m);
            loaded.Scenarios.Should().ContainSingle().Which.IsBaseline.Should().BeTrue();
            loaded.Reviews.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task RowVersion_changes_on_update_and_detects_a_stale_write()
    {
        Guid decisionId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Concurrency probe", DecisionCategory.Finance);
            db.Decisions.Add(decision);
            await db.SaveChangesAsync();
            decisionId = decision.Id;
        }

        byte[] firstVersion;

        await using (var db = _database.CreateContext())
        {
            var decision = await db.Decisions.SingleAsync(d => d.Id == decisionId);
            firstVersion = decision.RowVersion.ToArray();
            firstVersion.Should().NotBeEmpty();

            decision.UpdateDetails(
                "Concurrency probe updated",
                null,
                DecisionCategory.Finance,
                ImportanceLevel.Moderate,
                DecisionHorizon.MediumTerm,
                RiskTolerance.Moderate,
                null,
                null,
                null,
                null);

            await db.SaveChangesAsync();

            decision.RowVersion.Should().NotEqual(firstVersion);
        }

        // Two contexts load the same row, one saves, the other must fail.
        await using var stale = _database.CreateContext();
        await using var fresh = _database.CreateContext();

        var staleCopy = await stale.Decisions.SingleAsync(d => d.Id == decisionId);
        var freshCopy = await fresh.Decisions.SingleAsync(d => d.Id == decisionId);

        freshCopy.UpdateDetails(
            "Written by the fresh context",
            null,
            DecisionCategory.Finance,
            ImportanceLevel.Moderate,
            DecisionHorizon.MediumTerm,
            RiskTolerance.Moderate,
            null,
            null,
            null,
            null);

        await fresh.SaveChangesAsync();

        staleCopy.UpdateDetails(
            "Written by the stale context",
            null,
            DecisionCategory.Finance,
            ImportanceLevel.Moderate,
            DecisionHorizon.MediumTerm,
            RiskTolerance.Moderate,
            null,
            null,
            null,
            null);

        var act = async () => await stale.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Rejects_a_second_baseline_alternative_for_the_same_decision()
    {
        await using var db = _database.CreateContext();

        var decision = new Decision("Baseline uniqueness", DecisionCategory.Business);
        decision.AddAlternative("First", null, isBaseline: true, null, null, null, null);
        decision.AddAlternative("Second", null, isBaseline: false, null, null, null, null);

        db.Decisions.Add(decision);
        await db.SaveChangesAsync();

        // Bypass the domain guard to prove the database itself enforces it.
        // ExecuteSqlRawAsync surfaces the provider exception directly; only
        // SaveChanges wraps failures in DbUpdateException.
        var second = decision.Alternatives.Single(a => !a.IsBaseline);

        var act = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE DecisionAlternatives SET IsBaseline = 1 WHERE Id = {0}", second.Id);

        await act.Should().ThrowAsync<SqlException>()
            .Where(ex => ex.Message.Contains("IX_DecisionAlternatives_OneBaselinePerDecision"));
    }

    [Fact]
    public async Task Persists_user_profile_location_and_preferences()
    {
        await using (var db = _database.CreateContext())
        {
            var profile = new UserProfile(
                "Local user",
                new GeoLocation("IR", "Iran", "Tehran", "Asia/Tehran"),
                "fa-IR",
                CalendarSystem.Jalali,
                "IRR",
                CurrencyUnitConvention.Toman);

            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var profile = await db.UserProfiles.SingleAsync();

            profile.DisplayName.Should().Be("Local user");
            profile.CountryCode.Should().Be("IR");
            profile.City.Should().Be("Tehran");
            profile.CalendarSystem.Should().Be(CalendarSystem.Jalali);
            profile.CurrencyUnitConvention.Should().Be(CurrencyUnitConvention.Toman);
            profile.Location.TimeZoneId.Should().Be("Asia/Tehran");
        }
    }
}
