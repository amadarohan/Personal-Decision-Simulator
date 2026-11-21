using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class ScenarioVariableConfiguration : IEntityTypeConfiguration<ScenarioVariable>
{
    public void Configure(EntityTypeBuilder<ScenarioVariable> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ScenarioVariables");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Value)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.MinimumValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.MaximumValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.StandardDeviation)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.DistributionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(v => v.Confidence)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(v => v.SourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(v => v.DistributionBasis)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(v => v.SourceReference)
            .HasMaxLength(1000);

        builder.Property(v => v.DiscreteOutcomesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(v => v.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(v => v.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasOne<VariableDefinition>()
            .WithMany()
            .HasForeignKey(v => v.VariableDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // One value per variable per scenario. The domain already replaces
        // rather than duplicates, but the constraint protects the invariant.
        builder.HasIndex(v => new { v.ScenarioId, v.VariableDefinitionId })
            .IsUnique();

        builder.HasIndex(v => v.VariableDefinitionId);
    }
}
