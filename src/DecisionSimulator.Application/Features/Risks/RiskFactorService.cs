using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Risks;

/// <summary>
/// The risk register. Risks are recorded as qualitative uncertainties with a
/// provenance reference; the service never derives a probability from them.
/// </summary>
public sealed class RiskFactorService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;

    public RiskFactorService(IApplicationDbContext db, IClock clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<RiskFactorDto>>> ListAsync(
        bool includeInactive = false,
        RiskCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.RiskFactors.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        if (category is not null)
        {
            query = query.Where(r => r.Category == category);
        }

        var risks = await query
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<RiskFactorDto>>.Success(risks.Select(Map).ToList());
    }

    public async Task<Result<RiskFactorDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var risk = await _db.RiskFactors
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return risk is null
            ? Result<RiskFactorDto>.Failure(ErrorCodes.NotFound, "The risk factor could not be found.")
            : Result<RiskFactorDto>.Success(Map(risk));
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateRiskFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A risk factor must have a name.");
        }

        var risk = new RiskFactor(
            request.Name.Trim(),
            request.Category,
            request.GeographicScope,
            request.ImpactDirection,
            request.Description,
            request.ImpactMagnitude,
            request.LikelihoodAssessment,
            request.Confidence,
            request.SourceReference);

        _db.RiskFactors.Add(risk);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(risk.Id);
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        UpdateRiskFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var risk = await _db.RiskFactors.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (risk is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The risk factor could not be found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, "A risk factor must have a name.");
        }

        risk.Update(
            request.Name.Trim(),
            request.Description,
            request.Category,
            request.GeographicScope,
            request.ImpactDirection,
            request.ImpactMagnitude,
            request.LikelihoodAssessment,
            request.Confidence,
            request.SourceReference);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Records that the user re-checked this risk against its source. The
    /// timestamp is what the UI uses to warn that an assessment has aged.
    /// </summary>
    public async Task<Result> MarkVerifiedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var risk = await _db.RiskFactors.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (risk is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The risk factor could not be found.");
        }

        risk.MarkVerified(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var risk = await _db.RiskFactors.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (risk is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The risk factor could not be found.");
        }

        if (isActive)
        {
            risk.Activate();
        }
        else
        {
            risk.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Deletes a risk. Unlike a variable definition, nothing binds to a risk by
    /// key, so removal is safe; deactivating is still preferred for history.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var risk = await _db.RiskFactors.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (risk is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The risk factor could not be found.");
        }

        _db.RiskFactors.Remove(risk);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static RiskFactorDto Map(RiskFactor risk) =>
        new(
            risk.Id,
            risk.Name,
            risk.Description,
            risk.Category,
            risk.GeographicScope,
            risk.ImpactDirection,
            risk.ImpactMagnitude,
            risk.LikelihoodAssessment,
            risk.Confidence,
            risk.SourceReference,
            risk.LastVerifiedAtUtc,
            risk.IsActive);
}
