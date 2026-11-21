using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Common;
using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DecisionSimulator.Infrastructure.Persistence;

/// <summary>
/// The application database context.
/// <para>
/// Schema is created exclusively through migrations. <c>EnsureCreated</c> is
/// never used: it bypasses migration history, which makes a real deployment
/// impossible to upgrade and hides destructive changes.
/// </para>
/// <para>
/// Delete behaviour is deliberately restrictive on anything that represents
/// user history. A decision, a simulation run, an observation, or an AI audit
/// record must never disappear as a side effect of deleting a parent row; the
/// user has to remove it explicitly.
/// </para>
/// </summary>
public class DecisionSimulatorDbContext : DbContext, IApplicationDbContext
{
    public DecisionSimulatorDbContext(DbContextOptions<DecisionSimulatorDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Decision> Decisions => Set<Decision>();

    public DbSet<DecisionAlternative> DecisionAlternatives => Set<DecisionAlternative>();

    public DbSet<DecisionReview> DecisionReviews => Set<DecisionReview>();

    public DbSet<DecisionDocument> DecisionDocuments => Set<DecisionDocument>();

    public DbSet<Scenario> Scenarios => Set<Scenario>();

    public DbSet<ScenarioVariable> ScenarioVariables => Set<ScenarioVariable>();

    public DbSet<ScenarioDependency> ScenarioDependencies => Set<ScenarioDependency>();

    public DbSet<VariableDefinition> VariableDefinitions => Set<VariableDefinition>();

    public DbSet<SimulationModel> SimulationModels => Set<SimulationModel>();

    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();

    public DbSet<SimulationResult> SimulationResults => Set<SimulationResult>();

    public DbSet<SimulationTimePoint> SimulationTimePoints => Set<SimulationTimePoint>();

    public DbSet<RiskFactor> RiskFactors => Set<RiskFactor>();

    public DbSet<EconomicIndicator> EconomicIndicators => Set<EconomicIndicator>();

    public DbSet<DataSource> DataSources => Set<DataSource>();

    public DbSet<DataObservation> DataObservations => Set<DataObservation>();

    public DbSet<AIConversation> AIConversations => Set<AIConversation>();

    public DbSet<AIMessage> AIMessages => Set<AIMessage>();

    public DbSet<AIProposal> AIProposals => Set<AIProposal>();

    public DbSet<AIExecutionLog> AIExecutionLogs => Set<AIExecutionLog>();

    public DbSet<AIMemory> AIMemories => Set<AIMemory>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<EntityTag> EntityTags => Set<EntityTag>();

    public DbSet<AssumptionSet> AssumptionSets => Set<AssumptionSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Every IEntityTypeConfiguration in this assembly is applied here, so a
        // new entity only needs its configuration file to be picked up.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DecisionSimulatorDbContext).Assembly);

        ConfigureClientGeneratedKeys(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Declares that the application, not the database, assigns every primary key.
    /// <para>
    /// <see cref="Entity.Id"/> is initialised to a new <see cref="Guid"/> in the
    /// constructor, but EF's convention treats a Guid key as
    /// <c>ValueGeneratedOnAdd</c>. Under that convention, when a child entity is
    /// added to the collection of an already-tracked aggregate, EF sees a key
    /// that is already set and concludes the row must already exist, so it marks
    /// the child <c>Modified</c> and issues an <c>UPDATE</c> against a row that
    /// was never inserted. Declaring the keys as never generated makes EF treat
    /// such a child as <c>Added</c>, which is what the aggregate design intends.
    /// </para>
    /// <para>
    /// This changes no SQL: EF never emitted a default constraint for these
    /// columns, so the existing migration remains correct.
    /// </para>
    /// </summary>
    private static void ConfigureClientGeneratedKeys(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                continue;
            }

            foreach (var property in primaryKey.Properties)
            {
                if (property.ClrType == typeof(Guid))
                {
                    property.ValueGenerated = ValueGenerated.Never;
                }
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareForSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareForSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Applies the cross-cutting rules that must hold for every write:
    /// timestamps are stamped in UTC, append-only tables reject edits and
    /// deletes, and a simulation run's frozen inputs cannot change.
    /// </summary>
    private void PrepareForSave()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            GuardAppendOnly(entry);
            GuardFrozenRunInputs(entry);

            if (entry.Entity is not AuditableEntity)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                // The domain sets these on construction, but a value loaded from
                // a backup or a test fixture may carry a stale value.
                entry.Property(nameof(AuditableEntity.CreatedAtUtc)).CurrentValue = now;
                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Never trust a caller-supplied UpdatedAtUtc: it must reflect the
                // moment of the write, not the moment the object was built.
                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
                entry.Property(nameof(AuditableEntity.CreatedAtUtc)).IsModified = false;
            }
        }
    }

