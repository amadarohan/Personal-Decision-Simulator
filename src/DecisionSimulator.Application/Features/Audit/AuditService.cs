using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Audit;

/// <summary>
/// Read access to the audit trail plus the backup history.
/// <para>
/// There is deliberately no update or delete method. The audit log is an
/// append-only record, and the persistence guard rejects any attempt to rewrite
/// or remove a row even if one is made directly through the context.
/// </para>
/// </summary>
public sealed class AuditService
{
    private const int MaxPageSize = 500;

    private readonly IApplicationDbContext _db;

    public AuditService(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var take = Math.Clamp(query.Take, 1, MaxPageSize);
        var skip = Math.Max(query.Skip, 0);

        var logs = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            logs = logs.Where(l => l.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            logs = logs.Where(l => l.EntityId == query.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(l => l.Action == query.Action);
        }

        if (query.FromUtc is { } from)
        {
            logs = logs.Where(l => l.CreatedAtUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            logs = logs.Where(l => l.CreatedAtUtc <= to);
        }

        var results = await logs
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(l => new AuditLogDto(
                l.Id,
                l.Action,
                l.EntityType,
                l.EntityId,
                l.DetailsJson,
                l.Actor,
                l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AuditLogDto>>.Success(results);
    }

    /// <summary>
    /// Appends an audit entry. Callers use this for actions the application
    /// performs on the user's behalf, such as approving an AI proposal.
    /// </summary>
    public async Task<Result<Guid>> RecordAsync(
        string action,
        string entityType,
        string? entityId,
        string? detailsJson = null,
        string? actor = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "An action is required.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "An entity type is required.");
        }

        var log = new AuditLog(action.Trim(), entityType.Trim(), entityId, detailsJson, actor);
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(log.Id);
    }

    public async Task<Result<IReadOnlyList<BackupHistoryDto>>> ListBackupsAsync(
        bool? isRestore = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.BackupHistories.AsNoTracking();

        if (isRestore is { } restore)
        {
            query = query.Where(b => b.IsRestore == restore);
        }

        var results = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BackupHistoryDto(
                b.Id,
                b.FileName,
                b.RelativePath,
                b.SizeBytes,
                b.Checksum,
                b.IsRestore,
                b.Succeeded,
                b.Message,
                b.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<BackupHistoryDto>>.Success(results);
    }

    /// <summary>
    /// Records the outcome of a backup or restore. The file itself is written by
    /// the infrastructure layer; this only keeps the history.
    /// </summary>
    public async Task<Result<Guid>> RecordBackupAsync(
        RecordBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A file name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RelativePath))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A relative path is required.");
        }

        if (request.SizeBytes < 0)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A file size cannot be negative.");
        }

        var record = new BackupHistory(
            request.FileName.Trim(),
            request.RelativePath.Trim(),
            request.SizeBytes,
            request.Checksum ?? string.Empty,
            request.IsRestore,
            request.Succeeded,
            request.Message);

        _db.BackupHistories.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(record.Id);
    }
}
