using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Settings;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly InMemoryDatabase _database = new();
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _service = new SettingsService(_database);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_setting_can_be_created_and_read_back()
    {
        var created = await _service.SetAsync(
            SettingKeys.AIProvider,
            new SetSettingRequest("ollama"));

        created.Succeeded.Should().BeTrue();
        created.Value.Key.Should().Be(SettingKeys.AIProvider);
        created.Value.Value.Should().Be("ollama");

        var fetched = await _service.GetAsync(SettingKeys.AIProvider);

        fetched.Succeeded.Should().BeTrue();
        fetched.Value.Value.Should().Be("ollama");
    }

    [Fact]
    public async Task Setting_the_same_key_twice_updates_rather_than_duplicates()
    {
        await _service.SetAsync(SettingKeys.AIModel, new SetSettingRequest("llama3"));
        await _service.SetAsync(SettingKeys.AIModel, new SetSettingRequest("mistral"));

        var all = await _service.ListAsync();

        all.Value.Should().ContainSingle(s => s.Key == SettingKeys.AIModel);
        all.Value.Single(s => s.Key == SettingKeys.AIModel).Value.Should().Be("mistral");
    }

    [Fact]
    public async Task A_secret_cannot_be_stored_in_the_database()
    {
        // A key in the database would be copied into every backup file.
        var result = await _service.SetAsync("ai.apiKey", new SetSettingRequest("sk-secret"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Forbidden);
        result.ErrorMessage.Should().Contain("backups");

        var all = await _service.ListAsync();
        all.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task A_blank_key_is_rejected()
    {
        var result = await _service.SetAsync("   ", new SetSettingRequest("value"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task An_unknown_setting_reports_not_found()
    {
        var result = await _service.GetAsync("does.not.exist");

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task A_setting_can_be_deleted()
    {
        await _service.SetAsync(SettingKeys.CalendarSystem, new SetSettingRequest("Jalali"));

        var deleted = await _service.DeleteAsync(SettingKeys.CalendarSystem);

        deleted.Succeeded.Should().BeTrue();

        var fetched = await _service.GetAsync(SettingKeys.CalendarSystem);
        fetched.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_an_unknown_setting_reports_not_found()
    {
        var result = await _service.DeleteAsync("does.not.exist");

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task The_profile_is_absent_before_onboarding()
    {
        var result = await _service.GetProfileAsync();

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Saving_a_profile_creates_it_once()
    {
        var request = new UpdateUserProfileRequest(
            "Sample User",
            "IR",
            "Iran",
            "Tehran",
            "Asia/Tehran",
            "fa-IR",
            CalendarSystem.Jalali,
            "IRR",
            CurrencyUnitConvention.Toman);

        var first = await _service.UpdateProfileAsync(request);
        var second = await _service.UpdateProfileAsync(request);

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeTrue();
        second.Value.Id.Should().Be(first.Value.Id);
        second.Value.CalendarSystem.Should().Be(CalendarSystem.Jalali);
        second.Value.CurrencyUnitConvention.Should().Be(CurrencyUnitConvention.Toman);
    }

    [Fact]
    public async Task A_profile_without_a_display_name_is_rejected()
    {
        var request = new UpdateUserProfileRequest(
            "  ",
            "IR",
            "Iran",
            "Tehran",
            "Asia/Tehran",
            "fa-IR",
            CalendarSystem.Jalali,
            "IRR",
            CurrencyUnitConvention.Toman);

        var result = await _service.UpdateProfileAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Onboarding_cannot_complete_without_a_profile()
    {
        var result = await _service.CompleteOnboardingAsync();

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Onboarding_marks_the_profile_complete()
    {
        await _service.UpdateProfileAsync(new UpdateUserProfileRequest(
            "Sample User",
            "IR",
            "Iran",
            "Tehran",
            "Asia/Tehran",
            "fa-IR",
            CalendarSystem.Jalali,
            "IRR",
            CurrencyUnitConvention.Toman));

        var completed = await _service.CompleteOnboardingAsync();
        var profile = await _service.GetProfileAsync();

        completed.Succeeded.Should().BeTrue();
        profile.Value.IsOnboardingComplete.Should().BeTrue();
    }

    [Fact]
    public async Task The_country_code_is_normalised_to_upper_case()
    {
        var result = await _service.UpdateProfileAsync(new UpdateUserProfileRequest(
            "Sample User",
            "de",
            "Germany",
            "Berlin",
            "Europe/Berlin",
            "de-DE",
            CalendarSystem.Gregorian,
            "eur",
            CurrencyUnitConvention.BaseUnit));

        result.Value.CountryCode.Should().Be("DE");
        result.Value.DefaultCurrencyCode.Should().Be("EUR");
    }
}
