using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// A reusable variable definition (for example "monthly income"). Definitions
/// are shared across decisions; per-scenario values live in
/// <see cref="ScenarioVariable"/>.
/// </summary>
public class VariableDefinition : AuditableEntity, IAggregateRoot
{
    private VariableDefinition()
    {
    }

    public VariableDefinition(
        string key,
        string name,
        VariableCategory category,
        VariableDataType dataType,
        string unit,
        decimal? defaultValue = null,
        decimal? minimumAllowedValue = null,
        decimal? maximumAllowedValue = null,
        string? description = null,
        bool isExternalDataCandidate = false,
        bool isSensitive = false)
    {
        SetKey(key);
        SetName(name);
        Category = category;
        DataType = dataType;
        Unit = unit;
        Description = description;
        IsExternalDataCandidate = isExternalDataCandidate;
        IsSensitive = isSensitive;
        SetBounds(minimumAllowedValue, maximumAllowedValue);

        if (defaultValue is not null)
        {
            DefaultValue = ValidateValue(defaultValue.Value);
        }
    }

    /// <summary>
    /// Stable machine-readable identifier used to bind this definition to a
    /// simulation model's input key (for example <c>monthly_income</c>).
    /// Renaming the display name must never break that binding, so the key is
    /// separate from <see cref="Name"/> and is immutable once created.
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public VariableCategory Category { get; private set; }

    public VariableDataType DataType { get; private set; }

    /// <summary>Free-form unit label, e.g. "IRR/month", "%", "months".</summary>
    public string Unit { get; private set; } = string.Empty;

    public decimal? DefaultValue { get; private set; }

    public decimal? MinimumAllowedValue { get; private set; }

    public decimal? MaximumAllowedValue { get; private set; }

    /// <summary>True when this variable can be populated from external data sources.</summary>
    public bool IsExternalDataCandidate { get; private set; }

    /// <summary>
    /// True when the value is personal or financial. Sensitive variables are
    /// excluded from cloud AI requests unless the user explicitly permits it.
    /// </summary>
    public bool IsSensitive { get; private set; }

    /// <summary>True for variables shipped with the application.</summary>
    public bool IsBuiltIn { get; private set; }

    /// <summary>
    /// Marks this definition as shipped with the application. Called by the
    /// seeder that installs the catalogue the built-in models bind to. A
    /// built-in definition may still be edited, but it cannot be deleted while
    /// a model requires its key.
    /// </summary>
    public void MarkAsBuiltIn()
    {
        IsBuiltIn = true;
    }

    public void Update(
        string name,
        string? description,
        VariableCategory category,
        VariableDataType dataType,
        string unit,
        decimal? defaultValue,
        decimal? minimumAllowedValue,
        decimal? maximumAllowedValue,
        bool isExternalDataCandidate,
        bool isSensitive)
    {
        SetName(name);
        Description = description;
        Category = category;
        DataType = dataType;
        Unit = unit;
        IsExternalDataCandidate = isExternalDataCandidate;
        IsSensitive = isSensitive;
        SetBounds(minimumAllowedValue, maximumAllowedValue);
        DefaultValue = defaultValue is null ? null : ValidateValue(defaultValue.Value);
        Touch();
    }

    /// <summary>
    /// Validates a candidate value against the definition's bounds. Used by
    /// scenario variable assignment so invalid values never reach the engine.
    /// </summary>
    public decimal ValidateValue(decimal value)
    {
        if (MinimumAllowedValue is not null && value < MinimumAllowedValue)
        {
            throw new DomainException($"'{Name}' cannot be less than {MinimumAllowedValue} {Unit}.");
        }

        if (MaximumAllowedValue is not null && value > MaximumAllowedValue)
        {
            throw new DomainException($"'{Name}' cannot be greater than {MaximumAllowedValue} {Unit}.");
        }

        return value;
    }

    private void SetBounds(decimal? minimum, decimal? maximum)
    {
        if (minimum is not null && maximum is not null && maximum < minimum)
        {
            throw new DomainException($"The maximum allowed value of '{Name}' cannot be less than its minimum.");
        }

        MinimumAllowedValue = minimum;
        MaximumAllowedValue = maximum;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A variable definition must have a name.");
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Keys are restricted to lower-case letters, digits and underscores so they
    /// can be used directly as expression identifiers and JSON property names.
    /// </summary>
    private void SetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("A variable definition must have a key.");
        }

        var trimmed = key.Trim();

        foreach (var c in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                throw new DomainException(
                    $"The variable key '{trimmed}' may only contain letters, digits and underscores.");
            }
        }

        if (char.IsAsciiDigit(trimmed[0]))
        {
            throw new DomainException($"The variable key '{trimmed}' cannot start with a digit.");
        }

        Key = trimmed.ToLowerInvariant();
    }
}
