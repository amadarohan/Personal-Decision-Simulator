namespace DecisionSimulator.Application.Common;

/// <summary>
/// A stored file, described without exposing an absolute path.
/// </summary>
public sealed record StoredFile(
    string FileName,
    string RelativePath,
    string ContentType,
    long SizeBytes,
    string Checksum);

/// <summary>
/// Local file storage for decision attachments.
/// <para>
/// The Application layer never touches the file system directly. Keeping storage
/// behind this interface means the path-traversal rules live in one place, and a
/// test can substitute an in-memory implementation.
/// </para>
/// </summary>
public interface IDocumentStorage
{
    /// <summary>
    /// Writes a file and returns its metadata. The implementation is responsible
    /// for rejecting names that would escape the storage root.
    /// </summary>
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stored file for reading, or returns null when it is missing.
    /// </summary>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored file. Returns false when it was already absent.
    /// </summary>
    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
