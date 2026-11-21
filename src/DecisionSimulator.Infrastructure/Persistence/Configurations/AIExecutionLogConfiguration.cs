using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AIExecutionLogConfiguration : IEntityTypeConfiguration<AIExecutionLog>
{
    public void Configure(EntityTypeBuilder<AIExecutionLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AIExecutionLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Operation)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.InputJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(l => l.OutputJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(l => l.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(l => l.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(l => l.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasOne<AIProposal>()
            .WithMany()
            .HasForeignKey(l => l.AIProposalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.AIProposalId);

        builder.HasIndex(l => l.CreatedAtUtc);
    }
}
