using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(l => l.EntityType)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(l => l.EntityId)
            .HasMaxLength(120);

        builder.Property(l => l.DetailsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(l => l.Actor)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(l => l.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(l => l.CreatedAtUtc);

        builder.HasIndex(l => new { l.EntityType, l.EntityId });

        builder.HasIndex(l => l.Action);
    }
}
