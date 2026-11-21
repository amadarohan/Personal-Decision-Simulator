using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// Durable, user-visible facts and preferences the assistant may use as context.
/// Nothing is remembered silently: every entry is listed, editable, and
/// deactivatable by the user.
/// </summary>
public sealed class AIMemoryService
{
    private readonly IApplicationDbContext _db;

    public AIMemoryService(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<IReadOnlyList<AIMemoryDto>>> ListAsync(
        bool includeInactive = false,
        AIMemoryType? memoryType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AIMemories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        if (memoryType is not null)
        {
            query = query.Where(m => m.MemoryType == memoryType);
        }

        var memories = await query
            .OrderBy(m => m.MemoryType)
            .ThenByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AIMemoryDto>>.Success(memories.Select(Map).ToList());
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateMemoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A memory cannot be empty.");
        }

        var memory = new AIMemory(
            request.MemoryType,
            request.Content.Trim(),
            request.SourceReference,
            request.Confidence);

        _db.AIMemories.Add(memory);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(memory.Id);
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        UpdateMemoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, "A memory cannot be empty.");
        }

        var memory = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (memory is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The memory could not be found.");
        }

        memory.Update(request.Content.Trim(), request.Confidence, request.SourceReference);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Marks a memory as confirmed by the user. Only validated memories should be
    /// treated as reliable context.
    /// </summary>
    public async Task<Result> ValidateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var memory = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (memory is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The memory could not be found.");
        }

        memory.Validate();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var memory = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (memory is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The memory could not be found.");
        }

        if (isActive)
        {
            // The domain only exposes Deactivate; reactivation is a new memory.
            return Result.Failure(
                ErrorCodes.Conflict,
                "A deactivated memory cannot be reactivated. Create a new memory instead.");
        }

        memory.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var memory = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (memory is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The memory could not be found.");
        }

        _db.AIMemories.Remove(memory);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static AIMemoryDto Map(AIMemory memory) => new(
        memory.Id,
        memory.MemoryType,
        memory.Content,
        memory.SourceReference,
        memory.Confidence,
        memory.IsValidated,
        memory.IsActive,
        memory.CreatedAtUtc);
}
