using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Variables;

/// <summary>
/// Manages the catalogue of variable definitions.
/// <para>
/// A definition is the contract between a user-entered assumption and a
/// simulation model's input key. Deleting one would silently unbind every
/// scenario value that references it, so deletion is refused while any
/// assumption still uses it.
/// </para>
/// </summary>
public sealed class VariableDefinitionService
{
    private readonly IApplicationDbContext _db;

    public VariableDefinitionService(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Result<IReadOnlyList<VariableDefinitionDto>>> ListAsync(
        VariableCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.VariableDefinitions.AsNoTracking();

        if (category is not null)
        {
            query = query.Where(v => v.Category == category);
        }

        var definitions = await query
            .OrderBy(v => v.Category)
            .ThenBy(v => v.Name)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<VariableDefinitionDto>>.Success(
            definitions.Select(Map).ToList());
    }

    public async Task<Result<VariableDefinitionDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var definition = await _db.VariableDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        return definition is null
            ? Result<VariableDefinitionDto>.Failure(ErrorCodes.NotFound, "The variable definition could not be found.")
            : Result<VariableDefinitionDto>.Success(Map(definition));
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateVariableDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, "A variable definition must have a key.");
        }

        // The domain stores the key trimmed and lower-cased, so the duplicate
        // check normalises the same way. Comparing the normalised form avoids a
        // case-insensitive comparison that the database might not translate.
        var normalizedKey = request.Key.Trim().ToLowerInvariant();

        // The key is what a model binds to, so two definitions sharing a key
        // would make the binding ambiguous. Checked before the domain runs so
        // the message names the conflict rather than a constraint.
        var keyTaken = await _db.VariableDefinitions
            .AsNoTracking()
            .AnyAsync(v => v.Key == normalizedKey, cancellationToken);

        if (keyTaken)
        {
            return Result<Guid>.Failure(
                ErrorCodes.Conflict,
                $"A variable with the key '{normalizedKey}' already exists.");
        }

        VariableDefinition definition;

        try
        {
            definition = new VariableDefinition(
                request.Key,
                request.Name,
                request.Category,
                request.DataType,
                request.Unit,
                request.DefaultValue,
                request.MinimumAllowedValue,
                request.MaximumAllowedValue,
                request.Description,
                request.IsExternalDataCandidate,
                request.IsSensitive);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        _db.VariableDefinitions.Add(definition);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(definition.Id);
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        UpdateVariableDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = await _db.VariableDefinitions
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (definition is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The variable definition could not be found.");
        }

        try
        {
            definition.Update(
                request.Name,
                request.Description,
                request.Category,
                request.DataType,
                request.Unit,
                request.DefaultValue,
                request.MinimumAllowedValue,
                request.MaximumAllowedValue,
                request.IsExternalDataCandidate,
                request.IsSensitive);
        }
        catch (DomainException ex)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Deletes a definition only when no assumption references it. The foreign
    /// key would reject the delete anyway; checking first turns a database
    /// exception into a message that explains what to do.
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _db.VariableDefinitions
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (definition is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The variable definition could not be found.");
        }

        if (definition.IsBuiltIn)
        {
            return Result.Failure(
                ErrorCodes.Conflict,
                $"'{definition.Name}' is a built-in variable and cannot be deleted.");
        }

        var usageCount = await _db.ScenarioVariables
            .AsNoTracking()
            .CountAsync(v => v.VariableDefinitionId == id, cancellationToken);

        if (usageCount > 0)
        {
            return Result.Failure(
                ErrorCodes.Conflict,
                $"'{definition.Name}' is used by {usageCount} scenario assumption(s) and cannot be deleted.");
        }

        _db.VariableDefinitions.Remove(definition);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static VariableDefinitionDto Map(VariableDefinition definition) =>
        new(
            definition.Id,
            definition.Key,
            definition.Name,
            definition.Description,
            definition.Category,
            definition.DataType,
            definition.Unit,
            definition.DefaultValue,
            definition.MinimumAllowedValue,
            definition.MaximumAllowedValue,
            definition.IsExternalDataCandidate,
            definition.IsSensitive,
            definition.IsBuiltIn);
}
