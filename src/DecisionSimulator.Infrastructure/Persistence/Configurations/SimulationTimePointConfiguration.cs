using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class SimulationTimePointConfiguration : IEntityTypeConfiguration<SimulationTimePoint>
{
    public void Configure(EntityTypeBuilder<SimulationTimePoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SimulationTimePoints");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.MetricName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Value).HasColumnType(ColumnPrecision.Rate);
        builder.Property(t => t.LowerBound).HasColumnType(ColumnPrecision.Rate);
        builder.Property(t => t.UpperBound).HasColumnType(ColumnPrecision.Rate);

        builder.Property(t => t.Date)
            .HasColumnType(ColumnPrecision.Timestamp);

        // One value per metric per time index per run.
        builder.HasIndex(t => new { t.SimulationRunId, t.MetricName, t.TimeIndex })
            .IsUnique();

        builder.HasIndex(t => new { t.SimulationRunId, t.Date });
    }
}
