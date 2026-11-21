using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class ScenarioConfiguration : IEntityTypeConfiguration<Scenario>
{
    public void Configure(EntityTypeBuilder<Scenario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Scenarios");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.Description)
            .HasMaxLength(4000);

        builder.Property(s => s.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(s => s.ProbabilityWeight)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        // Variables and dependencies are owned by the scenario: they have no
        // meaning outside it, so cascade is correct.
        builder.HasMany(s => s.Variables)
            .WithOne()
            .HasForeignKey(v => v.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Dependencies)
            .WithOne()
            .HasForeignKey(d => d.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Runs are history. Deleting a scenario must not silently destroy the
        // record of what was simulated, so the relationship is restrictive and
        // the user has to remove runs deliberately.
        builder.HasMany(s => s.Runs)
            .WithOne()
            .HasForeignKey(r => r.ScenarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // The Decision -> Scenarios edge is configured once, on the Decision
        // side, to avoid two competing definitions of the same relationship.
        builder.HasOne<DecisionAlternative>()
            .WithMany()
            .HasForeignKey(s => s.AlternativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Scenario>()
            .WithMany()
            .HasForeignKey(s => s.ParentScenarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.DecisionId);

        builder.HasIndex(s => s.AlternativeId);

        builder.HasIndex(s => s.ParentScenarioId);

        builder.HasIndex(s => new { s.DecisionId, s.Status });

        // A decision may only have one baseline scenario.
        builder.HasIndex(s => new { s.DecisionId, s.IsBaseline })
            .HasDatabaseName("IX_Scenarios_OneBaselinePerDecision")
            .IsUnique()
            .HasFilter("[IsBaseline] = 1");
    }
}
