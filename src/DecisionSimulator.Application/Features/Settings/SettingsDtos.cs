using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Settings;

public sealed record AppSettingDto(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    DateTime UpdatedAtUtc);

public sealed record SetSettingRequest(string Value, string? Description = null);

/// <summary>
/// The runtime-editable settings the application exposes. Keys are constants so
/// a typo cannot silently create a second, ignored setting.
/// </summary>
public static class SettingKeys
{
    public const string AIProvider = "ai.provider";
    public const string AIBaseUrl = "ai.baseUrl";
    public const string AIModel = "ai.model";
    public const string AIAllowRemote = "ai.allowRemote";
    public const string AIMaxRemoteSensitivity = "ai.maxRemoteSensitivity";
    public const string DefaultCurrencyCode = "ui.defaultCurrencyCode";
    public const string CalendarSystem = "ui.calendarSystem";
    public const string CurrencyUnitConvention = "ui.currencyUnitConvention";
    public const string PreferredLanguage = "ui.preferredLanguage";

    /// <summary>
    /// Settings that must never be stored in the database because they are
    /// secrets. The API key is supplied through configuration or an environment
    /// variable instead.
    /// </summary>
    public static readonly IReadOnlySet<string> SecretKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ai.apiKey",
            "ai.apikey",
            "ai.key",
            "ai.token"
        };
}

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string CountryCode,
    string CountryName,
    string City,
    string TimeZoneId,
    string PreferredLanguage,
    CalendarSystem CalendarSystem,
    string DefaultCurrencyCode,
    CurrencyUnitConvention CurrencyUnitConvention,
    bool IsOnboardingComplete);

public sealed record UpdateUserProfileRequest(
    string DisplayName,
    string CountryCode,
    string CountryName,
    string City,
    string TimeZoneId,
    string PreferredLanguage,
    CalendarSystem CalendarSystem,
    string DefaultCurrencyCode,
    CurrencyUnitConvention CurrencyUnitConvention);
