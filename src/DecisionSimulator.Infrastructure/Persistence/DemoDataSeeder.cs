using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DecisionSimulator.Infrastructure.Persistence;

/// <summary>
/// Installs a small, realistic set of sample data so that a fresh installation
/// has something to look at.
/// <para>
/// The data is deliberately Persian and deliberately Iranian in context: the
/// application defaults to Jalali dates, Toman display and a Tehran time zone,
/// and sample data that ignored that would make the first run look broken.
/// </para>
/// <para>
/// Every number here is an assumption, not a measurement. The scenarios are
/// labelled with <see cref="DistributionBasis.Exploratory"/> or
/// <see cref="DistributionBasis.UserDefined"/> so the interface never presents
/// them as observed data.
/// </para>
/// <para>
/// The seeder runs at most once. It writes a marker setting on completion and
/// also refuses to run when the database already holds decisions, so a user who
/// deleted the samples does not get them back on the next start.
/// </para>
/// </summary>
public sealed class DemoDataSeeder
{
    /// <summary>
    /// Presence of this setting means the samples were already offered. It is
    /// written even when seeding is skipped, so the check is a single lookup.
    /// </summary>
    public const string MarkerKey = "demo.data.seeded";

    private readonly DecisionSimulatorDbContext _db;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(DecisionSimulatorDbContext db, ILogger<DemoDataSeeder> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seeds the sample data. Returns true when rows were inserted.
    /// </summary>
    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        var alreadyMarked = await _db.AppSettings
            .AsNoTracking()
            .AnyAsync(s => s.Key == MarkerKey, cancellationToken);

        if (alreadyMarked)
        {
            return false;
        }

        // A database that already holds decisions belongs to the user. Adding
        // samples on top would mix invented data into real records.
        var hasDecisions = await _db.Decisions
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (hasDecisions)
        {
            await MarkAsync(cancellationToken);
            return false;
        }

        var definitions = await _db.VariableDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Key, v => v.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (definitions.Count == 0)
        {
            // The built-in catalogue is installed first and is what the models
            // bind to. Without it the scenarios below would reference nothing.
            SeederLog.DemoSeedSkippedNoCatalogue(_logger);
            return false;
        }

        var now = DateTime.UtcNow;

        await SeedProfileAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedTagsAsync(cancellationToken);

        var sources = await SeedDataSourcesAsync(cancellationToken);
        await SeedIndicatorsAsync(sources, now, cancellationToken);
        await SeedRiskFactorsAsync(cancellationToken);

        await SeedDecisionsAsync(definitions, now, cancellationToken);

        await MarkAsync(cancellationToken);

        SeederLog.DemoSeedCompleted(_logger);

        return true;
    }

