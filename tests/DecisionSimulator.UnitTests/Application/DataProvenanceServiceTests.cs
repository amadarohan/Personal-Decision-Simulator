using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Data;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class DataProvenanceServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryDatabase _db = new();
    private readonly FixedClock _clock = new(Now);
    private readonly DataProvenanceService _service;

    public DataProvenanceServiceTests()
    {
        _service = new DataProvenanceService(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> CreateSourceAsync() =>
        (await _service.CreateSourceAsync(new CreateDataSourceRequest(
            "Central bank bulletin",
            DataProviderType.Manual,
            ReliabilityLevel.Authoritative,
            BaseUrl: "https://example.org/bulletin",
            UpdateFrequency: "monthly"))).Value;

    private async Task<Guid> CreateIndicatorAsync(TimeSpan? window = null) =>
        (await _service.CreateIndicatorAsync(new CreateEconomicIndicatorRequest(
            "CPI inflation",
            "IR.CPI.YOY",
            VariableCategory.Economic,
            "IR",
            GeographicScope.Country,
            "%",
            FreshnessWindow: window))).Value;

    // ---------------------------------------------------------------- sources

    [Fact]
    public async Task Creates_a_source()
    {
        var result = await _service.CreateSourceAsync(new CreateDataSourceRequest(
            "Central bank bulletin",
            DataProviderType.Manual,
            ReliabilityLevel.Authoritative,
            BaseUrl: "https://example.org/bulletin"));

        result.Succeeded.Should().BeTrue();

        var stored = await _db.DataSources.FindAsync(result.Value);
        stored!.Name.Should().Be("Central bank bulletin");
        stored.IsActive.Should().BeTrue();
        stored.RequiresApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_a_source_with_no_name()
    {
        var result = await _service.CreateSourceAsync(new CreateDataSourceRequest(
            "  ",
            DataProviderType.Manual,
            ReliabilityLevel.Unknown));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Lists_only_active_sources_by_default()
    {
        var active = await CreateSourceAsync();

        var inactive = await _service.CreateSourceAsync(new CreateDataSourceRequest(
            "Retired feed",
            DataProviderType.RestApi,
            ReliabilityLevel.Low));

        await _service.SetSourceActiveAsync(inactive.Value, false);

        var result = await _service.ListSourcesAsync();

        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(active);
    }

    // ------------------------------------------------------------- indicators

    [Fact]
    public async Task Creates_an_indicator_with_no_value()
    {
        var result = await _service.CreateIndicatorAsync(new CreateEconomicIndicatorRequest(
            "CPI inflation",
            "IR.CPI.YOY",
            VariableCategory.Economic,
            "ir",
            GeographicScope.Country,
            "%"));

        result.Succeeded.Should().BeTrue();

        var stored = await _db.EconomicIndicators.FindAsync(result.Value);
        stored!.CountryCode.Should().Be("IR");
        stored.CurrentValue.Should().BeNull();
        stored.FreshnessWindow.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public async Task Rejects_a_duplicate_indicator_code_for_the_same_country()
    {
        await CreateIndicatorAsync();

        var result = await _service.CreateIndicatorAsync(new CreateEconomicIndicatorRequest(
            "CPI inflation (duplicate)",
            "IR.CPI.YOY",
            VariableCategory.Economic,
            "IR",
            GeographicScope.Country,
            "%"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Allows_the_same_indicator_code_in_another_country()
    {
        await CreateIndicatorAsync();

        var result = await _service.CreateIndicatorAsync(new CreateEconomicIndicatorRequest(
            "CPI inflation",
            "IR.CPI.YOY",
            VariableCategory.Economic,
            "TR",
            GeographicScope.Country,
            "%"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_a_non_positive_freshness_window()
    {
        var result = await _service.CreateIndicatorAsync(new CreateEconomicIndicatorRequest(
            "Exchange rate",
            "IR.USD.MID",
            VariableCategory.Economic,
            "IR",
            GeographicScope.Country,
            "IRR",
            FreshnessWindow: TimeSpan.Zero));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Reports_an_indicator_with_no_observation_as_unknown_freshness()
    {
        var id = await CreateIndicatorAsync();

        var result = await _service.GetIndicatorAsync(id);

        result.Value.Freshness.Should().Be(DataFreshness.Unknown);
        result.Value.CurrentValue.Should().BeNull();
    }

    // ----------------------------------------------------------- observations

    [Fact]
    public async Task Recording_an_observation_updates_the_cached_value()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync();

        var result = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId,
            42.5m,
            "%",
            Now.AddDays(-5),
            IndicatorId: indicatorId,
            VerificationStatus: VerificationStatus.SingleSource,
            Confidence: ConfidenceLevel.Medium));

        result.Succeeded.Should().BeTrue();

        var indicator = await _service.GetIndicatorAsync(indicatorId);
        indicator.Value.CurrentValue.Should().Be(42.5m);
        indicator.Value.ObservationDate.Should().Be(Now.AddDays(-5));
        indicator.Value.RetrievedAtUtc.Should().Be(Now);
        indicator.Value.SourceId.Should().Be(sourceId);
        indicator.Value.IsVerified.Should().BeTrue();
        indicator.Value.Freshness.Should().Be(DataFreshness.Fresh);
    }

    [Fact]
    public async Task A_second_observation_keeps_the_previous_value()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync();

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 40m, "%", Now.AddDays(-40), IndicatorId: indicatorId));

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now.AddDays(-5), IndicatorId: indicatorId));

        var indicator = await _service.GetIndicatorAsync(indicatorId);
        indicator.Value.CurrentValue.Should().Be(42.5m);
        indicator.Value.PreviousValue.Should().Be(40m);
    }

    [Fact]
    public async Task An_older_observation_does_not_overwrite_the_cached_value()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync();

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now.AddDays(-5), IndicatorId: indicatorId));

        // A backfilled value is still real data and is stored, but it must not
        // become the "current" value.
        var backfill = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 38m, "%", Now.AddDays(-60), IndicatorId: indicatorId));

        backfill.Succeeded.Should().BeTrue();

        var indicator = await _service.GetIndicatorAsync(indicatorId);
        indicator.Value.CurrentValue.Should().Be(42.5m);
        indicator.Value.ObservationDate.Should().Be(Now.AddDays(-5));
    }

    [Fact]
    public async Task Superseding_closes_the_previous_observations_window()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync();

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 40m, "%", Now.AddDays(-40), IndicatorId: indicatorId));

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now.AddDays(-5), IndicatorId: indicatorId));

        var observations = await _service.ListObservationsAsync(indicatorId: indicatorId);

        observations.Value.Should().HaveCount(2);

        var current = observations.Value.Single(o => o.IsCurrent);
        current.Value.Should().Be(42.5m);

        var superseded = observations.Value.Single(o => !o.IsCurrent);
        superseded.Value.Should().Be(40m);
        superseded.ValidUntilUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Freshness_degrades_as_the_clock_advances()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync(TimeSpan.FromDays(10));

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now, IndicatorId: indicatorId));

        (await _service.GetIndicatorAsync(indicatorId)).Value.Freshness
            .Should().Be(DataFreshness.Fresh);

        _clock.UtcNow = Now.AddDays(15);
        (await _service.GetIndicatorAsync(indicatorId)).Value.Freshness
            .Should().Be(DataFreshness.Aging);

        _clock.UtcNow = Now.AddDays(25);
        (await _service.GetIndicatorAsync(indicatorId)).Value.Freshness
            .Should().Be(DataFreshness.Stale);
    }

    [Fact]
    public async Task Rejects_an_observation_with_no_target()
    {
        var sourceId = await CreateSourceAsync();

        var result = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Rejects_an_observation_from_an_unknown_source()
    {
        var indicatorId = await CreateIndicatorAsync();

        var result = await _service.RecordObservationAsync(new RecordObservationRequest(
            Guid.NewGuid(), 42.5m, "%", Now, IndicatorId: indicatorId));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Rejects_an_observation_for_an_unknown_indicator()
    {
        var sourceId = await CreateSourceAsync();

        var result = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now, IndicatorId: Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Records_an_observation_against_a_variable_definition()
    {
        var sourceId = await CreateSourceAsync();

        var definition = new VariableDefinition(
            "market_rent",
            "Market rent",
            VariableCategory.Geographic,
            VariableDataType.Currency,
            "IRR/month");

        _db.VariableDefinitions.Add(definition);
        await _db.SaveChangesAsync(CancellationToken.None);

        var result = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId,
            25_000_000m,
            "IRR/month",
            Now.AddDays(-1),
            VariableDefinitionId: definition.Id));

        result.Succeeded.Should().BeTrue();

        var observations = await _service.ListObservationsAsync(variableDefinitionId: definition.Id);
        observations.Value.Should().ContainSingle();
        observations.Value[0].Value.Should().Be(25_000_000m);
    }

    [Fact]
    public async Task Listing_observations_requires_a_target()
    {
        var result = await _service.ListObservationsAsync();

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Verifying_the_current_observation_updates_the_indicator()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync();

        var recorded = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now.AddDays(-5), IndicatorId: indicatorId));

        var result = await _service.VerifyObservationAsync(
            recorded.Value,
            VerificationStatus.CrossVerified,
            ConfidenceLevel.High);

        result.Succeeded.Should().BeTrue();

        var indicator = await _service.GetIndicatorAsync(indicatorId);
        indicator.Value.IsVerified.Should().BeTrue();
        indicator.Value.Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public async Task Verifying_a_superseded_observation_does_not_relabel_the_current_value()
    {
        var sourceId = await CreateSourceAsync();
        var indicatorId = await CreateIndicatorAsync();

        var old = await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 40m, "%", Now.AddDays(-40), IndicatorId: indicatorId));

        await _service.RecordObservationAsync(new RecordObservationRequest(
            sourceId, 42.5m, "%", Now.AddDays(-5), IndicatorId: indicatorId));

        await _service.VerifyObservationAsync(
            old.Value,
            VerificationStatus.Rejected,
            ConfidenceLevel.Low);

        var indicator = await _service.GetIndicatorAsync(indicatorId);
        indicator.Value.CurrentValue.Should().Be(42.5m);
        indicator.Value.Confidence.Should().Be(ConfidenceLevel.Unknown);
    }
}
