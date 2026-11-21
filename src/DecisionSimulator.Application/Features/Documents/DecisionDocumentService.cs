using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Documents;

/// <summary>
/// Attachments for a decision.
/// <para>
/// The file is written first and the metadata row second. If the row cannot be
/// saved the file is removed again, so a failed upload never leaves an orphan on
/// disk. The reverse order would leave a row pointing at nothing.
/// </para>
/// </summary>
public sealed class DecisionDocumentService
{
    private readonly IApplicationDbContext _db;
    private readonly IDocumentStorage _storage;

    public DecisionDocumentService(IApplicationDbContext db, IDocumentStorage storage)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task<Result<IReadOnlyList<DecisionDocumentDto>>> ListAsync(
        Guid decisionId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Decisions
            .AnyAsync(d => d.Id == decisionId, cancellationToken);

        if (!exists)
        {
            return Result<IReadOnlyList<DecisionDocumentDto>>.Failure(
                ErrorCodes.NotFound,
                "The decision could not be found.");
        }

        var documents = await _db.DecisionDocuments
            .AsNoTracking()
            .Where(d => d.DecisionId == decisionId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DecisionDocumentDto(
                d.Id,
                d.DecisionId,
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.Checksum,
                d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DecisionDocumentDto>>.Success(documents);
    }

    public async Task<Result<DecisionDocumentDto>> UploadAsync(
        Guid decisionId,
        UploadDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return Result<DecisionDocumentDto>.Failure(
                ErrorCodes.ValidationFailed,
                "A file name is required.");
        }

        var decision = await _db.Decisions
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result<DecisionDocumentDto>.Failure(
                ErrorCodes.NotFound,
                "The decision could not be found.");
        }

        StoredFile stored;

        try
        {
            stored = await _storage.SaveAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                cancellationToken);
        }
        catch (NotSupportedException ex)
        {
            return Result<DecisionDocumentDto>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DecisionDocumentDto>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<DecisionDocumentDto>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        DecisionDocument document;

        try
        {
            document = decision.AddDocument(
                stored.FileName,
                stored.RelativePath,
                stored.ContentType,
                stored.SizeBytes,
                stored.Checksum);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // The row was not saved, so the file must not stay behind.
            await _storage.DeleteAsync(stored.RelativePath, CancellationToken.None);
            throw;
        }

        return Result<DecisionDocumentDto>.Success(new DecisionDocumentDto(
            document.Id,
            document.DecisionId,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.Checksum,
            document.CreatedAtUtc));
    }

    public async Task<Result<Stream>> OpenAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _db.DecisionDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            return Result<Stream>.Failure(ErrorCodes.NotFound, "The document could not be found.");
        }

        var stream = await _storage.OpenReadAsync(document.RelativePath, cancellationToken);

        return stream is null
            ? Result<Stream>.Failure(
                ErrorCodes.NotFound,
                "The document is recorded but its file is missing from storage.")
            : Result<Stream>.Success(stream);
    }

    public async Task<Result> DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _db.DecisionDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The document could not be found.");
        }

        var relativePath = document.RelativePath;

        _db.DecisionDocuments.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);

        // The row is gone, so a leftover file is only wasted space. Deleting it
        // after the row keeps the database as the source of truth.
        await _storage.DeleteAsync(relativePath, cancellationToken);

        return Result.Success();
    }
}
