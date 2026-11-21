using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// Covers the economic-context and AI tables. These are the ones most likely to
/// lose information silently: a <see cref="TimeSpan"/> stored as a SQL
/// <c>time</c> column would cap at 24 hours, and a decimal stored at the wrong
/// scale would round a rate without any error.
/// </summary>
public sealed class ContextAndAIPersistenceTests : IClassFixture<SqlServerTestDatabase>
{
    private readonly SqlServerTestDatabase _database;

    public ContextAndAIPersistenceTests(SqlServerTestDatabase database) => _database = database;

    [Fact]
    public async Task Freshness_window_round_trips_beyond_twenty_four_hours()
    {
        Guid indicatorId;

        await using (var db = _database.CreateContext())
        {
            var indicator = new EconomicIndicator(
                "Consumer price index",
                "CPI",
                VariableCategory.Economic,
                "IR",
                GeographicScope.Country,
                "index");

            // 90 days would overflow a SQL "time" column, which is why the
            // configuration stores ticks in a bigint.
            indicator.SetFreshnessWindow(TimeSpan.FromDays(90));

            db.EconomicIndicators.Add(indicator);
            await db.SaveChangesAsync();

            indicatorId = indicator.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var indicator = await db.EconomicIndicators.SingleAsync(i => i.Id == indicatorId);

            indicator.FreshnessWindow.Should().Be(TimeSpan.FromDays(90));
            indicator.IndicatorCode.Should().Be("CPI");
            indicator.CountryCode.Should().Be("IR");
            indicator.City.Should().BeNull();
        }
    }

    [Fact]
    public async Task Allows_one_city_less_indicator_per_code_and_country()
    {
        await using var db = _database.CreateContext();

        db.EconomicIndicators.Add(new EconomicIndicator(
            "Policy rate", "POLICY_RATE", VariableCategory.Economic, "IR", GeographicScope.Country, "%"));

        await db.SaveChangesAsync();

        // A second city-less row with the same code must be rejected: the
        // filtered unique index exists precisely because SQL Server treats NULLs
        // as equal in a unique index.
        db.EconomicIndicators.Add(new EconomicIndicator(
            "Policy rate duplicate", "POLICY_RATE", VariableCategory.Economic, "IR", GeographicScope.Country, "%"));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Allows_the_same_indicator_code_in_different_cities()
    {
        await using var db = _database.CreateContext();

        db.EconomicIndicators.Add(new EconomicIndicator(
            "Rent index", "RENT_INDEX", VariableCategory.Economic, "IR", GeographicScope.City, "index", city: "Tehran"));

        db.EconomicIndicators.Add(new EconomicIndicator(
            "Rent index", "RENT_INDEX", VariableCategory.Economic, "IR", GeographicScope.City, "index", city: "Isfahan"));

        await db.SaveChangesAsync();

        var count = await db.EconomicIndicators.CountAsync(i => i.IndicatorCode == "RENT_INDEX");
        count.Should().Be(2);
    }

    [Fact]
    public async Task Persists_an_observation_with_its_provenance()
    {
        Guid observationId;

        await using (var db = _database.CreateContext())
        {
            var source = new DataSource(
                "Manual entry",
                DataProviderType.Manual,
                ReliabilityLevel.Medium,
                description: "Values typed in by the user.");

            var indicator = new EconomicIndicator(
                "Inflation", "INFLATION", VariableCategory.Economic, "IR", GeographicScope.Country, "%");

            db.DataSources.Add(source);
            db.EconomicIndicators.Add(indicator);
            await db.SaveChangesAsync();

            var observation = new DataObservation(
                source.Id,
                value: 38.5m,
                unit: "%",
                observationDate: new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                retrievedAtUtc: DateTime.UtcNow,
                indicatorId: indicator.Id,
                verificationStatus: VerificationStatus.SingleSource,
                confidence: ConfidenceLevel.Medium,
                notes: "Central bank monthly bulletin.");

            db.DataObservations.Add(observation);
            await db.SaveChangesAsync();

            observationId = observation.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var observation = await db.DataObservations.SingleAsync(o => o.Id == observationId);

            observation.Value.Should().Be(38.5m);
            observation.Unit.Should().Be("%");
            observation.VerificationStatus.Should().Be(VerificationStatus.SingleSource);
            observation.Confidence.Should().Be(ConfidenceLevel.Medium);
            observation.Notes.Should().Be("Central bank monthly bulletin.");
            observation.VariableDefinitionId.Should().BeNull();
        }
    }

    [Fact]
    public async Task Persists_an_ai_proposal_and_its_approval_audit_trail()
    {
        Guid proposalId;

        await using (var db = _database.CreateContext())
        {
            var proposal = new AIProposal(
                AIProposalType.Scenario,
                "Add a stress scenario",
                "Proposes a scenario where income falls 30% for six months.",
                """{"scenarioType":"StressTest","timeHorizonMonths":12}""",
                evidenceJson: """{"basis":"user stated income volatility"}""",
                confidence: ConfidenceLevel.Low);

            db.AIProposals.Add(proposal);
            await db.SaveChangesAsync();

            proposalId = proposal.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var proposal = await db.AIProposals.SingleAsync(p => p.Id == proposalId);

            proposal.Status.Should().Be(AIProposalStatus.Pending);
            proposal.Confidence.Should().Be(ConfidenceLevel.Low);
            proposal.ApprovedAtUtc.Should().BeNull();

            proposal.Approve();
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var proposal = await db.AIProposals.SingleAsync(p => p.Id == proposalId);

            proposal.Status.Should().Be(AIProposalStatus.Approved);
            proposal.ApprovedAtUtc.Should().NotBeNull();

            db.AIExecutionLogs.Add(new AIExecutionLog(
                proposal.Id,
                "Scenario.Create",
                """{"name":"Income shock"}""",
                """{"scenarioId":"00000000-0000-0000-0000-000000000001"}""",
                AIExecutionStatus.Succeeded));

            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var log = await db.AIExecutionLogs.SingleAsync();
            log.Operation.Should().Be("Scenario.Create");
            log.Status.Should().Be(AIExecutionStatus.Succeeded);
        }
    }

    [Fact]
    public async Task Preserves_six_decimal_places_on_rates()
    {
        Guid scenarioId;

        await using (var db = _database.CreateContext())
        {
            var definition = new VariableDefinition(
                "monthly_growth_rate",
                "Monthly growth rate",
                VariableCategory.Financial,
                VariableDataType.Percentage,
                "%");

            db.VariableDefinitions.Add(definition);

            var decision = new Decision("Rate precision", DecisionCategory.Investment);
            var scenario = decision.AddScenario("Baseline", ScenarioType.Baseline, 60);

            // A rate rounded to two decimals would visibly distort a five-year
            // projection, so the column must keep six.
            scenario.SetVariable(
                definition.Id,
                0.012345m,
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

            scenarioId = scenario.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var variable = await db.ScenarioVariables.SingleAsync(v => v.ScenarioId == scenarioId);
            variable.Value.Should().Be(0.012345m);
        }
    }

    [Fact]
    public async Task Variable_definition_key_is_unique()
    {
        await using var db = _database.CreateContext();

        db.VariableDefinitions.Add(new VariableDefinition(
            "monthly_income", "Monthly income", VariableCategory.Financial, VariableDataType.Currency, "IRR"));

        await db.SaveChangesAsync();

        db.VariableDefinitions.Add(new VariableDefinition(
            "monthly_income", "Duplicate key", VariableCategory.Financial, VariableDataType.Currency, "IRR"));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
