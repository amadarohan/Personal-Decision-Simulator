using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AIProposalConfiguration : IEntityTypeConfiguration<AIProposal>
{
    public void Configure(EntityTypeBuilder<AIProposal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AIProposals");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProposalType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(p => p.PayloadJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(p => p.EvidenceJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(p => p.Confidence)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(p => p.RejectionReason)
            .HasMaxLength(2000);

        builder.Property(p => p.ApprovedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(p => p.RejectedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(p => p.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(p => p.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasOne<Decision>()
            .WithMany()
            .HasForeignKey(p => p.DecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Status);

        builder.HasIndex(p => p.DecisionId);

        builder.HasIndex(p => new { p.Status, p.CreatedAtUtc });
    }
}
