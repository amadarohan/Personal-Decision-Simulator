using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(t => t.ColorHex)
            .HasMaxLength(9);

        builder.Property(t => t.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(t => t.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(t => t.Name)
            .IsUnique();
    }
}

public sealed class EntityTagConfiguration : IEntityTypeConfiguration<EntityTag>
{
    public void Configure(EntityTypeBuilder<EntityTag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EntityTags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.EntityType)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(t => t.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // The same tag cannot be attached twice to the same entity.
        builder.HasIndex(t => new { t.TagId, t.EntityType, t.EntityId })
            .IsUnique();

        builder.HasIndex(t => new { t.EntityType, t.EntityId });
    }
}
