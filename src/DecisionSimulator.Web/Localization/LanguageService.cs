using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Settings;
using DecisionSimulator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Web.Localization;

/// <summary>
/// Resolves the active language for the current request and lets the user change it.
/// <para>
/// The choice is persisted on the user profile rather than in a cookie so that it
/// survives a cleared browser and travels with a backup. Switching language
/// triggers a full page reload: every component then renders from a single
/// catalogue, which avoids the half-translated screens that a partial re-render
/// would produce.
/// </para>
/// </summary>
public sealed class LanguageService : IDisposable
{
    /// <summary>Language tag for Persian.</summary>
    public const string PersianTag = "fa-IR";

    /// <summary>Language tag for English.</summary>
    public const string EnglishTag = "en-US";

    private readonly IApplicationDbContext _db;
    private readonly SettingsService _settings;

    /// <summary>
    /// Serialises the first load. Two components ask for the language during the
    /// same render pass, and a <see cref="DbContext"/> cannot run two queries at
    /// once, so the second caller must wait for the first query rather than
    /// start a competing one.
    /// </summary>
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private string? _resolvedTag;

    public LanguageService(IApplicationDbContext db, SettingsService settings)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// The language tag in effect for this request. Persian is the default
    /// because the product ships for a Persian-speaking user first.
    /// </summary>
    public string CurrentTag => _resolvedTag ?? PersianTag;

    /// <summary>The catalogue for <see cref="CurrentTag"/>.</summary>
    public AppStrings Strings => For(CurrentTag);

    /// <summary>True when the active language reads right to left.</summary>
    public bool IsRightToLeft => IsPersian(CurrentTag);

    /// <summary>
    /// True when the active language is Persian. Pages use this to pick enum
    /// labels, which are translated separately from the string catalogue.
    /// </summary>
    public bool IsPersianLanguage => IsPersian(CurrentTag);

    /// <summary>
    /// Loads the persisted language once per request scope. A missing profile
    /// falls back to Persian rather than throwing, so first run still renders.
    /// <para>
    /// Concurrent callers are serialised. Blazor renders a child component
    /// without waiting for the parent's <c>OnInitializedAsync</c> to finish, so
    /// the layout can ask for the language while the document component is still
    /// awaiting it. Without the gate the second call would issue a second query
    /// on the same context and fail.
    /// </para>
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_resolvedTag is not null)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);

        try
        {
            // Re-checked inside the gate: the caller that held it may have
            // already resolved the tag while this one was waiting.
            if (_resolvedTag is not null)
            {
                return;
            }

            var profile = await _db.UserProfiles
                .AsNoTracking()
                .OrderBy(p => p.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            _resolvedTag = Normalize(profile?.PreferredLanguage);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Persists a new language on the profile. Returns false when the tag is not
    /// one this build ships, so the caller can report it instead of silently
    /// falling back.
    /// </summary>
    public async Task<bool> SetLanguageAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (!IsSupported(tag))
        {
            return false;
        }

        var normalized = Normalize(tag);

        var profile = await _db.UserProfiles
            .OrderBy(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            // No profile yet: create one so the choice is not lost. The remaining
            // fields take the same defaults the settings page shows.
            var created = await _settings.UpdateProfileAsync(
                new UpdateUserProfileRequest(
                    "کاربر",
                    "IR",
                    "Iran",
                    "Tehran",
                    "Asia/Tehran",
                    normalized,
                    CalendarSystem.Jalali,
                    "IRR",
                    CurrencyUnitConvention.Toman),
                cancellationToken);

            if (!created.Succeeded)
            {
                return false;
            }
        }
        else
        {
            profile.UpdatePreferences(
                profile.DisplayName,
                normalized,
                profile.CalendarSystem,
                profile.DefaultCurrencyCode,
                profile.CurrencyUnitConvention);

            await _db.SaveChangesAsync(cancellationToken);
        }

        _resolvedTag = normalized;

        return true;
    }

    /// <summary>True when this build ships a catalogue for the tag.</summary>
    public static bool IsSupported(string? tag) =>
        string.Equals(tag, PersianTag, StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, EnglishTag, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps any tag to a shipped catalogue. A regional variant such as "fa" or
    /// "en-GB" resolves to its base language instead of failing.
    /// </summary>
    public static string Normalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return PersianTag;
        }

        return tag.Trim().StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? EnglishTag
            : PersianTag;
    }

    /// <summary>The catalogue for a tag, normalised first.</summary>
    public static AppStrings For(string? tag) =>
        IsPersian(Normalize(tag)) ? AppStringsPersian.Instance : AppStringsEnglish.Instance;

    private static bool IsPersian(string tag) =>
        !string.Equals(tag, EnglishTag, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Releases the load gate. The service is scoped, so the container disposes
    /// it at the end of the request along with the context it uses.
    /// </summary>
    public void Dispose() => _loadGate.Dispose();
}
