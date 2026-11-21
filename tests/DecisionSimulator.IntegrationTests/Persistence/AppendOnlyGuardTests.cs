using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// The append-only guard is the last line of defence for the audit trail. These
/// tests run against SQL Server because they exercise the real save pipeline,
/// including the change tracker's view of which properties were modified.
/// </summary>
public sealed class AppendOnlyGuardTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public AppendOnlyGuardTests(SqlServerTestDatabase database)
    {
        _database = database;
    }

    private async Task<(Guid SourceId, Guid IndicatorId, Guid ObservationId)> SeedObservationAsync()
    {
        await using var db = _database.CreateContext();

        // The fixture shares one database across the class, and DataSource.Name
        // is uniquely indexed, so each seed needs its own name.
        var suffix = Guid.NewGuid().ToString("N");

        var source = new DataSource($"Guard source {suffix}", DataProviderType.Manual, ReliabilityLevel.High);
        var indicator = new EconomicIndicator(
            "Guard indicator",
            $"GUARD.{suffix}",
            VariableCategory.Economic,
            "IR",
            GeographicScope.Country,
            "%");

        db.DataSources.Add(source);
        db.EconomicIndicators.Add(indicator);
        await db.SaveChangesAsync();

        var observation = new DataObservation(
            source.Id,
            10m,
            "%",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            indicatorId: indicator.Id);

        db.DataObservations.Add(observation);
        await db.SaveChangesAsync();

        return (source.Id, indicator.Id, observation.Id);
    }

    [Fact]
    public async Task An_observation_cannot_be_deleted()
    {
        var (_, _, observationId) = await SeedObservationAsync();

        await using var db = _database.CreateContext();
        var observation = await db.DataObservations.SingleAsync(o => o.Id == observationId);
        db.DataObservations.Remove(observation);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public async Task An_observed_value_cannot_be_rewritten()
    {
        var (_, _, observationId) = await SeedObservationAsync();

        await using var db = _database.CreateContext();
        var observation = await db.DataObservations.SingleAsync(o => o.Id == observationId);

        // Bypass the domain method to prove the guard, not the entity, is what
        // protects the recorded value.
        db.Entry(observation).Property(nameof(DataObservation.Value)).CurrentValue = 999m;

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Value*append-only*");
    }

    [Fact]
    public async Task An_observation_may_be_superseded()
    {
        var (_, _, observationId) = await SeedObservationAsync();

        await using (var db = _database.CreateContext())
        {
            var observation = await db.DataObservations.SingleAsync(o => o.Id == observationId);
            observation.Supersede(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var stored = await db.DataObservations.AsNoTracking().SingleAsync(o => o.Id == observationId);
            stored.ValidUntilUtc.Should().NotBeNull();
            stored.Value.Should().Be(10m);
        }
    }

    [Fact]
    public async Task An_observation_may_be_verified()
    {
        var (_, _, observationId) = await SeedObservationAsync();

        await using (var db = _database.CreateContext())
        {
            var observation = await db.DataObservations.SingleAsync(o => o.Id == observationId);
            observation.MarkVerified(VerificationStatus.CrossVerified, ConfidenceLevel.High);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var stored = await db.DataObservations.AsNoTracking().SingleAsync(o => o.Id == observationId);
            stored.VerificationStatus.Should().Be(VerificationStatus.CrossVerified);
            stored.Confidence.Should().Be(ConfidenceLevel.High);
        }
    }

    [Fact]
    public async Task A_review_can_be_annotated_but_not_reassigned()
    {
        Guid reviewId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Review guard", DecisionCategory.Career);
            db.Decisions.Add(decision);
            await db.SaveChangesAsync();

            var review = decision.AddReview(
                DateTime.UtcNow.AddDays(-1),
                "The role was more operational than expected.",
                "Expected a strategy role.",
                whatWentWell: "Good team.",
                whatWentWrong: null,
                lessonsLearned: null);

            await db.SaveChangesAsync();
            reviewId = review.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var review = await db.DecisionReviews.SingleAsync(r => r.Id == reviewId);
            review.Update(
                "The role was more operational than expected.",
                "Expected a strategy role.",
                "A clear gap between the job description and the work.",
                "Good team.",
                "Little strategic ownership.",
                "Ask for the last six months of actual work in the next interview.");

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var stored = await db.DecisionReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
            stored.LessonsLearned.Should().Contain("last six months");
            stored.OutcomeDifference.Should().Contain("gap");
        }
    }

    [Fact]
    public async Task A_review_cannot_be_moved_to_another_decision()
    {
        Guid reviewId;

        await using (var db = _database.CreateContext())
        {
            var decision = new Decision("Review guard 2", DecisionCategory.Career);
            db.Decisions.Add(decision);
            await db.SaveChangesAsync();

            var review = decision.AddReview(
                DateTime.UtcNow.AddDays(-1),
                "Outcome.",
                null,
                null,
                null,
                null);

            await db.SaveChangesAsync();
            reviewId = review.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var review = await db.DecisionReviews.SingleAsync(r => r.Id == reviewId);
            db.Entry(review).Property(nameof(DecisionReview.DecisionId)).CurrentValue = Guid.NewGuid();

            var act = async () => await db.SaveChangesAsync();

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*DecisionId*append-only*");
        }
    }
}
