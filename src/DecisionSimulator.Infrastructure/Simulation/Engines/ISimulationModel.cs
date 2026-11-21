using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Infrastructure.Simulation.Engines;

/// <summary>
/// The resolved inputs for one simulation path. Variable keys are the stable
/// codes used by models (for example <c>monthly_income</c>), not display names,
/// so renaming a variable in the UI cannot silently break a model.
/// </summary>
public sealed class SimulationInputs
{
    private readonly Dictionary<string, decimal> _values;

    public SimulationInputs(IReadOnlyDictionary<string, decimal> values, int timeHorizonMonths, string currencyCode)
    {
        _values = new Dictionary<string, decimal>(values, StringComparer.OrdinalIgnoreCase);
        TimeHorizonMonths = timeHorizonMonths;
        CurrencyCode = currencyCode;
    }

    public int TimeHorizonMonths { get; }

    public string CurrencyCode { get; }

    public IReadOnlyDictionary<string, decimal> Values => _values;

    public decimal Get(string key)
    {
        if (_values.TryGetValue(key, out var value))
        {
            return value;
        }

        throw new MissingInputException(key);
    }

    public decimal GetOrDefault(string key, decimal fallback)
        => _values.TryGetValue(key, out var value) ? value : fallback;

    public bool Has(string key) => _values.ContainsKey(key);
}

/// <summary>
/// Raised when a model needs an input that the scenario did not provide. This is
/// surfaced to the user as a concrete "missing assumption" rather than a generic
/// failure, because the fix is to supply the value.
/// </summary>
public sealed class MissingInputException(string key)
    : Exception($"The model requires the input '{key}', which this scenario does not define.")
{
    public string Key { get; } = key;
}

/// <summary>
/// One month of a projected path. Models emit these and the engine aggregates
/// them into time series and summary statistics.
/// </summary>
public sealed record MonthlyProjection(
    int MonthIndex,
    decimal Income,
    decimal Expenses,
    decimal NetCashFlow,
    decimal CumulativeCash,
    decimal? DebtBalance = null);

/// <summary>
/// The output of evaluating a model for a single set of inputs.
/// </summary>
public sealed class ModelOutput
{
    public ModelOutput(
        IReadOnlyList<MonthlyProjection> months,
        IReadOnlyDictionary<string, decimal> summaryMetrics,
        IReadOnlyList<string> warnings)
    {
        Months = months;
        SummaryMetrics = summaryMetrics;
        Warnings = warnings;
    }

    public IReadOnlyList<MonthlyProjection> Months { get; }

    /// <summary>Terminal metrics such as final cash, minimum cash, or months to depletion.</summary>
    public IReadOnlyDictionary<string, decimal> SummaryMetrics { get; }

    /// <summary>Model-level caveats that must be shown alongside the result.</summary>
    public IReadOnlyList<string> Warnings { get; }
}

/// <summary>
/// A deterministic calculation model. Implementations must be pure: the same
/// inputs always produce the same output, with no clock, I/O, or randomness.
/// </summary>
public interface ISimulationModel
{
    /// <summary>Stable identifier used in persisted runs.</summary>
    string ModelKey { get; }

    /// <summary>Semantic version. Bump whenever the maths changes.</summary>
    string ModelVersion { get; }

    ModelType ModelType { get; }

    /// <summary>Input keys this model requires. Used for pre-flight validation.</summary>
    IReadOnlyCollection<string> RequiredInputs { get; }

    /// <summary>Human-readable description of the assumptions the model makes.</summary>
    IReadOnlyCollection<string> Assumptions { get; }

    ModelOutput Evaluate(SimulationInputs inputs);
}
