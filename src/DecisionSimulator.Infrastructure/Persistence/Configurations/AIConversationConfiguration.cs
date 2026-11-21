using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AIConversationConfiguration : IEntityTypeConfiguration<AIConversation>
{
    public void Configure(EntityTypeBuilder<AIConversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AIConversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.ContextType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(c => c.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Decision>()
            .WithMany()
            .HasForeignKey(c => c.DecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.DecisionId);

        builder.HasIndex(c => c.ContextType);
    }
}
