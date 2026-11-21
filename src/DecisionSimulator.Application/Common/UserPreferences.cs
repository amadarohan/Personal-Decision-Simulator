using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Common;

/// <summary>
/// The user's display preferences. Kept separate from the persisted
/// <c>UserProfile</c> entity so that services can format output without loading
/// the profile on every call.
/// </summary>
public sealed record UserPreferences(
    string PreferredLanguage,
    CalendarSystem CalendarSystem,
    string DefaultCurrencyCode,
    CurrencyUnitConvention CurrencyUnitConvention,
    string TimeZoneId)
{
    public static UserPreferences Default { get; } = new(
        "fa-IR",
        CalendarSystem.Jalali,
        "IRR",
        CurrencyUnitConvention.Toman,
        "Asia/Tehran");
}

/// <summary>
/// Supplies the active user preferences. Implemented in the web layer from the
/// persisted profile; the application layer only consumes it.
/// </summary>
public interface IUserPreferencesAccessor
{
    Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default);
}
