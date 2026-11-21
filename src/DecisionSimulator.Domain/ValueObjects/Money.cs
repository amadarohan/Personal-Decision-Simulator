namespace DecisionSimulator.Domain.ValueObjects;

/// <summary>
/// A monetary amount in a specific currency. Amounts are always stored in the
/// currency's base unit (IRR, not Toman) so that arithmetic is unambiguous.
/// </summary>
public readonly record struct Money(decimal Amount, string CurrencyCode)
{
    public static Money Zero(string currencyCode) => new(0m, currencyCode);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, CurrencyCode);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, CurrencyCode);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, CurrencyCode);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(CurrencyCode, other.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in {CurrencyCode} and {other.CurrencyCode} without an explicit exchange rate.");
        }
    }

    public override string ToString() => $"{Amount:N0} {CurrencyCode}";
}

/// <summary>
/// A percentage expressed as a fraction (0.12 = 12%). Stored as a fraction to
/// avoid the classic "is this 12 or 0.12" ambiguity in formulas.
/// </summary>
public readonly record struct Rate(decimal Fraction)
{
    public static Rate FromPercent(decimal percent) => new(percent / 100m);

    public decimal AsPercent => Fraction * 100m;

    public static Rate Zero => new(0m);

    public override string ToString() => $"{AsPercent:0.####}%";
}

/// <summary>
/// A closed numeric interval used for variable bounds and sensitivity ranges.
/// </summary>
public readonly record struct NumericRange(decimal Minimum, decimal Maximum)
{
    public bool IsValid => Maximum >= Minimum;

    public decimal Clamp(decimal value) => Math.Min(Math.Max(value, Minimum), Maximum);

    public decimal Span => Maximum - Minimum;

    // CA1720 flags "Single" as a type name. Here it means "a range containing one
    // value", which is the clearest name for the concept.
#pragma warning disable CA1720
    public static NumericRange Single(decimal value) => new(value, value);
#pragma warning restore CA1720
}

/// <summary>
/// Identifies a geographic context. Country and city are separate so that
/// relocation scenarios can compare two locations without duplicating logic.
/// </summary>
public readonly record struct GeoLocation(
    string CountryCode,
    string CountryName,
    string? City,
    string TimeZoneId)
{
    public string DisplayName => string.IsNullOrWhiteSpace(City)
        ? CountryName
        : $"{City}, {CountryName}";
}
