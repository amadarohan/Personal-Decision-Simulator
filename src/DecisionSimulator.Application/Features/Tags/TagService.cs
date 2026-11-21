using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Tags;

/// <summary>
/// Free-form labels attached to decisions and scenarios.
/// <para>
/// The join table is generic, so the service validates the entity type against a
/// known list rather than trusting whatever string a caller supplies. Without
/// that check a typo would create tags that never appear anywhere.
/// </para>
/// </summary>
public sealed class TagService
{
    /// <summary>
    /// The entity types that may be tagged. Extending this list is the only way
    /// to make a new type taggable.
    /// </summary>
    public static readonly IReadOnlySet<string> TaggableEntityTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Decision",
            "Scenario",
            "DecisionAlternative",
            "RiskFactor"
        };

    private readonly IApplicationDbContext _db;

    public TagService(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<IReadOnlyList<TagDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var tags = await _db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await _db.EntityTags
            .AsNoTracking()
            .GroupBy(et => et.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TagId, x => x.Count, cancellationToken);

        var result = tags
            .Select(t => new TagDto(
                t.Id,
                t.Name,
                t.ColorHex,
                counts.TryGetValue(t.Id, out var count) ? count : 0))
            .ToList();

        return Result<IReadOnlyList<TagDto>>.Success(result);
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateTagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A tag must have a name.");
        }

        var name = request.Name.Trim();

        var exists = await _db.Tags
            .AnyAsync(t => t.Name == name, cancellationToken);

        if (exists)
        {
            return Result<Guid>.Failure(
                ErrorCodes.Conflict,
                $"A tag named '{name}' already exists.");
        }

        var tag = new Tag(name, request.ColorHex);
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(tag.Id);
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        UpdateTagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, "A tag must have a name.");
        }

        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tag is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The tag could not be found.");
        }

        tag.Update(request.Name, request.ColorHex);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tag is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The tag could not be found.");
        }

        var links = await _db.EntityTags
            .Where(et => et.TagId == id)
            .ToListAsync(cancellationToken);

        _db.EntityTags.RemoveRange(links);
        _db.Tags.Remove(tag);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AttachAsync(
        Guid tagId,
        AttachTagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TaggableEntityTypes.Contains(request.EntityType))
        {
            return Result.Failure(
                ErrorCodes.ValidationFailed,
                $"'{request.EntityType}' is not a taggable entity type. " +
                $"Supported types: {string.Join(", ", TaggableEntityTypes.OrderBy(t => t))}.");
        }

        var tagExists = await _db.Tags.AnyAsync(t => t.Id == tagId, cancellationToken);

        if (!tagExists)
        {
            return Result.Failure(ErrorCodes.NotFound, "The tag could not be found.");
        }

        var alreadyAttached = await _db.EntityTags
            .AnyAsync(
                et => et.TagId == tagId
                    && et.EntityType == request.EntityType
                    && et.EntityId == request.EntityId,
                cancellationToken);

        if (alreadyAttached)
        {
            return Result.Failure(ErrorCodes.Conflict, "This tag is already attached to that entity.");
        }

        _db.EntityTags.Add(new EntityTag(tagId, request.EntityType, request.EntityId));
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DetachAsync(
        Guid tagId,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var link = await _db.EntityTags
            .FirstOrDefaultAsync(
                et => et.TagId == tagId
                    && et.EntityType == entityType
                    && et.EntityId == entityId,
                cancellationToken);

        if (link is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "That tag is not attached to that entity.");
        }

        _db.EntityTags.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<TagDto>>> ListForEntityAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var tagIds = await _db.EntityTags
            .AsNoTracking()
            .Where(et => et.EntityType == entityType && et.EntityId == entityId)
            .Select(et => et.TagId)
            .ToListAsync(cancellationToken);

        var tags = await _db.Tags
            .AsNoTracking()
            .Where(t => tagIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var result = tags
            .Select(t => new TagDto(t.Id, t.Name, t.ColorHex, 0))
            .ToList();

        return Result<IReadOnlyList<TagDto>>.Success(result);
    }
}
