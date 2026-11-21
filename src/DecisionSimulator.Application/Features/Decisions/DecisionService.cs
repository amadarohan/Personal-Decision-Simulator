using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Decisions;

/// <summary>
/// Read and write operations for decisions and their alternatives.
/// <para>
/// Every method returns DTOs, never entities. The web layer therefore cannot
/// hold a tracked entity across a render and accidentally persist a change the
/// user did not confirm.
/// </para>
/// </summary>
public sealed class DecisionService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public DecisionService(IApplicationDbContext db, IClock clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<IReadOnlyList<DecisionSummaryDto>> ListAsync(
        DecisionStatus? status = null,
        DecisionCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Decisions.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(d => d.Status == status);
        }

        if (category is not null)
        {
            query = query.Where(d => d.Category == category);
        }

        return await query
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Select(d => new DecisionSummaryDto(
                d.Id,
                d.Title,
                d.Category,
                d.Status,
                d.Importance,
                d.Horizon,
                d.Alternatives.Count,
                d.Scenarios.Count,
                d.DecisionDate,
                d.TargetDecisionDate,
                d.ReviewScheduledForUtc,
                d.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<DecisionDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .AsNoTracking()
            .Include(d => d.Alternatives)
            .Include(d => d.Scenarios)
                .ThenInclude(s => s.Variables)
            .Include(d => d.Scenarios)
                .ThenInclude(s => s.Runs)
            .Include(d => d.Reviews)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (decision is null)
        {
            return Result<DecisionDetailDto>.Failure(
                ErrorCodes.NotFound,
                "The decision could not be found.");
        }

        return Result<DecisionDetailDto>.Success(MapDetail(decision));
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Decision decision;

        try
        {
            decision = new Decision(
                request.Title,
                request.Category,
                request.Description,
                request.Importance,
                request.Horizon,
                request.RiskTolerance);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        _db.Decisions.Add(decision);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(decision.Id);
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        UpdateDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decision = await _db.Decisions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (decision is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        try
        {
            decision.UpdateDetails(
                request.Title,
                request.Description,
                request.Category,
                request.Importance,
                request.Horizon,
                request.RiskTolerance,
                request.Context,
                request.DesiredOutcome,
                request.ConstraintsJson,
                request.TargetDecisionDate);
        }
        catch (DomainException ex)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Moves the decision to a new status. The domain rejects transitions that
    /// would skip a stage, so the caller gets a clear message instead of a
    /// silently inconsistent workflow.
    /// </summary>
    public async Task<Result> ChangeStatusAsync(
        Guid id,
        DecisionStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (decision is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        try
        {
            decision.ChangeStatus(newStatus);
        }
        catch (InvalidStateTransitionException ex)
        {
            return Result.Failure(ErrorCodes.InvalidState, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ScheduleReviewAsync(
        Guid id,
        DateTime reviewDateUtc,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (decision is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        if (reviewDateUtc <= _clock.UtcNow)
        {
            return Result.Failure(
                ErrorCodes.ValidationFailed,
                "A review date must be in the future. Reviewing a decision before it has played out cannot produce a useful lesson.");
        }

        decision.ScheduleReview(reviewDateUtc);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> AddAlternativeAsync(
        Guid decisionId,
        CreateAlternativeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decision = await _db.Decisions
            .Include(d => d.Alternatives)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result<Guid>.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        DecisionAlternative alternative;

        try
        {
            alternative = decision.AddAlternative(
                request.Name,
                request.Description,
                request.IsBaseline,
                request.EstimatedInitialCost,
                request.EstimatedRecurringCost,
                request.ExpectedBenefits,
                request.KnownRisks);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(alternative.Id);
    }

    public async Task<Result> RemoveAlternativeAsync(
        Guid decisionId,
        Guid alternativeId,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .Include(d => d.Alternatives)
            .Include(d => d.Scenarios)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        // Removing an alternative that scenarios still reference would leave
        // those scenarios pointing at nothing, so the user must resolve them first.
        if (decision.Scenarios.Any(s => s.AlternativeId == alternativeId))
        {
            return Result.Failure(
                ErrorCodes.Conflict,
                "This alternative is still used by one or more scenarios. Remove or reassign those scenarios first.");
        }

        try
        {
            decision.RemoveAlternative(alternativeId);
        }
        catch (EntityNotFoundException ex)
        {
            return Result.Failure(ErrorCodes.NotFound, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<Guid>> AddReviewAsync(
        Guid decisionId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decision = await _db.Decisions
            .Include(d => d.Reviews)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result<Guid>.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        DecisionReview review;

        try
        {
            review = decision.AddReview(
                request.ReviewDateUtc,
                request.ActualOutcome,
                request.ExpectedOutcome,
                request.WhatWentWell,
                request.WhatWentWrong,
                request.LessonsLearned);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(review.Id);
    }

    private static DecisionDetailDto MapDetail(Decision decision) => new(
        decision.Id,
        decision.Title,
        decision.Description,
        decision.Category,
        decision.Status,
        decision.Importance,
        decision.Horizon,
        decision.RiskTolerance,
        decision.DecisionDate,
        decision.TargetDecisionDate,
        decision.Context,
        decision.DesiredOutcome,
        decision.ConstraintsJson,
        decision.ReviewScheduledForUtc,
        decision.CreatedAtUtc,
        decision.UpdatedAtUtc,
        decision.Alternatives
            .OrderByDescending(a => a.IsBaseline)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new DecisionAlternativeDto(
                a.Id,
                a.Name,
                a.Description,
                a.IsBaseline,
                a.EstimatedInitialCost,
                a.EstimatedRecurringCost,
                a.ExpectedBenefits,
                a.KnownRisks))
            .ToList(),
        decision.Scenarios
            .OrderByDescending(s => s.IsBaseline)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new ScenarioSummaryDto(
                s.Id,
                s.Name,
                s.Type,
                s.Status,
                s.TimeHorizonMonths,
                s.AlternativeId,
                s.ParentScenarioId,
                s.IsBaseline,
                s.ProbabilityWeight,
                s.AssumptionSetVersion,
                s.Variables.Count,
                s.Runs.Count,
                s.Runs
                    .Where(r => r.CompletedAtUtc is not null)
                    .OrderByDescending(r => r.CompletedAtUtc)
                    .Select(r => r.CompletedAtUtc)
                    .FirstOrDefault(),
                s.UpdatedAtUtc))
            .ToList(),
        decision.Reviews
            .OrderByDescending(r => r.ReviewDate)
            .Select(r => new DecisionReviewDto(
                r.Id,
                r.ReviewDate,
                r.ActualOutcome,
                r.ExpectedOutcome,
                r.OutcomeDifference,
                r.WhatWentWell,
                r.WhatWentWrong,
                r.LessonsLearned))
            .ToList());
}
