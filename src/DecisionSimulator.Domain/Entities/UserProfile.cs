using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.ValueObjects;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// The single local user profile. The architecture allows more than one row so
/// that relocation comparisons and future multi-profile use do not require a
/// schema change, but the application treats one row as active.
/// </summary>
public class UserProfile : AuditableEntity, IAggregateRoot
{
    private UserProfile()
    {
    }

    public UserProfile(
        string displayName,
        GeoLocation location,
        string preferredLanguage,
        CalendarSystem calendarSystem,
        string defaultCurrencyCode,
        CurrencyUnitConvention currencyUnitConvention)
    {
        DisplayName = displayName;
        CountryCode = location.CountryCode;
        CountryName = location.CountryName;
        City = location.City;
        TimeZoneId = location.TimeZoneId;
        PreferredLanguage = preferredLanguage;
        CalendarSystem = calendarSystem;
        DefaultCurrencyCode = defaultCurrencyCode;
        CurrencyUnitConvention = currencyUnitConvention;
    }

    public string DisplayName { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = "IR";

    public string CountryName { get; private set; } = "Iran";

    public string? City { get; private set; } = "Tehran";

    public string TimeZoneId { get; private set; } = "Asia/Tehran";

    public string PreferredLanguage { get; private set; } = "fa-IR";

    public CalendarSystem CalendarSystem { get; private set; } = CalendarSystem.Jalali;

    public string DefaultCurrencyCode { get; private set; } = "IRR";

    public CurrencyUnitConvention CurrencyUnitConvention { get; private set; } = CurrencyUnitConvention.Toman;

    /// <summary>True once the first-run wizard has been completed.</summary>
    public bool IsOnboardingCompleted { get; private set; }

    public GeoLocation Location => new(CountryCode, CountryName, City, TimeZoneId);

    public void UpdateLocation(GeoLocation location)
    {
        CountryCode = location.CountryCode;
        CountryName = location.CountryName;
        City = location.City;
        TimeZoneId = location.TimeZoneId;
        Touch();
    }

    public void UpdatePreferences(
        string displayName,
        string preferredLanguage,
        CalendarSystem calendarSystem,
        string defaultCurrencyCode,
        CurrencyUnitConvention currencyUnitConvention)
    {
        DisplayName = displayName;
        PreferredLanguage = preferredLanguage;
        CalendarSystem = calendarSystem;
        DefaultCurrencyCode = defaultCurrencyCode;
        CurrencyUnitConvention = currencyUnitConvention;
        Touch();
    }

    public void CompleteOnboarding()
    {
        IsOnboardingCompleted = true;
        Touch();
    }
}
