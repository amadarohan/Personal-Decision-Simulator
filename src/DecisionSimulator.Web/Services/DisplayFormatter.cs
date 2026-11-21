using System.Globalization;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Web.Services;

/// <summary>
/// Formats dates and money for display.
/// <para>
/// The Jalali calendar is supported through <see cref="PersianCalendar"/>, which
/// ships with .NET. Formatting is centralised here so that no page has to decide
/// which calendar to use, and so a future calendar can be added in one place.
/// </para>
/// </summary>
public sealed class DisplayFormatter
{
    private static readonly PersianCalendar Persian = new();

    /// <summary>
    /// Formats a UTC instant in the user's calendar and time zone.
    /// </summary>
    public string FormatDate(DateTime utc, CalendarSystem calendar, string timeZoneId)
    {
        var local = ToLocal(utc, timeZoneId);

        if (calendar == CalendarSystem.Jalali)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Persian.GetYear(local):0000}/{Persian.GetMonth(local):00}/{Persian.GetDayOfMonth(local):00}");
        }

        return local.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a UTC instant as a date and time in the user's calendar.
    /// </summary>
    public string FormatDateTime(DateTime utc, CalendarSystem calendar, string timeZoneId)
    {
        var local = ToLocal(utc, timeZoneId);
        var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);

        return $"{FormatDate(utc, calendar, timeZoneId)} {time}";
    }

    /// <summary>
    /// Formats an amount using the user's currency convention.
    /// <para>
    /// The unit convention is applied as a divisor, not as a rounding step, so no
    /// precision is invented. A value the user entered in Toman is displayed in
    /// Toman and never silently converted to Rial.
    /// </para>
    /// <para>
    /// <paramref name="unitLabel"/> lets the caller supply a translated unit
    /// name. The formatter is a singleton and has no access to the language, so
    /// the label is passed in rather than resolved here.
    /// </para>
    /// </summary>
    public string FormatMoney(
        decimal amount,
        string currencyCode,
        CurrencyUnitConvention convention,
        string? unitLabel = null)
    {
        var unit = unitLabel
            ?? (convention == CurrencyUnitConvention.Toman ? "Toman" : currencyCode);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{amount:N0} {unit}");
    }

    /// <summary>
    /// Formats a percentage that the simulation engine produced. The value is
    /// shown as-is; nothing here estimates or rounds away uncertainty.
    /// </summary>
    public string FormatPercent(decimal fraction) =>
        string.Create(CultureInfo.InvariantCulture, $"{fraction * 100m:N1}%");

    private static DateTime ToLocal(DateTime utc, string timeZoneId)
    {
        var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(unspecified, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return unspecified;
        }
        catch (InvalidTimeZoneException)
        {
            return unspecified;
        }
    }
}
