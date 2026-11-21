using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AssumptionSetConfiguration : IEntityTypeConfiguration<AssumptionSet>
{
    public void Configure(EntityTypeBuilder<AssumptionSet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AssumptionSets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.Description)
            .HasMaxLength(4000);

        builder.Property(a => a.ValuesJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(a => a.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(a => a.Name)
            .IsUnique();
    }
}
