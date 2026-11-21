using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A named economic indicator for a geographic scope (for example "CPI inflation,
/// Iran"). The entity holds the latest known value for convenience; the full
/// history lives in <see cref="DataObservation"/> and is never overwritten.
/// </summary>
public class EconomicIndicator : AuditableEntity, IAggregateRoot
{
    private EconomicIndicator()
    {
    }

    public EconomicIndicator(
        string name,
        string indicatorCode,
        VariableCategory category,
        string countryCode,
        GeographicScope geographicScope,
        string unit,
        string? city = null)
    {
        Name = name;
        IndicatorCode = indicatorCode;
        Category = category;
        CountryCode = countryCode;
        GeographicScope = geographicScope;
        Unit = unit;
        City = city;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Stable machine-readable code, e.g. "IR.CPI.YOY".</summary>
    public string IndicatorCode { get; private set; } = string.Empty;

    public VariableCategory Category { get; private set; }

    public string CountryCode { get; private set; } = "IR";

    public string? City { get; private set; }

    public GeographicScope GeographicScope { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    /// <summary>Latest known value. Null when no observation has been recorded.</summary>
    public decimal? CurrentValue { get; private set; }

    public decimal? PreviousValue { get; private set; }

    /// <summary>The date the value describes, not the date it was fetched.</summary>
    public DateTime? ObservationDate { get; private set; }

    public DateTime? RetrievedAtUtc { get; private set; }

    public Guid? SourceId { get; private set; }

    public ConfidenceLevel Confidence { get; private set; } = ConfidenceLevel.Unknown;

    public bool IsVerified { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// How long a value stays "fresh" for this indicator. Exchange rates go
    /// stale in hours; annual statistics stay fresh for months.
    /// </summary>
    public TimeSpan FreshnessWindow { get; private set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Updates the descriptive metadata. The indicator code is deliberately not
    /// editable: it is the stable identifier that observations and external
    /// mappings refer to, so renaming it would silently break those links.
    /// </summary>
    public void UpdateDefinition(
        string name,
        VariableCategory category,
        string countryCode,
        GeographicScope geographicScope,
        string unit,
        string? city)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("An indicator must have a name.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new DomainException("An indicator must have a unit.");
        }

        Name = name.Trim();
        Category = category;
        CountryCode = countryCode;
        GeographicScope = geographicScope;
        Unit = unit.Trim();
        City = city;
        Touch();
    }

    /// <summary>
    /// Applies a new observation to the cached latest value. The previous value
    /// is retained so the UI can show direction of change.
    /// </summary>
    public void ApplyObservation(decimal value, DateTime observationDate, DateTime retrievedAtUtc, Guid? sourceId, ConfidenceLevel confidence, bool isVerified)
    {
        if (ObservationDate is not null && observationDate < ObservationDate)
        {
            // Older observations must not overwrite newer cached values.
            return;
        }

        PreviousValue = CurrentValue;
        CurrentValue = value;
        ObservationDate = observationDate;
        RetrievedAtUtc = retrievedAtUtc;
        SourceId = sourceId;
        Confidence = confidence;
        IsVerified = isVerified;
        Touch();
    }

    public void SetFreshnessWindow(TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "A freshness window must be positive.");
        }

        FreshnessWindow = window;
        Touch();
    }

    /// <summary>
    /// Classifies the cached value's freshness. Returns
    /// <see cref="DataFreshness.Unknown"/> when nothing has been recorded.
    /// </summary>
    public DataFreshness GetFreshness(DateTime nowUtc)
    {
        if (ObservationDate is null)
        {
            return DataFreshness.Unknown;
        }

        var age = nowUtc - ObservationDate.Value;

        if (age <= FreshnessWindow)
        {
            return DataFreshness.Fresh;
        }

        return age <= FreshnessWindow * 2
            ? DataFreshness.Aging
            : DataFreshness.Stale;
    }
}
