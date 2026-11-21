using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Common;

/// <summary>
/// The persistence surface the application layer is allowed to use.
/// <para>
/// Exposing <see cref="DbSet{TEntity}"/> keeps the application layer free of
/// repository boilerplate while still letting it be tested against an in-memory
/// or SQLite provider. The trade-off is a dependency on EF Core abstractions;
/// the alternative — one repository per aggregate — would add a large amount of
/// pass-through code without changing what the services can express.
/// </para>
/// <para>
/// Services must never return entities from this surface. They project to DTOs
/// so that the web layer cannot accidentally mutate tracked state.
/// </para>
/// </summary>
public interface IApplicationDbContext
{
    DbSet<UserProfile> UserProfiles { get; }

    DbSet<Decision> Decisions { get; }

    DbSet<DecisionAlternative> DecisionAlternatives { get; }

    DbSet<DecisionReview> DecisionReviews { get; }

    DbSet<DecisionDocument> DecisionDocuments { get; }

    DbSet<Scenario> Scenarios { get; }

    DbSet<ScenarioVariable> ScenarioVariables { get; }

    DbSet<ScenarioDependency> ScenarioDependencies { get; }

    DbSet<VariableDefinition> VariableDefinitions { get; }

    DbSet<SimulationModel> SimulationModels { get; }

    DbSet<SimulationRun> SimulationRuns { get; }

    DbSet<SimulationResult> SimulationResults { get; }

    DbSet<SimulationTimePoint> SimulationTimePoints { get; }

    DbSet<RiskFactor> RiskFactors { get; }

    DbSet<EconomicIndicator> EconomicIndicators { get; }

    DbSet<DataSource> DataSources { get; }

    DbSet<DataObservation> DataObservations { get; }

    DbSet<AIConversation> AIConversations { get; }

    DbSet<AIMessage> AIMessages { get; }

    DbSet<AIProposal> AIProposals { get; }

    DbSet<AIExecutionLog> AIExecutionLogs { get; }

    DbSet<AIMemory> AIMemories { get; }

    DbSet<AppSetting> AppSettings { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<BackupHistory> BackupHistories { get; }

    DbSet<Tag> Tags { get; }

    DbSet<EntityTag> EntityTags { get; }

    DbSet<AssumptionSet> AssumptionSets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
