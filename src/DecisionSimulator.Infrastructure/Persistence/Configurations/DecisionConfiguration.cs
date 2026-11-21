using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Decisions");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(d => d.Description)
            .HasMaxLength(4000);

        builder.Property(d => d.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(d => d.Importance)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(d => d.Horizon)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(d => d.RiskTolerance)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(d => d.Context)
            .HasMaxLength(8000);

        builder.Property(d => d.DesiredOutcome)
            .HasMaxLength(4000);

        builder.Property(d => d.ConstraintsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(d => d.DecisionDate)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(d => d.TargetDecisionDate)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(d => d.ReviewScheduledForUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(d => d.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(d => d.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(d => d.RowVersion)
            .IsRowVersion();

        // Alternatives and scenarios are part of the aggregate: they are only
        // reachable through the decision, so cascade is correct here.
        builder.HasMany(d => d.Alternatives)
            .WithOne()
            .HasForeignKey(a => a.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Scenarios)
            .WithOne()
            .HasForeignKey(s => s.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Reviews)
            .WithOne()
            .HasForeignKey(r => r.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Documents)
            .WithOne()
            .HasForeignKey(doc => doc.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.Status);

        builder.HasIndex(d => d.Category);

        builder.HasIndex(d => d.DecisionDate);

        builder.HasIndex(d => d.ReviewScheduledForUtc);
    }
}