    private async Task MarkAsync(CancellationToken cancellationToken)
    {
        _db.AppSettings.Add(new AppSetting(
            MarkerKey,
            "true",
            "Set once the sample data has been offered. Deleting it re-enables seeding on an empty database."));

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedProfileAsync(CancellationToken cancellationToken)
    {
        var exists = await _db.UserProfiles.AnyAsync(cancellationToken);

        if (exists)
        {
            return;
        }

        var profile = new UserProfile(
            "کاربر نمونه",
            new GeoLocation("IR", "ایران", "تهران", "Asia/Tehran"),
            "fa-IR",
            CalendarSystem.Jalali,
            "IRR",
            CurrencyUnitConvention.Toman);

        profile.CompleteOnboarding();

        _db.UserProfiles.Add(profile);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.AppSettings
            .AsNoTracking()
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        // Only non-secret defaults. An API key is never written here: the
        // settings page refuses to store secrets and the AI provider reads them
        // from configuration instead. The interface language is deliberately
        // absent: it lives on the user profile, and a second copy here would be
        // a value nothing reads and that could drift out of sync.
        var defaults = new (string Key, string Value, string Description)[]
        {
            ("ui.defaultCurrencyCode", "IRR", "Currency used for money formatting."),
            ("ui.calendarSystem", "Jalali", "Calendar used for date formatting."),
            ("ui.currencyUnitConvention", "Toman", "Display IRR amounts in Toman."),
            ("ai.provider", "None", "No language model is configured by default."),
            ("ai.allowRemote", "false", "Remote AI calls are opt-in.")
        };

        var added = false;

        foreach (var (key, value, description) in defaults)
        {
            if (!known.Add(key))
            {
                continue;
            }

            _db.AppSettings.Add(new AppSetting(key, value, description));
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedTagsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Tags
            .AsNoTracking()
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var tags = new (string Name, string Color)[]
        {
            ("خانواده", "#5b8def"),
            ("مالی", "#4caf7d"),
            ("شغلی", "#d9a441"),
            ("بلندمدت", "#9b7bd4"),
            ("نیازمند داده", "#e05c5c")
        };

        var added = false;

        foreach (var (name, color) in tags)
        {
            if (!known.Add(name))
            {
                continue;
            }

            _db.Tags.Add(new Tag(name, color));
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, Guid>> SeedDataSourcesAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.DataSources
            .AsNoTracking()
            .Select(s => new { s.Name, s.Id })
            .ToListAsync(cancellationToken);

        var byName = existing.ToDictionary(s => s.Name, s => s.Id, StringComparer.OrdinalIgnoreCase);

        var catalogue = new (string Name, DataProviderType Provider, ReliabilityLevel Reliability, string? Url, string Description, string Frequency)[]
        {
            (
                "بانک مرکزی ایران",
                DataProviderType.Manual,
                ReliabilityLevel.Authoritative,
                "https://www.cbi.ir",
                "آمار رسمی پولی و ارزی. ورود دستی، چون دسترسی برنامه‌ای به این سرویس تضمین‌شده نیست.",
                "ماهانه"),
            (
                "مرکز آمار ایران",
                DataProviderType.Manual,
                ReliabilityLevel.High,
                "https://www.amar.org.ir",
                "شاخص قیمت مصرف‌کننده و آمار جمعیتی. ورود دستی.",
                "ماهانه"),
            (
                "ثبت دستی مشاهدات شخصی",
                DataProviderType.Manual,
                ReliabilityLevel.Medium,
                null,
                "فیش حقوقی، قرارداد اجاره و صورتحساب‌های خود کاربر. منبع اصلی برای فرض‌های شخصی.",
                "نامنظم")
        };

        var added = false;

        foreach (var (name, provider, reliability, url, description, frequency) in catalogue)
        {
            if (byName.ContainsKey(name))
            {
                continue;
            }

            var source = new DataSource(name, provider, reliability, url, description, frequency);
            _db.DataSources.Add(source);
            byName[name] = source.Id;
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return byName;
    }

    private async Task SeedIndicatorsAsync(
        Dictionary<string, Guid> sources,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await _db.EconomicIndicators
            .AsNoTracking()
            .Select(i => i.IndicatorCode)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        sources.TryGetValue("بانک مرکزی ایران", out var centralBank);
        sources.TryGetValue("مرکز آمار ایران", out var statisticsCentre);

        var catalogue = new (string Name, string Code, VariableCategory Category, string Unit, decimal Value, Guid Source, ConfidenceLevel Confidence)[]
        {
            ("نرخ تورم نقطه‌به‌نقطه", "IR.CPI.YOY", VariableCategory.Economic, "درصد", 38.5m, statisticsCentre, ConfidenceLevel.Medium),
            ("نرخ ارز آزاد دلار", "IR.FX.USD", VariableCategory.Economic, "ریال", 612_000m, centralBank, ConfidenceLevel.Low),
            ("شاخص اجاره‌بهای مسکن شهری", "IR.RENT.INDEX", VariableCategory.Economic, "شاخص", 214.7m, statisticsCentre, ConfidenceLevel.Medium),
            ("نرخ بیکاری", "IR.UNEMP.RATE", VariableCategory.Economic, "درصد", 7.6m, statisticsCentre, ConfidenceLevel.Medium)
        };

        var added = false;

        foreach (var (name, code, category, unit, value, sourceId, confidence) in catalogue)
        {
            if (!known.Add(code))
            {
                continue;
            }

            var indicator = new EconomicIndicator(
                name,
                code,
                category,
                "IR",
                GeographicScope.Country,
                unit);

            // The observation date is the month the figure describes, not the
            // moment it was entered. Keeping them separate is what makes the
            // freshness classification meaningful.
            indicator.ApplyObservation(
                value,
                new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1),
                now,
                sourceId == Guid.Empty ? null : sourceId,
                confidence,
                isVerified: false);

            _db.EconomicIndicators.Add(indicator);
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedRiskFactorsAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.RiskFactors
            .AsNoTracking()
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var catalogue = new (string Name, RiskCategory Category, GeographicScope Scope, ImpactDirection Direction, string Description, string Magnitude, ConfidenceLevel Likelihood, ConfidenceLevel Confidence)[]
        {
            (
                "سخت‌تر شدن کنترل‌های ارزی",
                RiskCategory.Regulatory,
                GeographicScope.Country,
                ImpactDirection.Negative,
                "محدودیت جدید بر انتقال ارز می‌تواند هزینهٔ نقل مکان یا واردات کالا را به‌شدت بالا ببرد.",
                "می‌تواند تا ۴۰ درصد هزینهٔ اولیه را افزایش دهد.",
                ConfidenceLevel.Medium,
                ConfidenceLevel.Low),
            (
                "جهش تورم و کاهش قدرت خرید",
                RiskCategory.Economic,
                GeographicScope.Country,
                ImpactDirection.Negative,
                "اگر تورم از رشد درآمد جلو بزند، هر سناریوی با درآمد ثابت بدبینانه‌تر از محاسبه می‌شود.",
                "کاهش سالانهٔ ۱۵ تا ۲۵ درصدی قدرت خرید.",
                ConfidenceLevel.High,
                ConfidenceLevel.Medium),
            (
                "افزایش شدید اجاره‌بها در شهر مقصد",
                RiskCategory.Market,
                GeographicScope.City,
                ImpactDirection.Negative,
                "بازار اجاره در شهرهای بزرگ می‌تواند در یک فصل بیش از پیش‌بینی رشد کند.",
                "افزایش ۳۰ تا ۵۰ درصدی هزینهٔ مسکن.",
                ConfidenceLevel.Medium,
                ConfidenceLevel.Low),
            (
                "اشباع بازار کار در حوزهٔ تخصصی",
                RiskCategory.Personal,
                GeographicScope.Region,
                ImpactDirection.Negative,
                "پیدا کردن نقش هم‌سطح در شهر جدید ممکن است چند ماه طول بکشد.",
                "تا شش ماه بدون درآمد کامل.",
                ConfidenceLevel.Medium,
                ConfidenceLevel.Low),
            (
                "دسترسی به اینترنت بین‌الملل و ابزارهای ابری",
                RiskCategory.Infrastructure,
                GeographicScope.Country,
                ImpactDirection.Mixed,
                "کسب‌وکارهای آنلاین به دسترسی پایدار وابسته‌اند؛ اختلال هم فرصت و هم تهدید است.",
                "کاهش ۲۰ درصدی درآمد فروش آنلاین در ماه‌های اختلال.",
                ConfidenceLevel.Medium,
                ConfidenceLevel.Medium),
            (
                "حمایت خانوادگی در شهر مقصد",
                RiskCategory.Personal,
                GeographicScope.City,
                ImpactDirection.Positive,
                "وجود شبکهٔ خانوادگی هزینهٔ childcare و زمان بازیابی پس از بحران را کم می‌کند.",
                "کاهش ۱۰ تا ۱۵ درصدی هزینهٔ ماهانه.",
                ConfidenceLevel.High,
                ConfidenceLevel.Medium)
        };

        var added = false;

        foreach (var (name, category, scope, direction, description, magnitude, likelihood, confidence) in catalogue)
        {
            if (!known.Add(name))
            {
                continue;
            }

            _db.RiskFactors.Add(new RiskFactor(
                name,
                category,
                scope,
                direction,
                description,
                magnitude,
                likelihood,
                confidence,
                sourceReference: null));

            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedDecisionsAsync(
        Dictionary<string, Guid> definitions,
        DateTime now,
        CancellationToken cancellationToken)
    {
        Guid Def(string key) => definitions[key];

        // --- 1. Relocation ---------------------------------------------------
        var relocation = new Decision(
            "آیا به شهر دیگری نقل مکان کنم؟",
            DecisionCategory.Relocation,
            "پیشنهاد کاری در اصفهان با حقوق تقریباً برابر، اما هزینهٔ مسکن کمتر و شبکهٔ خانوادگی موجود.",
            ImportanceLevel.High,
            DecisionHorizon.MediumTerm,
            RiskTolerance.Moderate);

        relocation.UpdateDetails(
            relocation.Title,
            relocation.Description,
            relocation.Category,
            relocation.Importance,
            relocation.Horizon,
            relocation.RiskTolerance,
            context: "اجاره‌نامهٔ تهران در پایان سال تمام می‌شود.",
            desiredOutcome: "کاهش هزینهٔ ماهانه بدون افت کیفیت زندگی.",
            constraintsJson: null,
            targetDecisionDate: now.AddMonths(2));

        var stay = relocation.AddAlternative(
            "ماندن در تهران",
            "ادامهٔ وضعیت فعلی بدون تغییر.",
            isBaseline: true,
            estimatedInitialCost: 0m,
            estimatedRecurringCost: 32_000_000m,
            expectedBenefits: "بدون هزینهٔ جابه‌جایی، شبکهٔ کاری حفظ می‌شود.",
            knownRisks: "اجارهٔ سال بعد ممکن است ۴۰ درصد رشد کند.");

        var move = relocation.AddAlternative(
            "نقل مکان به اصفهان",
            "جابه‌جایی کامل با همان نقش شغلی.",
            isBaseline: false,
            estimatedInitialCost: 180_000_000m,
            estimatedRecurringCost: 21_000_000m,
            expectedBenefits: "هزینهٔ مسکن کمتر و نزدیکی به خانواده.",
            knownRisks: "دورهٔ بدون درآمد تا شروع کار جدید.");

        var relocationExpected = relocation.AddScenario(
            "نقل مکان با حقوق برابر",
            ScenarioType.Expected,
            24,
            move.Id,
            description: "درآمد فعلی حفظ می‌شود و هزینهٔ مسکن ۳۵ درصد کاهش می‌یابد.");

        relocationExpected.SetVariable(Def("monthly_income"), 95_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "فیش حقوقی، ماه گذشته", DistributionBasis.UserDefined, isUserOverride: true);
        relocationExpected.SetVariable(Def("monthly_expenses"), 21_000_000m, DistributionType.Normal, null, null, 2_500_000m, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد بر پایهٔ اجاره‌های اصفهان", DistributionBasis.Exploratory, isUserOverride: true);
        relocationExpected.SetVariable(Def("starting_cash"), 250_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "موجودی حساب", DistributionBasis.UserDefined, isUserOverride: true);
        relocationExpected.SetVariable(Def("relocation_one_off_cost"), 180_000_000m, DistributionType.Uniform, 150_000_000m, 230_000_000m, null, ConfidenceLevel.Low, SourceType.UserInput, "برآورد شرکت حمل", DistributionBasis.Exploratory, isUserOverride: true);
        relocationExpected.SetVariable(Def("destination_monthly_income"), 95_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "پیشنهاد کاری", DistributionBasis.UserDefined, isUserOverride: true);
        relocationExpected.SetVariable(Def("destination_monthly_expenses"), 21_000_000m, DistributionType.Normal, null, null, 2_500_000m, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد", DistributionBasis.Exploratory, isUserOverride: true);

        var relocationStress = relocation.AddScenario(
            "شش ماه بدون درآمد در شهر جدید",
            ScenarioType.StressTest,
            24,
            move.Id,
            description: "پیدا کردن کار در شهر جدید طول می‌کشد و درآمد شش ماه اول صفر است.");

        relocationStress.SetVariable(Def("monthly_income"), 0m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Low, SourceType.UserInput, "فرض بدبینانه", DistributionBasis.Exploratory, isUserOverride: true);
        relocationStress.SetVariable(Def("monthly_expenses"), 21_000_000m, DistributionType.Uniform, 19_000_000m, 26_000_000m, null, ConfidenceLevel.Low, SourceType.UserInput, "برآورد", DistributionBasis.Exploratory, isUserOverride: true);
        relocationStress.SetVariable(Def("starting_cash"), 250_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "موجودی حساب", DistributionBasis.UserDefined, isUserOverride: true);
        relocationStress.SetVariable(Def("relocation_one_off_cost"), 180_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد", DistributionBasis.UserDefined, isUserOverride: true);

        relocation.ChangeStatus(DecisionStatus.Exploring);
        relocation.ChangeStatus(DecisionStatus.Modeling);

        // --- 2. Business -----------------------------------------------------
        var business = new Decision(
            "کسب‌وکار آنلاین راه بیندازم یا در استخدام بمانم؟",
            DecisionCategory.Business,
            "یک فروشگاه آنلاین کوچک با سرمایهٔ اولیهٔ محدود؛ ریسک اصلی، نبود درآمد در ماه‌های اول است.",
            ImportanceLevel.Critical,
            DecisionHorizon.LongTerm,
            RiskTolerance.Low);

        var employment = business.AddAlternative(
            "ادامهٔ استخدام",
            "حفظ درآمد ثابت و راه‌اندازی کسب‌وکار در وقت آزاد.",
            isBaseline: true,
            estimatedInitialCost: 0m,
            estimatedRecurringCost: 0m,
            expectedBenefits: "درآمد قابل پیش‌بینی.",
            knownRisks: "رشد درآمد محدود و وابستگی به یک کارفرما.");

        var shop = business.AddAlternative(
            "راه‌اندازی فروشگاه آنلاین",
            "تمرکز کامل روی کسب‌وکار با سرمایهٔ اولیهٔ ۴۰۰ میلیون تومان.",
            isBaseline: false,
            estimatedInitialCost: 400_000_000m,
            estimatedRecurringCost: 45_000_000m,
            expectedBenefits: "سقف درآمد بالاتر و استقلال تصمیم.",
            knownRisks: "شش تا دوازده ماه بدون سود.");

        var businessExpected = business.AddScenario(
            "رسیدن به نقطهٔ سربه‌سر در ماه نهم",
            ScenarioType.Expected,
            36,
            shop.Id,
            description: "فروش به‌تدریج رشد می‌کند و از ماه نهم هزینه‌ها پوشش داده می‌شود.");

        businessExpected.SetVariable(Def("business_startup_cost"), 400_000_000m, DistributionType.Uniform, 350_000_000m, 480_000_000m, null, ConfidenceLevel.Medium, SourceType.UserInput, "فهرست خرید تجهیزات", DistributionBasis.UserDefined, isUserOverride: true);
        businessExpected.SetVariable(Def("business_monthly_revenue"), 55_000_000m, DistributionType.LogNormal, null, null, 0.35m, ConfidenceLevel.Low, SourceType.UserInput, "برآورد بر پایهٔ سه فروشگاه مشابه", DistributionBasis.Exploratory, isUserOverride: true);
        businessExpected.SetVariable(Def("business_monthly_cost"), 45_000_000m, DistributionType.Normal, null, null, 5_000_000m, ConfidenceLevel.Medium, SourceType.UserInput, "اجارهٔ انبار و حقوق یک نفر", DistributionBasis.UserDefined, isUserOverride: true);
        businessExpected.SetVariable(Def("business_revenue_growth"), 8m, DistributionType.Uniform, 3m, 14m, null, ConfidenceLevel.Low, SourceType.UserInput, "فرض رشد ماهانه", DistributionBasis.Exploratory, isUserOverride: true);
        businessExpected.SetVariable(Def("monthly_expenses"), 28_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "هزینهٔ زندگی", DistributionBasis.UserDefined, isUserOverride: true);

        var businessStress = business.AddScenario(
            "تعطیلی فروشگاه در ماه ششم",
            ScenarioType.StressTest,
            36,
            shop.Id,
            description: "فروش به سطح پوشش هزینه نمی‌رسد و کسب‌وکار در ماه ششم بسته می‌شود.");

        businessStress.SetVariable(Def("business_startup_cost"), 400_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Medium, SourceType.UserInput, "فهرست خرید", DistributionBasis.UserDefined, isUserOverride: true);
        businessStress.SetVariable(Def("business_monthly_revenue"), 18_000_000m, DistributionType.Uniform, 8_000_000m, 30_000_000m, null, ConfidenceLevel.Low, SourceType.UserInput, "فرض بدبینانه", DistributionBasis.Exploratory, isUserOverride: true);
        businessStress.SetVariable(Def("business_monthly_cost"), 45_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Medium, SourceType.UserInput, "اجاره و حقوق", DistributionBasis.UserDefined, isUserOverride: true);
        businessStress.SetVariable(Def("business_revenue_growth"), 0m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Low, SourceType.UserInput, "بدون رشد", DistributionBasis.Exploratory, isUserOverride: true);

        business.ChangeStatus(DecisionStatus.Exploring);
        business.ChangeStatus(DecisionStatus.Modeling);

        // --- 3. Housing ------------------------------------------------------
        var housing = new Decision(
            "خانه بخرم یا اجاره‌نشین بمانم؟",
            DecisionCategory.Housing,
            "خرید یک آپارتمان ۸۵ متری با وام بانکی در برابر ادامهٔ اجاره‌نشینی و سرمایه‌گذاری مابه‌التفاوت.",
            ImportanceLevel.Critical,
            DecisionHorizon.VeryLongTerm,
            RiskTolerance.Moderate);

        var rent = housing.AddAlternative(
            "ادامهٔ اجاره‌نشینی",
            "اجارهٔ فعلی و سرمایه‌گذاری پس‌انداز در دارایی کم‌ریسک.",
            isBaseline: true,
            estimatedInitialCost: 0m,
            estimatedRecurringCost: 32_000_000m,
            expectedBenefits: "انعطاف جابه‌جایی و نقدشوندگی.",
            knownRisks: "رشد اجاره و نبود دارایی ثابت.");

        var buy = housing.AddAlternative(
            "خرید آپارتمان با وام",
            "پیش‌پرداخت ۲ میلیارد تومان و وام ۱٫۵ میلیارد تومانی.",
            isBaseline: false,
            estimatedInitialCost: 2_000_000_000m,
            estimatedRecurringCost: 48_000_000m,
            expectedBenefits: "دارایی ثابت و قسط ثابت در برابر تورم.",
            knownRisks: "کاهش نقدشوندگی و ریسک نرخ سود.");

        var housingBaseline = housing.AddScenario(
            "نرخ سود ثابت تا پایان دوره",
            ScenarioType.Baseline,
            120,
            buy.Id,
            description: "نرخ سود در تمام دورهٔ بازپرداخت تغییر نمی‌کند.");

        housingBaseline.SetVariable(Def("loan_principal"), 1_500_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "قرارداد بانک", DistributionBasis.UserDefined, isUserOverride: true);
        housingBaseline.SetVariable(Def("annual_interest_rate"), 23m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "نرخ اعلامی بانک", DistributionBasis.UserDefined, isUserOverride: true);
        housingBaseline.SetVariable(Def("loan_term_months"), 120m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "قرارداد بانک", DistributionBasis.UserDefined, isUserOverride: true);
        housingBaseline.SetVariable(Def("monthly_income"), 95_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "فیش حقوقی", DistributionBasis.UserDefined, isUserOverride: true);
        housingBaseline.SetVariable(Def("monthly_expenses"), 30_000_000m, DistributionType.Normal, null, null, 3_000_000m, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد", DistributionBasis.Exploratory, isUserOverride: true);

        var housingStress = housing.AddScenario(
            "افزایش نرخ سود در سال سوم",
            ScenarioType.Pessimistic,
            120,
            buy.Id,
            description: "نرخ سود بازنگری می‌شود و قسط ماهانه بالا می‌رود.");

        housingStress.SetVariable(Def("loan_principal"), 1_500_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "قرارداد بانک", DistributionBasis.UserDefined, isUserOverride: true);
        housingStress.SetVariable(Def("annual_interest_rate"), 31m, DistributionType.Uniform, 26m, 36m, null, ConfidenceLevel.Low, SourceType.UserInput, "فرض بدبینانه", DistributionBasis.Exploratory, isUserOverride: true);
        housingStress.SetVariable(Def("loan_term_months"), 120m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "قرارداد بانک", DistributionBasis.UserDefined, isUserOverride: true);
        housingStress.SetVariable(Def("monthly_income"), 95_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "فیش حقوقی", DistributionBasis.UserDefined, isUserOverride: true);
        housingStress.SetVariable(Def("monthly_expenses"), 34_000_000m, DistributionType.Normal, null, null, 4_000_000m, ConfidenceLevel.Low, SourceType.UserInput, "برآورد با تورم بالاتر", DistributionBasis.Exploratory, isUserOverride: true);
        housingStress.SetVariable(Def("monthly_inflation"), 45m, DistributionType.Uniform, 35m, 60m, null, ConfidenceLevel.Low, SourceType.UserInput, "فرض بدبینانه", DistributionBasis.Exploratory, isUserOverride: true);

        housing.ChangeStatus(DecisionStatus.Exploring);

        // --- 4. Career -------------------------------------------------------
        var career = new Decision(
            "پیشنهاد شغلی با درآمد بالاتر را بپذیرم؟",
            DecisionCategory.Career,
            "شرکت جدید ۲۵ درصد بیشتر می‌دهد اما دورهٔ آزمایشی سه‌ماهه و تیم کوچک‌تری دارد.",
            ImportanceLevel.High,
            DecisionHorizon.MediumTerm,
            RiskTolerance.High);

        var currentRole = career.AddAlternative(
            "ماندن در نقش فعلی",
            "ثبات، شناخت تیم و مسیر ارتقای مشخص.",
            isBaseline: true,
            estimatedInitialCost: 0m,
            estimatedRecurringCost: 0m,
            expectedBenefits: "بدون ریسک دورهٔ آزمایشی.",
            knownRisks: "رشد حقوق کمتر از تورم.");

        var newRole = career.AddAlternative(
            "پذیرش پیشنهاد شرکت جدید",
            "حقوق بالاتر با ریسک دورهٔ آزمایشی.",
            isBaseline: false,
            estimatedInitialCost: 0m,
            estimatedRecurringCost: 0m,
            expectedBenefits: "افزایش ۲۵ درصدی درآمد پایه.",
            knownRisks: "احتمال پایان همکاری در سه ماه اول.");

        var careerOptimistic = career.AddScenario(
            "رشد درآمد ۲۵ درصدی",
            ScenarioType.Optimistic,
            24,
            newRole.Id,
            description: "دورهٔ آزمایشی موفق است و رشد سالانه ادامه می‌یابد.");

        careerOptimistic.SetVariable(Def("monthly_income"), 118_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "پیشنهاد کتبی", DistributionBasis.UserDefined, isUserOverride: true);
        careerOptimistic.SetVariable(Def("monthly_expenses"), 30_000_000m, DistributionType.Normal, null, null, 3_000_000m, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد", DistributionBasis.Exploratory, isUserOverride: true);
        careerOptimistic.SetVariable(Def("monthly_income_growth"), 2.5m, DistributionType.Uniform, 1m, 4m, null, ConfidenceLevel.Low, SourceType.UserInput, "فرض رشد ماهانه", DistributionBasis.Exploratory, isUserOverride: true);
        careerOptimistic.SetVariable(Def("monthly_expense_growth"), 3m, DistributionType.Uniform, 2m, 5m, null, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد تورم", DistributionBasis.Exploratory, isUserOverride: true);
        careerOptimistic.SetVariable(Def("starting_cash"), 250_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "موجودی حساب", DistributionBasis.UserDefined, isUserOverride: true);

        var careerStress = career.AddScenario(
            "پایان همکاری در ماه سوم",
            ScenarioType.Pessimistic,
            24,
            newRole.Id,
            description: "دورهٔ آزمایشی تمدید نمی‌شود و بازگشت به بازار کار دو ماه طول می‌کشد.");

        careerStress.SetVariable(Def("monthly_income"), 118_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "پیشنهاد کتبی", DistributionBasis.UserDefined, isUserOverride: true);
        careerStress.SetVariable(Def("monthly_expenses"), 30_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Medium, SourceType.UserInput, "برآورد", DistributionBasis.UserDefined, isUserOverride: true);
        careerStress.SetVariable(Def("monthly_income_growth"), 0m, DistributionType.Fixed, null, null, null, ConfidenceLevel.Low, SourceType.UserInput, "بدون رشد", DistributionBasis.Exploratory, isUserOverride: true);
        careerStress.SetVariable(Def("starting_cash"), 250_000_000m, DistributionType.Fixed, null, null, null, ConfidenceLevel.High, SourceType.UserInput, "موجودی حساب", DistributionBasis.UserDefined, isUserOverride: true);

        career.ChangeStatus(DecisionStatus.Exploring);
        career.ChangeStatus(DecisionStatus.Modeling);
        career.ChangeStatus(DecisionStatus.Evaluating);

        // A review on the relocation decision, so the Reviews section is not
        // empty on first run. The decision is moved through the allowed
        // transitions rather than having its status set directly.
        relocation.ChangeStatus(DecisionStatus.Evaluating);
        relocation.ChangeStatus(DecisionStatus.Decided);
        relocation.ChangeStatus(DecisionStatus.Monitoring);
        relocation.ChangeStatus(DecisionStatus.Reviewed);

        relocation.AddReview(
            now.AddMonths(-1),
            actualOutcome: "نقل مکان انجام شد. هزینهٔ مسکن ۳۲ درصد کمتر از تهران شد، اما یافتن کار دو ماه بیشتر طول کشید.",
            expectedOutcome: "کاهش ۳۵ درصدی هزینهٔ مسکن و شروع کار در ماه اول.",
            whatWentWell: "هزینهٔ ماهانه واقعاً پایین آمد و شبکهٔ خانوادگی کمک کرد.",
            whatWentWrong: "برآورد زمان پیدا کردن کار خیلی خوش‌بینانه بود.",
            lessonsLearned: "برای نقل مکان، حداقل شش ماه هزینهٔ زندگی را به‌عنوان ذخیره در نظر بگیر.");

        _db.Decisions.AddRange(relocation, business, housing, career);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
