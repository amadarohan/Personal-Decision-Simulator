namespace DecisionSimulator.Domain.Enums;

public enum DataProviderType
{
    Manual = 0,
    CsvImport = 1,
    JsonImport = 2,
    RestApi = 3,
    WebResearch = 4
}

public enum ReliabilityLevel
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Authoritative = 4
}

public enum VerificationStatus
{
    Unverified = 0,
    SingleSource = 1,
    CrossVerified = 2,
    Disputed = 3,
    Rejected = 4
}

/// <summary>
/// Freshness classification computed from the observation age and the
/// indicator's own freshness policy. Never a single global threshold.
/// </summary>
public enum DataFreshness
{
    Fresh = 0,
    Aging = 1,
    Stale = 2,
    Unknown = 3
}

public enum RiskCategory
{
    Economic = 0,
    Political = 1,
    Regulatory = 2,
    Infrastructure = 3,
    Market = 4,
    Personal = 5,
    Health = 6,
    Legal = 7,
    Operational = 8,
    Other = 99
}

public enum ImpactDirection
{
    Negative = 0,
    Positive = 1,
    Mixed = 2,
    Uncertain = 3
}

public enum GeographicScope
{
    Global = 0,
    Country = 1,
    Region = 2,
    City = 3,
    Neighborhood = 4
}
