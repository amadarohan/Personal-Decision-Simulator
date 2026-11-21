namespace DecisionSimulator.Infrastructure.Persistence;

/// <summary>
/// Column precision conventions, kept in one place so that a money column is
/// never accidentally created with a different scale than its neighbours.
/// <para>
/// Money uses <c>decimal(18,2)</c>. Rates, coefficients and statistical outputs
/// use <c>decimal(18,6)</c>: rounding a monthly growth rate to two decimals
/// would visibly distort a multi-year projection, so the wider scale is a
/// correctness requirement rather than a preference.
/// </para>
/// </summary>
public static class ColumnPrecision
{
    /// <summary>Currency amounts entered or displayed to the user.</summary>
    public const string Money = "decimal(18,2)";

    /// <summary>Rates, coefficients, standard deviations and computed statistics.</summary>
    public const string Rate = "decimal(18,6)";

    /// <summary>UTC timestamps. Millisecond precision is enough for ordering.</summary>
    public const string Timestamp = "datetime2(3)";
}
