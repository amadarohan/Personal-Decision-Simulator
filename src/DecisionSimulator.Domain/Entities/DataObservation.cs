using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// An immutable record of one observed value with full provenance. Observations
/// are append-only: correcting a value means adding a new observation, never
/// editing history.
/// </summary>
public class DataObservation : Entity
{
    private DataObservation()
    {
    }

    public DataObservation(
        Guid dataSourceId,
        decimal value,
        string unit,
        DateTime observationDate,
        DateTime retrievedAtUtc,
        Guid? indicatorId = null,
        Guid? variableDefinitionId = null,
        string? originalPayloadHash = null,
        VerificationStatus verificationStatus = VerificationStatus.Unverified,
        ConfidenceLevel confidence = ConfidenceLevel.Unknown,
        string? notes = null,
        string? sourceReference = null)
    {
        if (indicatorId is null && variableDefinitionId is null)
        {
            throw new ArgumentException("An observation must reference either an indicator or a variable definition.");
        }

        DataSourceId = dataSourceId;
        Value = value;
        Unit = unit;
        ObservationDate = observationDate;
        RetrievedAtUtc = retrievedAtUtc;
        IndicatorId = indicatorId;
        VariableDefinitionId = variableDefinitionId;
        OriginalPayloadHash = originalPayloadHash;
        VerificationStatus = verificationStatus;
        Confidence = confidence;
        Notes = notes;
        SourceReference = sourceReference;
        ValidFromUtc = retrievedAtUtc;
    }

    public Guid DataSourceId { get; private set; }

    public Guid? IndicatorId { get; private set; }

    public Guid? VariableDefinitionId { get; private set; }

    public decimal Value { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    /// <summary>The period the value describes.</summary>
    public DateTime ObservationDate { get; private set; }

    /// <summary>When the application obtained the value. Distinct from the observation date.</summary>
    public DateTime RetrievedAtUtc { get; private set; }

    /// <summary>Start of the validity window for this observation.</summary>
    public DateTime ValidFromUtc { get; private set; }

    /// <summary>End of the validity window, when the source superseded it.</summary>
    public DateTime? ValidUntilUtc { get; private set; }

    /// <summary>Hash of the original payload, for auditability of imports.</summary>
    public string? OriginalPayloadHash { get; private set; }

    public VerificationStatus VerificationStatus { get; private set; }

    public ConfidenceLevel Confidence { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>URL or document reference for the original source.</summary>
    public string? SourceReference { get; private set; }

    /// <summary>
    /// Closes the validity window when a newer observation supersedes this one.
    /// The row itself is never deleted or rewritten.
    /// </summary>
    public void Supersede(DateTime supersededAtUtc)
    {
        if (ValidUntilUtc is not null)
        {
            return;
        }

        ValidUntilUtc = supersededAtUtc;
    }

    public void MarkVerified(VerificationStatus status, ConfidenceLevel confidence)
    {
        VerificationStatus = status;
        Confidence = confidence;
    }
}
