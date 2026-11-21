using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class DecisionDocumentConfiguration : IEntityTypeConfiguration<DecisionDocument>
{
    public void Configure(EntityTypeBuilder<DecisionDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("DecisionDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(d => d.RelativePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Checksum)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(d => d.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(d => d.DecisionId);

        builder.HasIndex(d => d.RelativePath)
            .IsUnique();
    }
}
