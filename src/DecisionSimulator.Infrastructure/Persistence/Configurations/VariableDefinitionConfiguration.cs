using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class VariableDefinitionConfiguration : IEntityTypeConfiguration<VariableDefinition>
{
    public void Configure(EntityTypeBuilder<VariableDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("VariableDefinitions");

        builder.HasKey(v => v.Id);

        // The key is the stable identifier used by models and formulas. It must
        // be unique so that a formula can never resolve to two different rows.
        builder.Property(v => v.Key)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(v => v.Key)
            .IsUnique();

        builder.Property(v => v.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(v => v.Description)
            .HasMaxLength(2000);

        builder.Property(v => v.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(v => v.DataType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(v => v.Unit)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(v => v.DefaultValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.MinimumAllowedValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.MaximumAllowedValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(v => v.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(v => v.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(v => v.Category);

        builder.HasIndex(v => v.IsExternalDataCandidate);
    }
}
