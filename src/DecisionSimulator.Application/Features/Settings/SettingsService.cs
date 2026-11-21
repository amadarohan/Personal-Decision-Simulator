using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Settings;

/// <summary>
/// Runtime-editable settings and the local user profile.
/// <para>
/// Secrets are refused here on purpose. An API key stored in the database would
/// end up in every backup file, so keys are supplied through configuration or an
/// environment variable instead.
/// </para>
/// </summary>
public sealed class SettingsService
{
    private readonly IApplicationDbContext _db;

    public SettingsService(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<IReadOnlyList<AppSettingDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AppSettingDto>>.Success(settings.Select(Map).ToList());
    }

    public async Task<Result<AppSettingDto>> GetAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<AppSettingDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A setting key is required.");
        }

        var setting = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        return setting is null
            ? Result<AppSettingDto>.Failure(ErrorCodes.NotFound, "The setting could not be found.")
            : Result<AppSettingDto>.Success(Map(setting));
    }

    /// <summary>
    /// Creates the setting if it does not exist, otherwise updates its value.
    /// </summary>
    public async Task<Result<AppSettingDto>> SetAsync(
        string key,
        SetSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<AppSettingDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A setting key is required.");
        }

        var normalizedKey = key.Trim();

        if (SettingKeys.SecretKeys.Contains(normalizedKey))
        {
            return Result<AppSettingDto>.Failure(
                ErrorCodes.Forbidden,
                "Secrets cannot be stored in the database because they would be included in " +
                "backups. Supply this value through configuration or an environment variable.");
        }

        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == normalizedKey, cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting(normalizedKey, request.Value ?? string.Empty, request.Description);
            _db.AppSettings.Add(setting);
        }
        else
        {
            setting.SetValue(request.Value ?? string.Empty);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<AppSettingDto>.Success(Map(setting));
    }

    public async Task<Result> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The setting could not be found.");
        }

        _db.AppSettings.Remove(setting);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Returns the active profile, creating a default one on first run so the
    /// application always has a currency and calendar to work with.
    /// </summary>
    public async Task<Result<UserProfileDto>> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.UserProfiles
            .AsNoTracking()
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? Result<UserProfileDto>.Failure(
                ErrorCodes.NotFound,
                "No user profile exists yet. Complete onboarding first.")
            : Result<UserProfileDto>.Success(MapProfile(profile));
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<UserProfileDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A display name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CountryCode))
        {
            return Result<UserProfileDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A country code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DefaultCurrencyCode))
        {
            return Result<UserProfileDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A default currency is required.");
        }

        var profile = await _db.UserProfiles
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile(
                request.DisplayName.Trim(),
                new GeoLocation(
                    request.CountryCode.Trim().ToUpperInvariant(),
                    request.CountryName,
                    request.City,
                    request.TimeZoneId),
                request.PreferredLanguage,
                request.CalendarSystem,
                request.DefaultCurrencyCode.Trim().ToUpperInvariant(),
                request.CurrencyUnitConvention);

            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.UpdateLocation(new GeoLocation(
                request.CountryCode.Trim().ToUpperInvariant(),
                request.CountryName,
                request.City,
                request.TimeZoneId));

            profile.UpdatePreferences(
                request.DisplayName.Trim(),
                request.PreferredLanguage,
                request.CalendarSystem,
                request.DefaultCurrencyCode.Trim().ToUpperInvariant(),
                request.CurrencyUnitConvention);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<UserProfileDto>.Success(MapProfile(profile));
    }

    public async Task<Result> CompleteOnboardingAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _db.UserProfiles
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return Result.Failure(
                ErrorCodes.NotFound,
                "No user profile exists yet. Save the profile before completing onboarding.");
        }

        profile.CompleteOnboarding();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static AppSettingDto Map(AppSetting setting) => new(
        setting.Id,
        setting.Key,
        setting.Value,
        setting.Description,
        setting.UpdatedAtUtc);

    private static UserProfileDto MapProfile(UserProfile profile) => new(
        profile.Id,
        profile.DisplayName,
        profile.CountryCode,
        profile.CountryName,
        profile.City ?? string.Empty,
        profile.TimeZoneId,
        profile.PreferredLanguage,
        profile.CalendarSystem,
        profile.DefaultCurrencyCode,
        profile.CurrencyUnitConvention,
        profile.IsOnboardingCompleted);
}
