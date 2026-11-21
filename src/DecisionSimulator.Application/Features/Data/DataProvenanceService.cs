using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Data;

/// <summary>
/// Manages external data sources, economic indicators, and the append-only
/// observation log.
/// <para>
/// The service never invents a value. Every number it stores arrives through
/// <see cref="RecordObservationAsync"/> with a source, an observation date, and
/// a retrieval timestamp, which is what lets the UI show where a figure came
/// from and how old it is.
/// </para>
/// </summary>
public sealed class DataProvenanceService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public DataProvenanceService(IApplicationDbContext db, IClock clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // ---------------------------------------------------------------- sources

    public async Task<Result<IReadOnlyList<DataSourceDto>>> ListSourcesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.DataSources.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var sources = await query.OrderBy(s => s.Name).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<DataSourceDto>>.Success(sources.Select(MapSource).ToList());
    }

    public async Task<Result<Guid>> CreateSourceAsync(
        CreateDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A data source must have a name.");
        }

        var source = new DataSource(
            request.Name.Trim(),
            request.ProviderType,
            request.ReliabilityLevel,
            request.BaseUrl,
            request.Description,
            request.UpdateFrequency,
            request.RequiresApiKey);

        _db.DataSources.Add(source);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(source.Id);
    }

    public async Task<Result> UpdateSourceAsync(
        Guid id,
        UpdateDataSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await _db.DataSources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The data source could not be found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, "A data source must have a name.");
        }

        source.Update(
            request.Name.Trim(),
            request.BaseUrl,
            request.Description,
            request.ReliabilityLevel,
            request.UpdateFrequency,
            request.RequiresApiKey);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetSourceActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.DataSources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The data source could not be found.");
        }

        if (isActive)
        {
            source.Activate();
        }
        else
        {
            source.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ------------------------------------------------------------- indicators

    public async Task<Result<IReadOnlyList<EconomicIndicatorDto>>> ListIndicatorsAsync(
        string? countryCode = null,
        VariableCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.EconomicIndicators.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            query = query.Where(i => i.CountryCode == countryCode);
        }

        if (category is not null)
        {
            query = query.Where(i => i.Category == category);
        }

        var indicators = await query
            .OrderBy(i => i.CountryCode)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        return Result<IReadOnlyList<EconomicIndicatorDto>>.Success(
            indicators.Select(i => MapIndicator(i, now)).ToList());
    }

    public async Task<Result<EconomicIndicatorDto>> GetIndicatorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var indicator = await _db.EconomicIndicators
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return indicator is null
            ? Result<EconomicIndicatorDto>.Failure(ErrorCodes.NotFound, "The indicator could not be found.")
            : Result<EconomicIndicatorDto>.Success(MapIndicator(indicator, _clock.UtcNow));
    }

    public async Task<Result<Guid>> CreateIndicatorAsync(
        CreateEconomicIndicatorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "An indicator must have a name.");
        }

        if (string.IsNullOrWhiteSpace(request.IndicatorCode))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "An indicator must have a code.");
        }

        if (string.IsNullOrWhiteSpace(request.Unit))
        {
            return Result<Guid>.Failure(
                ErrorCodes.ValidationFailed,
                "An indicator must have a unit so its value is interpretable.");
        }

        var code = request.IndicatorCode.Trim();
        var country = request.CountryCode.Trim().ToUpperInvariant();

        var duplicate = await _db.EconomicIndicators
            .AsNoTracking()
            .AnyAsync(i => i.IndicatorCode == code && i.CountryCode == country, cancellationToken);

        if (duplicate)
        {
            return Result<Guid>.Failure(
                ErrorCodes.Conflict,
                $"An indicator with the code '{code}' already exists for {country}.");
        }

        var indicator = new EconomicIndicator(
            request.Name.Trim(),
            code,
            request.Category,
            country,
            request.GeographicScope,
            request.Unit.Trim(),
            request.City);

        if (request.FreshnessWindow is not null)
        {
            try
            {
                indicator.SetFreshnessWindow(request.FreshnessWindow.Value);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
            }
        }

        _db.EconomicIndicators.Add(indicator);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(indicator.Id);
    }

    public async Task<Result> UpdateIndicatorAsync(
        Guid id,
        UpdateEconomicIndicatorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var indicator = await _db.EconomicIndicators.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (indicator is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The indicator could not be found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, "An indicator must have a name.");
        }

        if (string.IsNullOrWhiteSpace(request.Unit))
        {
            return Result.Failure(
                ErrorCodes.ValidationFailed,
                "An indicator must have a unit so its value is interpretable.");
        }

        indicator.UpdateDefinition(
            request.Name.Trim(),
            request.Category,
            request.CountryCode.Trim().ToUpperInvariant(),
            request.GeographicScope,
            request.Unit.Trim(),
            request.City);

        if (request.FreshnessWindow is not null)
        {
            try
            {
                indicator.SetFreshnessWindow(request.FreshnessWindow.Value);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ----------------------------------------------------------- observations

    public async Task<Result<IReadOnlyList<DataObservationDto>>> ListObservationsAsync(
        Guid? indicatorId = null,
        Guid? variableDefinitionId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (indicatorId is null && variableDefinitionId is null)
        {
            return Result<IReadOnlyList<DataObservationDto>>.Failure(
                ErrorCodes.ValidationFailed,
                "Specify an indicator or a variable definition to list observations for.");
        }

        var query = _db.DataObservations.AsNoTracking();

        if (indicatorId is not null)
        {
            query = query.Where(o => o.IndicatorId == indicatorId);
        }

        if (variableDefinitionId is not null)
        {
            query = query.Where(o => o.VariableDefinitionId == variableDefinitionId);
        }

        var observations = await query
            .OrderByDescending(o => o.ObservationDate)
            .ThenByDescending(o => o.RetrievedAtUtc)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DataObservationDto>>.Success(
            observations.Select(MapObservation).ToList());
    }

    /// <summary>
    /// Appends an observation and, when it targets an indicator, refreshes that
    /// indicator's cached value.
    /// <para>
    /// The previous observation is closed rather than deleted, so the history
    /// stays auditable. An observation older than the current one is still
    /// stored — it is real data — but it does not overwrite the cached value.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> RecordObservationAsync(
        RecordObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IndicatorId is null && request.VariableDefinitionId is null)
        {
            return Result<Guid>.Failure(
                ErrorCodes.ValidationFailed,
                "An observation must reference either an indicator or a variable definition.");
        }

        if (string.IsNullOrWhiteSpace(request.Unit))
        {
            return Result<Guid>.Failure(
                ErrorCodes.ValidationFailed,
                "An observation must have a unit so its value is interpretable.");
        }

        var source = await _db.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.DataSourceId, cancellationToken);

        if (source is null)
        {
            return Result<Guid>.Failure(ErrorCodes.NotFound, "The data source could not be found.");
        }

        EconomicIndicator? indicator = null;

        if (request.IndicatorId is not null)
        {
            indicator = await _db.EconomicIndicators
                .FirstOrDefaultAsync(i => i.Id == request.IndicatorId, cancellationToken);

            if (indicator is null)
            {
                return Result<Guid>.Failure(ErrorCodes.NotFound, "The indicator could not be found.");
            }
        }

        if (request.VariableDefinitionId is not null)
        {
            var definitionExists = await _db.VariableDefinitions
                .AsNoTracking()
                .AnyAsync(v => v.Id == request.VariableDefinitionId, cancellationToken);

            if (!definitionExists)
            {
                return Result<Guid>.Failure(ErrorCodes.NotFound, "The variable definition could not be found.");
            }
        }

        var now = _clock.UtcNow;

        DataObservation observation;

        try
        {
            observation = new DataObservation(
                request.DataSourceId,
                request.Value,
                request.Unit.Trim(),
                request.ObservationDate,
                now,
                request.IndicatorId,
                request.VariableDefinitionId,
                request.OriginalPayloadHash,
                request.VerificationStatus,
                request.Confidence,
                request.Notes,
                request.SourceReference);
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        _db.DataObservations.Add(observation);

        if (indicator is not null)
        {
            // Close the window on the observation this one replaces, but only
            // when the new value is actually newer. A backfilled older value
            // must not invalidate the current one.
            var current = await _db.DataObservations
                .Where(o => o.IndicatorId == indicator.Id && o.ValidUntilUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var previous in current)
            {
                if (previous.ObservationDate <= request.ObservationDate)
                {
                    previous.Supersede(now);
                }
            }

            indicator.ApplyObservation(
                request.Value,
                request.ObservationDate,
                now,
                request.DataSourceId,
                request.Confidence,
                request.VerificationStatus is VerificationStatus.CrossVerified
                    or VerificationStatus.SingleSource);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(observation.Id);
    }

    /// <summary>
    /// Records a verification decision on an observation. Used when a second
    /// source confirms or contradicts a value.
    /// </summary>
    public async Task<Result> VerifyObservationAsync(
        Guid observationId,
        VerificationStatus status,
        ConfidenceLevel confidence,
        CancellationToken cancellationToken = default)
    {
        var observation = await _db.DataObservations
            .FirstOrDefaultAsync(o => o.Id == observationId, cancellationToken);

        if (observation is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The observation could not be found.");
        }

        observation.MarkVerified(status, confidence);

        if (observation.IndicatorId is not null)
        {
            var indicator = await _db.EconomicIndicators
                .FirstOrDefaultAsync(i => i.Id == observation.IndicatorId, cancellationToken);

            // Only the current observation may change the cached verification
            // state; verifying an old row must not relabel the latest value.
            if (indicator is not null && observation.ValidUntilUtc is null)
            {
                indicator.ApplyObservation(
                    observation.Value,
                    observation.ObservationDate,
                    observation.RetrievedAtUtc,
                    observation.DataSourceId,
                    confidence,
                    status is VerificationStatus.CrossVerified or VerificationStatus.SingleSource);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static DataSourceDto MapSource(DataSource source) =>
        new(
            source.Id,
            source.Name,
            source.ProviderType,
            source.BaseUrl,
            source.Description,
            source.ReliabilityLevel,
            source.UpdateFrequency,
            source.IsActive,
            source.RequiresApiKey);

    private static EconomicIndicatorDto MapIndicator(EconomicIndicator indicator, DateTime nowUtc) =>
        new(
            indicator.Id,
            indicator.Name,
            indicator.IndicatorCode,
            indicator.Category,
            indicator.CountryCode,
            indicator.City,
            indicator.GeographicScope,
            indicator.Unit,
            indicator.CurrentValue,
            indicator.PreviousValue,
            indicator.ObservationDate,
            indicator.RetrievedAtUtc,
            indicator.SourceId,
            indicator.Confidence,
            indicator.IsVerified,
            indicator.Notes,
            indicator.GetFreshness(nowUtc),
            indicator.FreshnessWindow);

    private static DataObservationDto MapObservation(DataObservation observation) =>
        new(
            observation.Id,
            observation.DataSourceId,
            observation.IndicatorId,
            observation.VariableDefinitionId,
            observation.Value,
            observation.Unit,
            observation.ObservationDate,
            observation.RetrievedAtUtc,
            observation.ValidFromUtc,
            observation.ValidUntilUtc,
            observation.VerificationStatus,
            observation.Confidence,
            observation.Notes,
            observation.SourceReference,
            observation.ValidUntilUtc is null);
}
