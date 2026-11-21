using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// The approval gate for AI-driven changes.
/// <para>
/// The AI never writes to the domain directly. It creates a proposal, which is
/// inert. Only an explicit user approval moves the proposal to
/// <see cref="AIProposalStatus.Approved"/>, and only then may a caller apply the
/// payload. This service records the decision and the execution attempt; it does
/// not interpret the payload, because interpreting it is the job of the feature
/// that owns the affected entity.
/// </para>
/// </summary>
public sealed class AIProposalService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public AIProposalService(IApplicationDbContext db, IClock clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<AIProposalDto>>> ListAsync(
        Guid? decisionId = null,
        AIProposalStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AIProposals.AsNoTracking();

        if (decisionId is not null)
        {
            query = query.Where(p => p.DecisionId == decisionId);
        }

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        var proposals = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AIProposalDto>>.Success(proposals.Select(Map).ToList());
    }

    public async Task<Result<AIProposalDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _db.AIProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return proposal is null
            ? Result<AIProposalDto>.Failure(ErrorCodes.NotFound, "The proposal could not be found.")
            : Result<AIProposalDto>.Success(Map(proposal));
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A proposal must have a title.");
        }

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Result<Guid>.Failure(
                ErrorCodes.ValidationFailed,
                "A proposal must carry a payload describing the proposed change.");
        }

        if (request.DecisionId is not null)
        {
            var decisionExists = await _db.Decisions
                .AnyAsync(d => d.Id == request.DecisionId, cancellationToken);

            if (!decisionExists)
            {
                return Result<Guid>.Failure(
                    ErrorCodes.NotFound,
                    "The decision this proposal refers to could not be found.");
            }
        }

        var proposal = new AIProposal(
            request.ProposalType,
            request.Title.Trim(),
            request.Description,
            request.PayloadJson,
            request.DecisionId,
            request.EvidenceJson,
            request.Confidence);

        _db.AIProposals.Add(proposal);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(proposal.Id);
    }

    /// <summary>
    /// Approves a pending proposal. Approval is the only path to applying an
    /// AI-suggested change, and it must come from the user.
    /// </summary>
    public async Task<Result> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var proposal = await _db.AIProposals
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (proposal is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The proposal could not be found.");
        }

        try
        {
            proposal.Approve();
        }
        catch (InvalidStateTransitionException ex)
        {
            return Result.Failure(ErrorCodes.Conflict, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid id,
        RejectProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var proposal = await _db.AIProposals
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (proposal is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The proposal could not be found.");
        }

        try
        {
            proposal.Reject(request.Reason);
        }
        catch (InvalidStateTransitionException ex)
        {
            return Result.Failure(ErrorCodes.Conflict, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Records the outcome of applying an approved proposal. A failed execution
    /// marks the proposal failed so it cannot be silently retried as if it had
    /// succeeded.
    /// </summary>
    public async Task<Result<Guid>> RecordExecutionAsync(
        Guid proposalId,
        string operation,
        string inputJson,
        string? outputJson,
        AIExecutionStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return Result<Guid>.Failure(
                ErrorCodes.ValidationFailed,
                "An execution log entry must name the operation.");
        }

        var proposal = await _db.AIProposals
            .FirstOrDefaultAsync(p => p.Id == proposalId, cancellationToken);

        if (proposal is null)
        {
            return Result<Guid>.Failure(ErrorCodes.NotFound, "The proposal could not be found.");
        }

        if (proposal.Status != AIProposalStatus.Approved)
        {
            return Result<Guid>.Failure(
                ErrorCodes.Conflict,
                "Only an approved proposal can be executed. Approve it first.");
        }

        var log = new AIExecutionLog(
            proposalId,
            operation.Trim(),
            inputJson,
            outputJson,
            status,
            errorMessage);

        _db.AIExecutionLogs.Add(log);

        if (status == AIExecutionStatus.Failed)
        {
            proposal.MarkFailed();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(log.Id);
    }

    public async Task<Result<IReadOnlyList<AIExecutionLogDto>>> ListExecutionsAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var logs = await _db.AIExecutionLogs
            .AsNoTracking()
            .Where(l => l.AIProposalId == proposalId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new AIExecutionLogDto(
                l.Id,
                l.AIProposalId,
                l.Operation,
                l.Status,
                l.ErrorMessage,
                l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AIExecutionLogDto>>.Success(logs);
    }

    private static AIProposalDto Map(AIProposal proposal) => new(
        proposal.Id,
        proposal.DecisionId,
        proposal.ProposalType,
        proposal.Title,
        proposal.Description,
        proposal.PayloadJson,
        proposal.EvidenceJson,
        proposal.Confidence,
        proposal.Status,
        proposal.ApprovedAtUtc,
        proposal.RejectedAtUtc,
        proposal.RejectionReason,
        proposal.CreatedAtUtc);
}
