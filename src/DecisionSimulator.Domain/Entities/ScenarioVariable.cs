using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// The value of a variable inside one scenario, together with its uncertainty
/// model and provenance.
/// </summary>
public class ScenarioVariable : AuditableEntity
{
    private ScenarioVariable()
    {
    }

    internal ScenarioVariable(
        Guid scenarioId,
        Guid variableDefinitionId,
        decimal value,
        DistributionType distributionType,
        decimal? minimumValue,
        decimal? maximumValue,
        decimal? standardDeviation,
        ConfidenceLevel confidence,
        SourceType sourceType,
        string? sourceReference,
        DistributionBasis distributionBasis,
        bool isUserOverride)
    {
        ScenarioId = scenarioId;
        VariableDefinitionId = variableDefinitionId;
        Confidence = confidence;
        SourceType = sourceType;
        SourceReference = sourceReference;
        DistributionBasis = distributionBasis;
        IsUserOverride = isUserOverride;
        Apply(value, distributionType, minimumValue, maximumValue, standardDeviation);
    }

    public Guid ScenarioId { get; private set; }

    public Guid VariableDefinitionId { get; private set; }

    /// <summary>Central / most-likely value. For <see cref="DistributionType.Fixed"/> this is the value.</summary>
    public decimal Value { get; private set; }

    public decimal? MinimumValue { get; private set; }

    public decimal? MaximumValue { get; private set; }

    /// <summary>Standard deviation for normal distributions, sigma for log-normal.</summary>
    public decimal? StandardDeviation { get; private set; }

    public DistributionType DistributionType { get; private set; }

    /// <summary>How much the user trusts this value. Qualitative, never a probability.</summary>
    public ConfidenceLevel Confidence { get; private set; }

    public SourceType SourceType { get; private set; }

    /// <summary>Free-form provenance: a URL, a document name, or a note.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>
    /// Whether the distribution parameters are empirical, user-defined, expert
    /// judgement, or purely exploratory. Surfaced in the UI.
    /// </summary>
    public DistributionBasis DistributionBasis { get; private set; }

    /// <summary>True when the user deliberately overrode an inherited or external value.</summary>
    public bool IsUserOverride { get; private set; }

    /// <summary>Weighted outcomes for <see cref="DistributionType.Discrete"/>.</summary>
    public string? DiscreteOutcomesJson { get; private set; }

    internal void Update(
        decimal value,
        DistributionType distributionType,
        decimal? minimumValue,
        decimal? maximumValue,
        decimal? standardDeviation,
        ConfidenceLevel confidence,
        SourceType sourceType,
        string? sourceReference,
        DistributionBasis distributionBasis,
        bool isUserOverride)
    {
        Confidence = confidence;
        SourceType = sourceType;
        SourceReference = sourceReference;
        DistributionBasis = distributionBasis;
        IsUserOverride = isUserOverride;
        Apply(value, distributionType, minimumValue, maximumValue, standardDeviation);
        Touch();
    }

    public void SetDiscreteOutcomes(string? discreteOutcomesJson)
    {
        if (DistributionType != DistributionType.Discrete && discreteOutcomesJson is not null)
        {
            throw new DomainException("Discrete outcomes can only be set on a discrete distribution.");
        }

        DiscreteOutcomesJson = discreteOutcomesJson;
        Touch();
    }

    /// <summary>
    /// Validates and stores the distribution parameters. Invalid parameter
    /// combinations are rejected here so the simulation engine can assume
    /// well-formed input.
    /// </summary>
    private void Apply(
        decimal value,
        DistributionType distributionType,
        decimal? minimumValue,
        decimal? maximumValue,
        decimal? standardDeviation)
    {
        DistributionType = distributionType;

        switch (distributionType)
        {
            case DistributionType.Fixed:
                Value = value;
                MinimumValue = null;
                MaximumValue = null;
                StandardDeviation = null;
                break;

            case DistributionType.Uniform:
                RequireRange(minimumValue, maximumValue);
                Value = value;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
                StandardDeviation = null;
                break;

            case DistributionType.Normal:
                if (standardDeviation is null || standardDeviation <= 0m)
                {
                    throw new InvalidDistributionException("A normal distribution requires a positive standard deviation.");
                }

                Value = value;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
                StandardDeviation = standardDeviation;
                break;

            case DistributionType.LogNormal:
                if (value <= 0m)
                {
                    throw new InvalidDistributionException("A log-normal distribution requires a positive median value.");
                }

                if (standardDeviation is null || standardDeviation <= 0m)
                {
                    throw new InvalidDistributionException("A log-normal distribution requires a positive sigma.");
                }

                Value = value;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
                StandardDeviation = standardDeviation;
                break;

            case DistributionType.Triangular:
                RequireRange(minimumValue, maximumValue);

                if (value < minimumValue || value > maximumValue)
                {
                    throw new InvalidDistributionException("The triangular mode must lie between the minimum and the maximum.");
                }

                Value = value;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
                StandardDeviation = null;
                break;

            case DistributionType.Discrete:
            case DistributionType.Custom:
                Value = value;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
                StandardDeviation = standardDeviation;
                break;

            default:
                throw new InvalidDistributionException($"Unsupported distribution type '{distributionType}'.");
        }
    }

    private static void RequireRange(decimal? minimum, decimal? maximum)
    {
        if (minimum is null || maximum is null)
        {
            throw new InvalidDistributionException("This distribution requires both a minimum and a maximum value.");
        }

        if (maximum < minimum)
        {
            throw new InvalidDistributionException("The maximum value cannot be less than the minimum value.");
        }
    }
}
