using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Web.Localization;

/// <summary>
/// Turns domain enum values into display text in the active language.
/// <para>
/// The domain enums are persisted as strings and are part of the data contract,
/// so they are never renamed for display. Translation happens here, at the edge,
/// which keeps the stored values stable while the interface reads naturally in
/// either language.
/// </para>
/// <para>
/// Labels are grouped per enum type rather than in one flat table. Several enums
/// share a member name (Low, High, Unknown, Other, Custom, Cancelled, StressTest,
/// MonteCarlo, Draft), and a flat table would either collide at initialisation or
/// force one translation to serve unrelated concepts.
/// </para>
/// </summary>
public static class EnumLabels
{
    /// <summary>
    /// Returns the label for an enum value. An unmapped value falls back to the
    /// enum name so a newly added member is visible rather than blank.
    /// </summary>
    public static string Label<TEnum>(TEnum value, bool persian)
        where TEnum : struct, Enum
    {
        var name = value.ToString();

        if (!persian)
        {
            return name;
        }

        return Persian.TryGetValue(typeof(TEnum), out var labels)
            && labels.TryGetValue(name, out var translated)
                ? translated
                : name;
    }

    private static readonly Dictionary<Type, Dictionary<string, string>> Persian = new()
    {
        [typeof(DecisionCategory)] = new()
        {
            ["Career"] = "شغلی",
            ["Finance"] = "مالی",
            ["Investment"] = "سرمایه‌گذاری",
            ["Business"] = "کسب‌وکار",
            ["Housing"] = "مسکن",
            ["Relocation"] = "نقل مکان",
            ["Education"] = "تحصیل",
            ["Relationship"] = "روابط",
            ["HealthAndLifestyle"] = "سلامت و سبک زندگی",
            ["Family"] = "خانواده",
            ["Technology"] = "فناوری",
            ["Other"] = "سایر",
        },

        [typeof(DecisionStatus)] = new()
        {
            ["Draft"] = "پیش‌نویس",
            ["Exploring"] = "در حال بررسی",
            ["Modeling"] = "در حال مدل‌سازی",
            ["Evaluating"] = "در حال ارزیابی",
            ["Decided"] = "تصمیم‌گیری‌شده",
            ["Monitoring"] = "در حال پایش",
            ["Reviewed"] = "بازبینی‌شده",
            ["Cancelled"] = "لغوشده",
        },

        [typeof(ImportanceLevel)] = new()
        {
            ["Low"] = "کم",
            ["Moderate"] = "متوسط",
            ["High"] = "زیاد",
            ["Critical"] = "بحرانی",
        },

        [typeof(DecisionHorizon)] = new()
        {
            ["ShortTerm"] = "کوتاه‌مدت",
            ["MediumTerm"] = "میان‌مدت",
            ["LongTerm"] = "بلندمدت",
            ["VeryLongTerm"] = "بسیار بلندمدت",
        },

        [typeof(RiskTolerance)] = new()
        {
            ["VeryLow"] = "بسیار کم",
            ["Low"] = "کم",
            ["Moderate"] = "متوسط",
            ["High"] = "زیاد",
            ["VeryHigh"] = "بسیار زیاد",
        },

        [typeof(CalendarSystem)] = new()
        {
            ["Gregorian"] = "میلادی",
            ["Jalali"] = "شمسی",
        },

        [typeof(CurrencyUnitConvention)] = new()
        {
            ["BaseUnit"] = "واحد پایه",
            ["Toman"] = "تومان",
        },

        [typeof(ConfidenceLevel)] = new()
        {
            ["Unknown"] = "نامشخص",
            ["Low"] = "کم",
            ["Medium"] = "متوسط",
            ["High"] = "زیاد",
        },

        [typeof(ScenarioType)] = new()
        {
            ["Baseline"] = "پایه",
            ["Optimistic"] = "خوش‌بینانه",
            ["Expected"] = "انتظاری",
            ["Pessimistic"] = "بدبینانه",
            ["StressTest"] = "آزمون تنش",
            ["Custom"] = "سفارشی",
            ["MonteCarlo"] = "مونت‌کارلو",
        },

        [typeof(ScenarioStatus)] = new()
        {
            ["Draft"] = "پیش‌نویس",
            ["Ready"] = "آماده",
            ["Simulated"] = "شبیه‌سازی‌شده",
            ["Archived"] = "بایگانی‌شده",
        },

        [typeof(DistributionType)] = new()
        {
            ["Fixed"] = "ثابت",
            ["Uniform"] = "یکنواخت",
            ["Normal"] = "نرمال",
            ["LogNormal"] = "لگ‌نرمال",
            ["Triangular"] = "مثلثی",
            ["Discrete"] = "گسسته",
            ["Custom"] = "سفارشی",
        },

        [typeof(SourceType)] = new()
        {
            ["UserInput"] = "ورودی کاربر",
            ["ExternalObservation"] = "مشاهده بیرونی",
            ["AIProposed"] = "پیشنهاد دستیار",
            ["DefinitionDefault"] = "پیش‌فرض تعریف",
            ["Imported"] = "واردشده",
            ["Derived"] = "استخراج‌شده",
        },

        [typeof(DistributionBasis)] = new()
        {
            ["Empirical"] = "تجربی",
            ["UserDefined"] = "تعریف کاربر",
            ["ExpertAssumption"] = "فرض کارشناس",
            ["Exploratory"] = "اکتشافی",
        },

        [typeof(SimulationRunType)] = new()
        {
            ["Deterministic"] = "قطعی",
            ["MonteCarlo"] = "مونت‌کارلو",
            ["Sensitivity"] = "حساسیت",
            ["StressTest"] = "آزمون تنش",
        },

        [typeof(SimulationRunStatus)] = new()
        {
            ["Pending"] = "در انتظار",
            ["Running"] = "در حال اجرا",
            ["Completed"] = "کامل‌شده",
            ["Failed"] = "ناموفق",
            ["Cancelled"] = "لغوشده",
        },

        [typeof(MetricUnit)] = new()
        {
            ["Currency"] = "ارز",
            ["Percentage"] = "درصد",
            ["Count"] = "تعداد",
            ["Months"] = "ماه",
            ["Ratio"] = "نسبت",
            ["Index"] = "شاخص",
        },

        [typeof(VariableCategory)] = new()
        {
            ["Personal"] = "شخصی",
            ["Financial"] = "مالی",
            ["Economic"] = "اقتصادی",
            ["Geographic"] = "جغرافیایی",
            ["Career"] = "شغلی",
            ["Business"] = "کسب‌وکار",
            ["Political"] = "سیاسی",
            ["Infrastructure"] = "زیرساخت",
            ["Market"] = "بازار",
            ["Custom"] = "سفارشی",
        },

        [typeof(VariableDataType)] = new()
        {
            ["Decimal"] = "اعشاری",
            ["Integer"] = "صحیح",
            ["Percentage"] = "درصد",
            ["Currency"] = "ارز",
            ["Boolean"] = "بولی",
            ["Text"] = "متنی",
            ["Date"] = "تاریخ",
        },

        [typeof(DataProviderType)] = new()
        {
            ["Manual"] = "دستی",
            ["CsvImport"] = "ورود CSV",
            ["JsonImport"] = "ورود JSON",
            ["RestApi"] = "REST API",
            ["WebResearch"] = "پژوهش وب",
        },

        [typeof(ReliabilityLevel)] = new()
        {
            ["Unknown"] = "نامشخص",
            ["Low"] = "کم",
            ["Medium"] = "متوسط",
            ["High"] = "زیاد",
            ["Authoritative"] = "مرجع",
        },

        [typeof(VerificationStatus)] = new()
        {
            ["Unverified"] = "تأییدنشده",
            ["SingleSource"] = "تک‌منبع",
            ["CrossVerified"] = "تأیید متقابل",
            ["Disputed"] = "مورد اختلاف",
            ["Rejected"] = "ردشده",
        },

        [typeof(DataFreshness)] = new()
        {
            ["Fresh"] = "تازه",
            ["Aging"] = "در حال کهنگی",
            ["Stale"] = "کهنه",
            ["Unknown"] = "نامشخص",
        },

        [typeof(RiskCategory)] = new()
        {
            ["Economic"] = "اقتصادی",
            ["Political"] = "سیاسی",
            ["Regulatory"] = "مقرراتی",
            ["Infrastructure"] = "زیرساخت",
            ["Market"] = "بازار",
            ["Personal"] = "شخصی",
            ["Health"] = "سلامت",
            ["Legal"] = "حقوقی",
            ["Operational"] = "عملیاتی",
            ["Other"] = "سایر",
        },

        [typeof(ImpactDirection)] = new()
        {
            ["Negative"] = "منفی",
            ["Positive"] = "مثبت",
            ["Mixed"] = "ترکیبی",
            ["Uncertain"] = "نامعلوم",
        },

        [typeof(GeographicScope)] = new()
        {
            ["Global"] = "جهانی",
            ["Country"] = "کشور",
            ["Region"] = "منطقه",
            ["City"] = "شهر",
            ["Neighborhood"] = "محله",
        },

        [typeof(AIMessageRole)] = new()
        {
            ["System"] = "سیستم",
            ["User"] = "کاربر",
            ["Assistant"] = "دستیار",
            ["Tool"] = "ابزار",
        },

        [typeof(AIContextType)] = new()
        {
            ["Global"] = "عمومی",
            ["Decision"] = "تصمیم",
            ["Scenario"] = "سناریو",
            ["SimulationRun"] = "اجرای شبیه‌سازی",
            ["EconomicContext"] = "زمینه اقتصادی",
        },

        [typeof(AIRole)] = new()
        {
            ["DecisionAnalyst"] = "تحلیل‌گر تصمیم",
            ["ScenarioArchitect"] = "معمار سناریو",
            ["RiskAnalyst"] = "تحلیل‌گر ریسک",
            ["ResearchAnalyst"] = "تحلیل‌گر پژوهش",
            ["SimulationInterpreter"] = "تفسیرگر شبیه‌سازی",
            ["StrategicAdvisor"] = "مشاور راهبردی",
        },

        [typeof(DataSensitivity)] = new()
        {
            ["Public"] = "عمومی",
            ["Internal"] = "داخلی",
            ["Personal"] = "شخصی",
            ["Financial"] = "مالی",
            ["HighlySensitive"] = "بسیار حساس",
        },
    };
}
