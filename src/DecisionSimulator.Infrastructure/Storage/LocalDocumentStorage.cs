using System.Security.Cryptography;
using DecisionSimulator.Application.Common;

namespace DecisionSimulator.Infrastructure.Storage;

/// <summary>
/// Options for local document storage.
/// </summary>
public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    /// <summary>
    /// Root directory for attachments. Relative paths are resolved against the
    /// application's content root.
    /// </summary>
    public string RootPath { get; set; } = "App_Data/documents";

    /// <summary>Largest file the application will accept, in bytes.</summary>
    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Extensions that may be stored. An allow-list is used rather than a
    /// deny-list because a deny-list always misses something.
    /// </summary>
    public IReadOnlySet<string> AllowedExtensions { get; set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".txt", ".md", ".csv", ".json", ".xml",
            ".png", ".jpg", ".jpeg", ".gif", ".webp",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
        };
}

/// <summary>
/// Stores attachments under a single root directory.
/// <para>
/// Every path is resolved and then checked to be inside the root, so a crafted
/// file name such as <c>../../appsettings.json</c> cannot escape. The stored name
/// is generated, never taken from the caller, which also removes the risk of
/// overwriting an existing file.
/// </para>
/// </summary>
public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly DocumentStorageOptions _options;
    private readonly string _rootPath;

    public LocalDocumentStorage(DocumentStorageOptions options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        _options = options;
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(options.RootPath)
                ? options.RootPath
                : Path.Combine(contentRootPath, options.RootPath));
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeName = SanitizeFileName(fileName);
        var extension = Path.GetExtension(safeName);

        if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new NotSupportedException(
                $"Files with the extension '{extension}' cannot be attached.");
        }

        Directory.CreateDirectory(_rootPath);

        // A generated name means two uploads of "notes.txt" never collide and a
        // caller cannot choose where the file lands.
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_rootPath, storedName);

        long sizeBytes;
        string checksum;

        await using (var target = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await content.CopyToAsync(target, cancellationToken);
            sizeBytes = target.Length;
        }

        if (sizeBytes > _options.MaxFileSizeBytes)
        {
            File.Delete(fullPath);
            throw new InvalidOperationException(
                $"The file is larger than the {_options.MaxFileSizeBytes} byte limit.");
        }

        checksum = await ComputeChecksumAsync(fullPath, cancellationToken);

        return new StoredFile(
            safeName,
            storedName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sizeBytes,
            checksum);
    }

    public Task<Stream?> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWithinRoot(relativePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWithinRoot(relativePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Resolves a relative path and refuses anything that lands outside the root.
    /// </summary>
    private string ResolveWithinRoot(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("A relative path is required.", nameof(relativePath));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));

        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The requested path resolves outside the document storage root.");
        }

        return fullPath;
    }

    /// <summary>
    /// Reduces a caller-supplied name to a bare file name with no directory part.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        var bare = Path.GetFileName(fileName.Trim());

        if (string.IsNullOrWhiteSpace(bare) || bare is "." or "..")
        {
            throw new ArgumentException("The file name is not usable.", nameof(fileName));
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            bare = bare.Replace(invalid, '_');
        }

        return bare;
    }
    private static async Task<string> ComputeChecksumAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
