using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Simulations;
using DecisionSimulator.Domain.Enums;
using DecisionSimulator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// A fixed clock so freshness and scheduling rules can be tested without waiting.
/// </summary>
internal sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class StubPreferences(UserPreferences preferences) : IUserPreferencesAccessor
{
    public Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(preferences);
}

/// <summary>
/// An in-memory database for application-service tests.
/// <para>
/// The in-memory provider does not enforce unique indexes or rowversion, so
/// constraint behaviour is covered by the SQL Server integration tests instead.
/// These tests cover service logic: validation, state transitions, and the
/// mapping between persisted assumptions and the engine.
/// </para>
/// </summary>
internal sealed class InMemoryDatabase : IApplicationDbContext, IDisposable
{
    private readonly DecisionSimulatorDbContext _context;

    public InMemoryDatabase()
    {
        var options = new DbContextOptionsBuilder<DecisionSimulatorDbContext>()
            .UseInMemoryDatabase($"PDS_Unit_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new DecisionSimulatorDbContext(options);
    }

    public DbSet<Domain.Entities.UserProfile> UserProfiles => _context.UserProfiles;

    public DbSet<Domain.Entities.Decision> Decisions => _context.Decisions;

    public DbSet<Domain.Entities.DecisionAlternative> DecisionAlternatives => _context.DecisionAlternatives;

    public DbSet<Domain.Entities.DecisionReview> DecisionReviews => _context.DecisionReviews;

    public DbSet<Domain.Entities.DecisionDocument> DecisionDocuments => _context.DecisionDocuments;

    public DbSet<Domain.Entities.Scenario> Scenarios => _context.Scenarios;

    public DbSet<Domain.Entities.ScenarioVariable> ScenarioVariables => _context.ScenarioVariables;

    public DbSet<Domain.Entities.ScenarioDependency> ScenarioDependencies => _context.ScenarioDependencies;

    public DbSet<Domain.Entities.VariableDefinition> VariableDefinitions => _context.VariableDefinitions;

    public DbSet<Domain.Entities.SimulationModel> SimulationModels => _context.SimulationModels;

    public DbSet<Domain.Entities.SimulationRun> SimulationRuns => _context.SimulationRuns;

    public DbSet<Domain.Entities.SimulationResult> SimulationResults => _context.SimulationResults;

    public DbSet<Domain.Entities.SimulationTimePoint> SimulationTimePoints => _context.SimulationTimePoints;

    public DbSet<Domain.Entities.RiskFactor> RiskFactors => _context.RiskFactors;

    public DbSet<Domain.Entities.EconomicIndicator> EconomicIndicators => _context.EconomicIndicators;

    public DbSet<Domain.Entities.DataSource> DataSources => _context.DataSources;

    public DbSet<Domain.Entities.DataObservation> DataObservations => _context.DataObservations;

    public DbSet<Domain.Entities.AIConversation> AIConversations => _context.AIConversations;

    public DbSet<Domain.Entities.AIMessage> AIMessages => _context.AIMessages;

    public DbSet<Domain.Entities.AIProposal> AIProposals => _context.AIProposals;

    public DbSet<Domain.Entities.AIExecutionLog> AIExecutionLogs => _context.AIExecutionLogs;

    public DbSet<Domain.Entities.AIMemory> AIMemories => _context.AIMemories;

    public DbSet<Domain.Entities.AppSetting> AppSettings => _context.AppSettings;

    public DbSet<Domain.Entities.AuditLog> AuditLogs => _context.AuditLogs;

    public DbSet<Domain.Entities.BackupHistory> BackupHistories => _context.BackupHistories;

    public DbSet<Domain.Entities.Tag> Tags => _context.Tags;

    public DbSet<Domain.Entities.EntityTag> EntityTags => _context.EntityTags;

    public DbSet<Domain.Entities.AssumptionSet> AssumptionSets => _context.AssumptionSets;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// The underlying context, for tests that need to seed entities directly
    /// rather than through a service.
    /// </summary>
    public DecisionSimulatorDbContext Context => _context;

    public void Dispose() => _context.Dispose();
}
