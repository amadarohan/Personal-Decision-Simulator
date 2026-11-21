using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// Record of a backup or restore operation, including validation outcome.
/// </summary>
public class BackupHistory : Entity
{
    private BackupHistory()
    {
    }

    public BackupHistory(
        string fileName,
        string relativePath,
        long sizeBytes,
        string checksum,
        bool isRestore,
        bool succeeded,
        string? message = null)
    {
        FileName = fileName;
        RelativePath = relativePath;
        SizeBytes = sizeBytes;
        Checksum = checksum;
        IsRestore = isRestore;
        Succeeded = succeeded;
        Message = message;
    }

    public string FileName { get; private set; } = string.Empty;

    public string RelativePath { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Checksum { get; private set; } = string.Empty;

    /// <summary>True when this record describes a restore rather than a backup.</summary>
    public bool IsRestore { get; private set; }

    public bool Succeeded { get; private set; }

    public string? Message { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
}
