namespace DecisionSimulator.Domain.Enums;

public enum DecisionCategory
{
    Career = 0,
    Finance = 1,
    Investment = 2,
    Business = 3,
    Housing = 4,
    Relocation = 5,
    Education = 6,
    Relationship = 7,
    HealthAndLifestyle = 8,
    Family = 9,
    Technology = 10,
    Other = 99
}

public enum DecisionStatus
{
    Draft = 0,
    Exploring = 1,
    Modeling = 2,
    Evaluating = 3,
    Decided = 4,
    Monitoring = 5,
    Reviewed = 6,
    Cancelled = 7
}

/// <summary>How much the outcome matters to the user. Drives UI emphasis, not scoring.</summary>
public enum ImportanceLevel
{
    Low = 0,
    Moderate = 1,
    High = 2,
    Critical = 3
}

/// <summary>How far ahead the decision is expected to play out.</summary>
public enum DecisionHorizon
{
    ShortTerm = 0,
    MediumTerm = 1,
    LongTerm = 2,
    VeryLongTerm = 3
}

/// <summary>
/// Self-reported tolerance for downside risk. Used to frame trade-offs; it is
/// never converted into a hidden numeric score.
/// </summary>
public enum RiskTolerance
{
    VeryLow = 0,
    Low = 1,
    Moderate = 2,
    High = 3,
    VeryHigh = 4
}

public enum CalendarSystem
{
    Gregorian = 0,
    Jalali = 1
}

/// <summary>
/// How the user wants Iranian Rial amounts displayed. IRR is the base unit,
/// IRT (Toman) is IRR / 10.
/// </summary>
public enum CurrencyUnitConvention
{
    /// <summary>Display the currency in its base unit (e.g. IRR).</summary>
    BaseUnit = 0,

    /// <summary>Display the currency divided by ten (e.g. IRT / Toman for IRR).</summary>
    Toman = 1
}

public enum ConfidenceLevel
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3
}
