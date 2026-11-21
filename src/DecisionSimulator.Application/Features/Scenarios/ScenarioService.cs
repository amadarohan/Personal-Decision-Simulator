using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Decisions;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.Scenarios;

/// <summary>
/// Read and write operations for scenarios and their assumptions.
/// <para>
/// Assumption changes go through the scenario aggregate so that the
/// assumption-set version is bumped in exactly one place. A simulation run
/// records that version, which is what makes an old result still interpretable
/// after the user edits the assumptions.
/// </para>
/// </summary>
public sealed class ScenarioService
{
    private readonly IApplicationDbContext _db;

    public ScenarioService(IApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Lists scenarios, optionally narrowed to one decision. Used by the scenario
    /// index page, which has no decision in context.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioSummaryDto>> ListAsync(
        Guid? decisionId = null,
        ScenarioStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Scenarios.AsNoTracking();

        if (decisionId is not null)
        {
            query = query.Where(s => s.DecisionId == decisionId);
        }

        if (status is not null)
        {
            query = query.Where(s => s.Status == status);
        }

        return await query
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Select(s => new ScenarioSummaryDto(
                s.Id,
                s.Name,
                s.Type,
                s.Status,
                s.TimeHorizonMonths,
                s.AlternativeId,
                s.ParentScenarioId,
                s.IsBaseline,
                s.ProbabilityWeight,
                s.AssumptionSetVersion,
                s.Variables.Count,
                s.Runs.Count,
                s.Runs.OrderByDescending(r => r.StartedAtUtc)
                    .Select(r => (DateTime?)r.StartedAtUtc)
                    .FirstOrDefault(),
                s.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<ScenarioDetailDto>> GetAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default)
    {        var scenario = await _db.Scenarios
            .AsNoTracking()
            .Include(s => s.Variables)
            .Include(s => s.Dependencies)
            .Include(s => s.Runs)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result<ScenarioDetailDto>.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        var definitions = await _db.VariableDefinitions
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        return Result<ScenarioDetailDto>.Success(MapDetail(scenario, definitions));
    }

    public async Task<Result<Guid>> CreateAsync(
        Guid decisionId,
        CreateScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decision = await _db.Decisions
            .Include(d => d.Scenarios)
            .Include(d => d.Alternatives)
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision is null)
        {
            return Result<Guid>.Failure(ErrorCodes.NotFound, "The decision could not be found.");
        }

        Scenario scenario;

        try
        {
            scenario = decision.AddScenario(
                request.Name,
                request.Type,
                request.TimeHorizonMonths,
                request.AlternativeId,
                request.ParentScenarioId,
                request.Description);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(scenario.Id);
    }

    public async Task<Result> UpdateAsync(
        Guid scenarioId,
        UpdateScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenario = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);
        if (scenario is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        try
        {
            scenario.UpdateDetails(request.Name, request.Description, request.Type, request.TimeHorizonMonths);
        }
        catch (DomainException ex)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .Include(d => d.Scenarios)
            .FirstOrDefaultAsync(d => d.Scenarios.Any(s => s.Id == scenarioId), cancellationToken);

        if (decision is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        try
        {
            decision.RemoveScenario(scenarioId);
        }
        catch (EntityNotFoundException ex)
        {
            return Result.Failure(ErrorCodes.NotFound, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Sets or replaces one assumption. The variable definition must exist,
    /// because a value with no definition has no key for the model to bind to.
    /// </summary>
    public async Task<Result> SetVariableAsync(
        Guid scenarioId,
        SetScenarioVariableRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenario = await _db.Scenarios
            .Include(s => s.Variables)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        var definition = await _db.VariableDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VariableDefinitionId, cancellationToken);

        if (definition is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The variable definition could not be found.");
        }

        // The domain validates the distribution parameters, but the allowed
        // range belongs to the definition, so it is checked here.
        if (definition.MinimumAllowedValue is not null && request.Value < definition.MinimumAllowedValue)
        {
            return Result.Failure(
                ErrorCodes.ValidationFailed,
                $"'{definition.Name}' cannot be below {definition.MinimumAllowedValue}.");
        }

        if (definition.MaximumAllowedValue is not null && request.Value > definition.MaximumAllowedValue)
        {
            return Result.Failure(
                ErrorCodes.ValidationFailed,
                $"'{definition.Name}' cannot be above {definition.MaximumAllowedValue}.");
        }

        ScenarioVariable variable;

        try
        {
            variable = scenario.SetVariable(
                request.VariableDefinitionId,
                request.Value,
                request.DistributionType,
                request.MinimumValue,
                request.MaximumValue,
                request.StandardDeviation,
                request.Confidence,
                request.SourceType,
                request.SourceReference,
                request.DistributionBasis,
                request.IsUserOverride);
        }
        catch (DomainException ex)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        if (request.DiscreteOutcomesJson is not null)
        {
            try
            {
                variable.SetDiscreteOutcomes(request.DiscreteOutcomesJson);
            }
            catch (DomainException ex)
            {
                return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveVariableAsync(
        Guid scenarioId,
        Guid scenarioVariableId,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.Variables)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        try
        {
            scenario.RemoveVariable(scenarioVariableId);
        }
        catch (EntityNotFoundException ex)
        {
            return Result.Failure(ErrorCodes.NotFound, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetBaselineAsync(
        Guid scenarioId,
        bool isBaseline,
        CancellationToken cancellationToken = default)
    {
        var decision = await _db.Decisions
            .Include(d => d.Scenarios)
            .FirstOrDefaultAsync(d => d.Scenarios.Any(s => s.Id == scenarioId), cancellationToken);

        if (decision is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        var scenario = decision.Scenarios.First(s => s.Id == scenarioId);

        // The database enforces one baseline per decision with a filtered unique
        // index. Clearing the previous baseline here keeps the failure a clear
        // message instead of a constraint violation.
        if (isBaseline)
        {
            foreach (var other in decision.Scenarios.Where(s => s.IsBaseline && s.Id != scenarioId))
            {
                other.MarkBaseline(false);
            }
        }

        scenario.MarkBaseline(isBaseline);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetProbabilityWeightAsync(
        Guid scenarioId,
        decimal? weight,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);
        if (scenario is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        try
        {
            scenario.SetProbabilityWeight(weight);
        }
        catch (DomainException ex)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AddDependencyAsync(
        Guid scenarioId,
        Guid dependsOnScenarioId,
        ScenarioRelationshipType relationshipType,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.Dependencies)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        var target = await _db.Scenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == dependsOnScenarioId, cancellationToken);

        if (target is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario to depend on could not be found.");
        }

        if (target.DecisionId != scenario.DecisionId)
        {
            return Result.Failure(
                ErrorCodes.ValidationFailed,
                "A scenario can only depend on another scenario of the same decision.");
        }

        try
        {
            scenario.AddDependency(dependsOnScenarioId, relationshipType);
        }
        catch (CircularDependencyException ex)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveDependencyAsync(
        Guid scenarioId,
        Guid dependsOnScenarioId,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.Scenarios
            .Include(s => s.Dependencies)
            .FirstOrDefaultAsync(s => s.Id == scenarioId, cancellationToken);

        if (scenario is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The scenario could not be found.");
        }

        scenario.RemoveDependency(dependsOnScenarioId);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static ScenarioDetailDto MapDetail(
        Scenario scenario,
        Dictionary<Guid, VariableDefinition> definitions) => new(
        scenario.Id,
        scenario.DecisionId,
        scenario.AlternativeId,
        scenario.ParentScenarioId,
        scenario.Name,
        scenario.Description,
        scenario.Type,
        scenario.Status,
        scenario.TimeHorizonMonths,
        scenario.IsBaseline,
        scenario.ProbabilityWeight,
        scenario.AssumptionSetVersion,
        scenario.CreatedAtUtc,
        scenario.UpdatedAtUtc,
        scenario.Variables
            .OrderBy(v => definitions.TryGetValue(v.VariableDefinitionId, out var d) ? d.Name : string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .Select(v =>
            {
                definitions.TryGetValue(v.VariableDefinitionId, out var definition);

                return new ScenarioVariableDto(
                    v.Id,
                    v.VariableDefinitionId,
                    definition?.Key ?? string.Empty,
                    definition?.Name ?? "(unknown variable)",
                    definition?.Unit ?? string.Empty,
                    definition?.Category ?? VariableCategory.Custom,
                    v.Value,
                    v.DistributionType,
                    v.MinimumValue,
                    v.MaximumValue,
                    v.StandardDeviation,
                    v.Confidence,
                    v.SourceType,
                    v.SourceReference,
                    v.DistributionBasis,
                    v.IsUserOverride,
                    v.DiscreteOutcomesJson);
            })
            .ToList(),
        scenario.Dependencies
            .Select(d => new ScenarioDependencyDto(d.DependsOnScenarioId, d.RelationshipType))
            .ToList(),
        scenario.Runs
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => new SimulationRunSummaryDto(
                r.Id,
                r.RunType,
                r.Status,
                r.ModelVersion,
                r.IterationCount,
                r.RandomSeed,
                r.AssumptionSetVersion,
                r.StartedAtUtc,
                r.CompletedAtUtc,
                r.ErrorMessage))
            .ToList());
}
