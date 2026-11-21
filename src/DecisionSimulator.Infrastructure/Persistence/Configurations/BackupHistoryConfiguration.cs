using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class BackupHistoryConfiguration : IEntityTypeConfiguration<BackupHistory>
{
    public void Configure(EntityTypeBuilder<BackupHistory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BackupHistories");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(b => b.RelativePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.Checksum)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(b => b.Message)
            .HasMaxLength(4000);

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(b => b.CreatedAtUtc);
    }
}
