using DecisionSimulator.Domain.Common;

namespace DecisionSimulator.Domain.Entities;

/// <summary>
/// Metadata for a file attached to a decision. The file itself lives on local
/// disk under the configured storage root; only the relative path is stored.
/// </summary>
public class DecisionDocument : Entity
{
    private DecisionDocument()
    {
    }

    internal DecisionDocument(
        Guid decisionId,
        string fileName,
        string relativePath,
        string contentType,
        long fileSizeBytes,
        string checksum)
    {
        DecisionId = decisionId;
        FileName = fileName;
        RelativePath = relativePath;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        Checksum = checksum;
    }

    public Guid DecisionId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Path relative to the configured storage root. Storing a relative path
    /// prevents absolute-path leakage and makes backups portable.
    /// </summary>
    public string RelativePath { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = "application/octet-stream";

    public long FileSizeBytes { get; private set; }

    /// <summary>SHA-256 of the file contents, used to detect tampering or corruption.</summary>
    public string Checksum { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
}
