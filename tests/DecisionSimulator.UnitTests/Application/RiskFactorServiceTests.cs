using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Risks;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class RiskFactorServiceTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();
    private readonly FixedClock _clock = new(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly RiskFactorService _service;

    public RiskFactorServiceTests()
    {
        _service = new RiskFactorService(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static CreateRiskFactorRequest ValidRequest(string name = "Currency devaluation") =>
        new(
            name,
            RiskCategory.Economic,
            GeographicScope.Country,
            ImpactDirection.Negative,
            Description: "A sharp fall in the value of the rial.",
            ImpactMagnitude: "Could remove a large share of real purchasing power.",
            LikelihoodAssessment: ConfidenceLevel.Medium,
            Confidence: ConfidenceLevel.Low,
            SourceReference: "https://example.org/report");

    [Fact]
    public async Task Creates_a_risk_with_qualitative_assessments()
    {
        var result = await _service.CreateAsync(ValidRequest());

        result.Succeeded.Should().BeTrue();

        var stored = await _db.RiskFactors.FindAsync(result.Value);
        stored!.Name.Should().Be("Currency devaluation");
        stored.LikelihoodAssessment.Should().Be(ConfidenceLevel.Medium);
        stored.Confidence.Should().Be(ConfidenceLevel.Low);
        stored.IsActive.Should().BeTrue();
        stored.LastVerifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Rejects_a_risk_with_no_name()
    {
        var result = await _service.CreateAsync(ValidRequest("   "));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Trims_the_name()
    {
        var result = await _service.CreateAsync(ValidRequest("  Currency devaluation  "));

        var stored = await _db.RiskFactors.FindAsync(result.Value);
        stored!.Name.Should().Be("Currency devaluation");
    }

    [Fact]
    public async Task Lists_only_active_risks_by_default()
    {
        var active = await _service.CreateAsync(ValidRequest("Active risk"));
        var inactive = await _service.CreateAsync(ValidRequest("Inactive risk"));

        await _service.SetActiveAsync(inactive.Value, false);

        var result = await _service.ListAsync();

        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(active.Value);
    }

    [Fact]
    public async Task Includes_inactive_risks_when_asked()
    {
        await _service.CreateAsync(ValidRequest("Active risk"));
        var inactive = await _service.CreateAsync(ValidRequest("Inactive risk"));
        await _service.SetActiveAsync(inactive.Value, false);

        var result = await _service.ListAsync(includeInactive: true);

        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Filters_by_category()
    {
        await _service.CreateAsync(ValidRequest("Economic risk"));

        await _service.CreateAsync(new CreateRiskFactorRequest(
            "Regulatory risk",
            RiskCategory.Regulatory,
            GeographicScope.Country,
            ImpactDirection.Uncertain));

        var result = await _service.ListAsync(category: RiskCategory.Regulatory);

        result.Value.Should().ContainSingle();
        result.Value[0].Name.Should().Be("Regulatory risk");
    }

    [Fact]
    public async Task Marking_verified_records_the_clock_time()
    {
        var created = await _service.CreateAsync(ValidRequest());

        var result = await _service.MarkVerifiedAsync(created.Value);

        result.Succeeded.Should().BeTrue();

        var stored = await _db.RiskFactors.FindAsync(created.Value);
        stored!.LastVerifiedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task Updates_the_assessment()
    {
        var created = await _service.CreateAsync(ValidRequest());

        var result = await _service.UpdateAsync(
            created.Value,
            new UpdateRiskFactorRequest(
                "Currency devaluation",
                "Updated description",
                RiskCategory.Economic,
                GeographicScope.Region,
                ImpactDirection.Mixed,
                "Could cut import capacity.",
                ConfidenceLevel.High,
                ConfidenceLevel.Medium,
                "https://example.org/updated"));

        result.Succeeded.Should().BeTrue();

        var stored = await _db.RiskFactors.FindAsync(created.Value);
        stored!.GeographicScope.Should().Be(GeographicScope.Region);
        stored.ImpactDirection.Should().Be(ImpactDirection.Mixed);
        stored.LikelihoodAssessment.Should().Be(ConfidenceLevel.High);
        stored.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task Returns_not_found_for_an_unknown_risk()
    {
        var result = await _service.GetAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Deletes_a_risk()
    {
        var created = await _service.CreateAsync(ValidRequest());

        var result = await _service.DeleteAsync(created.Value);

        result.Succeeded.Should().BeTrue();
        (await _db.RiskFactors.FindAsync(created.Value)).Should().BeNull();
    }
}
