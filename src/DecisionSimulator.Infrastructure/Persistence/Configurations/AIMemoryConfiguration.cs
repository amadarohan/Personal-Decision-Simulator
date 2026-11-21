using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AIMemoryConfiguration : IEntityTypeConfiguration<AIMemory>
{
    public void Configure(EntityTypeBuilder<AIMemory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AIMemories");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MemoryType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.SourceReference)
            .HasMaxLength(1000);

        builder.Property(m => m.Confidence)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(m => m.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(m => m.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(m => m.MemoryType);

        builder.HasIndex(m => m.IsActive);
    }
}
