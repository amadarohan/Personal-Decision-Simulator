using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class SimulationResultConfiguration : IEntityTypeConfiguration<SimulationResult>
{
    public void Configure(EntityTypeBuilder<SimulationResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SimulationResults");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.MetricName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.MetricUnit)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        // Statistics are stored at the wider scale: a percentile rounded to two
        // decimals would misrepresent a ratio or a rate metric.
        builder.Property(r => r.MeanValue).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.MedianValue).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.MinimumValue).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.MaximumValue).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.StandardDeviation).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.Percentile10).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.Percentile25).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.Percentile75).HasColumnType(ColumnPrecision.Rate);
        builder.Property(r => r.Percentile90).HasColumnType(ColumnPrecision.Rate);

        builder.Property(r => r.ResultMetadataJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(r => new { r.SimulationRunId, r.MetricName })
            .IsUnique();
    }
}
