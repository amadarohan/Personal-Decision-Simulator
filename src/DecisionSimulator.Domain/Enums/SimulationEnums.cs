namespace DecisionSimulator.Domain.Enums;

// CA1720 flags identifiers that match a type name. These are the correct domain
// words for the data types a variable can hold, and renaming them would make the
// persisted values and the UI labels less clear, not more.
#pragma warning disable CA1720

public enum VariableDataType
{
    Decimal = 0,
    Integer = 1,
    Percentage = 2,
    Currency = 3,
    Boolean = 4,
    Text = 5,
    Date = 6
}

public enum VariableCategory
{
    Personal = 0,
    Financial = 1,
    Economic = 2,
    Geographic = 3,
    Career = 4,
    Business = 5,
    Political = 6,
    Infrastructure = 7,
    Market = 8,
    Custom = 99
}

public enum ModelType
{
    /// <summary>Monthly cash-flow projection with growth and inflation.</summary>
    CashFlowProjection = 0,

    /// <summary>Loan / installment amortisation schedule.</summary>
    DebtAmortization = 1,

    /// <summary>Business revenue versus operating cost viability.</summary>
    BusinessViability = 2,

    /// <summary>Side-by-side cost-of-living comparison between locations.</summary>
    RelocationComparison = 3,

    /// <summary>User-authored formula evaluated by the safe expression engine.</summary>
    Formula = 4
}

public enum SimulationRunType
{
    Deterministic = 0,
    MonteCarlo = 1,
    Sensitivity = 2,
    StressTest = 3
}

public enum SimulationRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>
/// Unit of a reported metric. Kept as an enum for the built-in models so that
/// formatting is consistent; free-form units live on variable definitions.
/// </summary>
public enum MetricUnit
{
    Currency = 0,
    Percentage = 1,
    Count = 2,
    Months = 3,
    Ratio = 4,
    Index = 5
}
