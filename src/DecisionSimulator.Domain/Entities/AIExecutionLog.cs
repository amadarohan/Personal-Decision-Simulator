using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// Audit record of an operation performed as a result of an approved AI
/// proposal. Every persistent AI-driven mutation leaves one of these rows.
/// </summary>
public class AIExecutionLog : Entity
{
    private AIExecutionLog()
    {
    }

    public AIExecutionLog(
        Guid aiProposalId,
        string operation,
        string inputJson,
        string? outputJson,
        AIExecutionStatus status,
        string? errorMessage = null)
    {
        AIProposalId = aiProposalId;
        Operation = operation;
        InputJson = inputJson;
        OutputJson = outputJson;
        Status = status;
        ErrorMessage = errorMessage;
    }

    public Guid AIProposalId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string InputJson { get; private set; } = "{}";

    public string? OutputJson { get; private set; }

    public AIExecutionStatus Status { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
}
