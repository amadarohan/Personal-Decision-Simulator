using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class SimulationRunConfiguration : IEntityTypeConfiguration<SimulationRun>
{
    public void Configure(EntityTypeBuilder<SimulationRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SimulationRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ModelVersion)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(r => r.RunType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.InputSnapshotJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(r => r.OutputSummaryJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(r => r.StartedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(r => r.CompletedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        // Results and time points are produced by the run and have no meaning
        // without it, so they cascade.
        builder.HasMany(r => r.Results)
            .WithOne()
            .HasForeignKey(x => x.SimulationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.TimePoints)
            .WithOne()
            .HasForeignKey(x => x.SimulationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deliberately no foreign key to SimulationModels. Built-in models are
        // defined in code and may never have a database row, so a hard
        // constraint would reject legitimate runs. The authoritative reference
        // for reproducibility is ModelVersion, which the run stores itself.
        builder.HasIndex(r => new { r.ScenarioId, r.StartedAtUtc });

        builder.HasIndex(r => r.Status);

        builder.HasIndex(r => r.SimulationModelId);
    }
}
