using DecisionSimulator.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Web.Services;

/// <summary>
/// Reads the active user's display preferences from the persisted profile.
/// <para>
/// Falls back to <see cref="UserPreferences.Default"/> when no profile exists yet,
/// so the first-run experience still renders with a sensible calendar and
/// currency instead of throwing.
/// </para>
/// </summary>
public sealed class UserPreferencesAccessor : IUserPreferencesAccessor
{
    private readonly IApplicationDbContext _db;

    public UserPreferencesAccessor(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _db.UserProfiles
            .AsNoTracking()
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return UserPreferences.Default;
        }

        return new UserPreferences(
            profile.PreferredLanguage,
            profile.CalendarSystem,
            profile.DefaultCurrencyCode,
            profile.CurrencyUnitConvention,
            profile.TimeZoneId);
    }
}
