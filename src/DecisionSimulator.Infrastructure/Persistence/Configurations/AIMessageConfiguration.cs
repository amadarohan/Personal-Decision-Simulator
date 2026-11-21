using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AIMessageConfiguration : IEntityTypeConfiguration<AIMessage>
{
    public void Configure(EntityTypeBuilder<AIMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AIMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.Provider)
            .HasMaxLength(120);

        builder.Property(m => m.Model)
            .HasMaxLength(200);

        builder.Property(m => m.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });
    }
}
