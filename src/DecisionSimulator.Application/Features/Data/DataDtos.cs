using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Data;

public sealed record DataSourceDto(
    Guid Id,
    string Name,
    DataProviderType ProviderType,
    string? BaseUrl,
    string? Description,
    ReliabilityLevel ReliabilityLevel,
    string? UpdateFrequency,
    bool IsActive,
    bool RequiresApiKey);

public sealed record CreateDataSourceRequest(
    string Name,
    DataProviderType ProviderType,
    ReliabilityLevel ReliabilityLevel,
    string? BaseUrl = null,
    string? Description = null,
    string? UpdateFrequency = null,
    bool RequiresApiKey = false);

public sealed record UpdateDataSourceRequest(
    string Name,
    string? BaseUrl,
    string? Description,
    ReliabilityLevel ReliabilityLevel,
    string? UpdateFrequency,
    bool RequiresApiKey);

/// <summary>
/// An indicator with its cached latest value and a freshness classification.
/// <para>
/// <see cref="Freshness"/> is computed at read time from the observation date
/// and the indicator's own window, so a value that was fresh yesterday is
/// reported as aging today without anything being written.
/// </para>
/// </summary>
public sealed record EconomicIndicatorDto(
    Guid Id,
    string Name,
    string IndicatorCode,
    VariableCategory Category,
    string CountryCode,
    string? City,
    GeographicScope GeographicScope,
    string Unit,
    decimal? CurrentValue,
    decimal? PreviousValue,
    DateTime? ObservationDate,
    DateTime? RetrievedAtUtc,
    Guid? SourceId,
    ConfidenceLevel Confidence,
    bool IsVerified,
    string? Notes,
    DataFreshness Freshness,
    TimeSpan FreshnessWindow);

public sealed record CreateEconomicIndicatorRequest(
    string Name,
    string IndicatorCode,
    VariableCategory Category,
    string CountryCode,
    GeographicScope GeographicScope,
    string Unit,
    string? City = null,
    TimeSpan? FreshnessWindow = null);

public sealed record UpdateEconomicIndicatorRequest(
    string Name,
    VariableCategory Category,
    string CountryCode,
    GeographicScope GeographicScope,
    string Unit,
    string? City,
    TimeSpan? FreshnessWindow);

/// <summary>
/// One observed value with its full provenance. Observations are append-only;
/// a correction is a new observation, never an edit.
/// </summary>
public sealed record DataObservationDto(
    Guid Id,
    Guid DataSourceId,
    Guid? IndicatorId,
    Guid? VariableDefinitionId,
    decimal Value,
    string Unit,
    DateTime ObservationDate,
    DateTime RetrievedAtUtc,
    DateTime ValidFromUtc,
    DateTime? ValidUntilUtc,
    VerificationStatus VerificationStatus,
    ConfidenceLevel Confidence,
    string? Notes,
    string? SourceReference,
    bool IsCurrent);

public sealed record RecordObservationRequest(
    Guid DataSourceId,
    decimal Value,
    string Unit,
    DateTime ObservationDate,
    Guid? IndicatorId = null,
    Guid? VariableDefinitionId = null,
    VerificationStatus VerificationStatus = VerificationStatus.Unverified,
    ConfidenceLevel Confidence = ConfidenceLevel.Unknown,
    string? Notes = null,
    string? SourceReference = null,
    string? OriginalPayloadHash = null);
