namespace DecisionSimulator.Application.Features.Audit;

public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    string? EntityId,
    string? DetailsJson,
    string? Actor,
    DateTime CreatedAtUtc);

public sealed record AuditQuery(
    string? EntityType = null,
    string? EntityId = null,
    string? Action = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Skip = 0,
    int Take = 100);

public sealed record BackupHistoryDto(
    Guid Id,
    string FileName,
    string RelativePath,
    long SizeBytes,
    string Checksum,
    bool IsRestore,
    bool Succeeded,
    string? Message,
    DateTime CreatedAtUtc);

public sealed record RecordBackupRequest(
    string FileName,
    string RelativePath,
    long SizeBytes,
    string Checksum,
    bool IsRestore,
    bool Succeeded,
    string? Message = null);