    /// <summary>
    /// Types whose rows are a historical record. Deleting them would destroy the
    /// audit trail the application promises to keep, so deletes are always
    /// rejected.
    /// <para>
    /// Some of these types do have a legitimate lifecycle: an observation is
    /// superseded or verified, and a review is annotated after the fact. Those
    /// transitions must not rewrite the recorded fact, so modification is
    /// allowed only for the fields listed in <see cref="MutableFieldsFor"/>.
    /// Everything else is frozen.
    /// </para>
    /// </summary>
    private static void GuardAppendOnly(EntityEntry entry)
    {
        var mutableFields = MutableFieldsFor(entry.Entity);

        // A null set means the type is not append-only at all.
        if (mutableFields is null)
        {
            return;
        }

        if (entry.State == EntityState.Deleted)
        {
            throw new InvalidOperationException(
                $"{entry.Entity.GetType().Name} rows are an append-only record and cannot be deleted. " +
                "Mark the row as superseded or rejected instead.");
        }

        if (entry.State != EntityState.Modified)
        {
            return;
        }

        foreach (var property in entry.Properties)
        {
            if (!property.IsModified || mutableFields.Contains(property.Metadata.Name))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"{entry.Entity.GetType().Name}.{property.Metadata.Name} is part of an append-only " +
                "record and cannot be modified. Insert a new row instead.");
        }
    }

    /// <summary>
    /// The fields each append-only type may still change after insert.
    /// <para>
    /// Returns <see langword="null"/> when the type is not append-only, and an
    /// empty set when the type is append-only but fully immutable. The two cases
    /// must stay distinguishable, otherwise a fully immutable type would be
    /// treated as unprotected.
    /// </para>
    /// </summary>
    private static HashSet<string>? MutableFieldsFor(object entity) => entity switch
    {
        // Supersede() closes the validity window; MarkVerified() records a
        // verification decision. Neither rewrites the observed value.
        DataObservation =>
        [
            nameof(DataObservation.ValidUntilUtc),
            nameof(DataObservation.VerificationStatus),
            nameof(DataObservation.Confidence)
        ],

        // A review is written once and then annotated as understanding improves.
        // The decision it belongs to and the date it was taken are fixed.
        DecisionReview =>
        [
            nameof(DecisionReview.ActualOutcome),
            nameof(DecisionReview.ExpectedOutcome),
            nameof(DecisionReview.OutcomeDifference),
            nameof(DecisionReview.WhatWentWell),
            nameof(DecisionReview.WhatWentWrong),
            nameof(DecisionReview.LessonsLearned)
        ],

        // Written once, never amended.
        AuditLog or AIExecutionLog or BackupHistory or AIMessage => [],

        _ => null
    };

    /// <summary>
    /// A simulation run's inputs are what make its output reproducible. Once the
    /// run exists, the model version, seed, iteration count and input snapshot
    /// are frozen; only status and output fields may change.
    /// </summary>
    private static void GuardFrozenRunInputs(EntityEntry entry)
    {
        if (entry.Entity is not SimulationRun || entry.State != EntityState.Modified)
        {
            return;
        }

        string[] frozen =
        [
            nameof(SimulationRun.ScenarioId),
            nameof(SimulationRun.SimulationModelId),
            nameof(SimulationRun.ModelVersion),
            nameof(SimulationRun.RunType),
            nameof(SimulationRun.IterationCount),
            nameof(SimulationRun.RandomSeed),
            nameof(SimulationRun.InputSnapshotJson),
            nameof(SimulationRun.AssumptionSetVersion),
            nameof(SimulationRun.StartedAtUtc),
            nameof(SimulationRun.CreatedAtUtc)
        ];

        foreach (var propertyName in frozen)
        {
            if (entry.Property(propertyName).IsModified)
            {
                throw new InvalidOperationException(
                    $"SimulationRun.{propertyName} is part of the frozen input snapshot and cannot be changed after the run is created. " +
                    "Register a new run instead.");
            }
        }
    }
}
