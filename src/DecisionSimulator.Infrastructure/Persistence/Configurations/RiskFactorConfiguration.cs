using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class RiskFactorConfiguration : IEntityTypeConfiguration<RiskFactor>
{
    public void Configure(EntityTypeBuilder<RiskFactor> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RiskFactors");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.Description)
            .HasMaxLength(4000);

        builder.Property(r => r.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.GeographicScope)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.ImpactDirection)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.ImpactMagnitude)
            .HasMaxLength(1000);

        builder.Property(r => r.LikelihoodAssessment)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.Confidence)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(r => r.SourceReference)
            .HasMaxLength(1000);

        builder.Property(r => r.LastVerifiedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(r => r.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(r => r.Category);

        builder.HasIndex(r => r.IsActive);

        builder.HasIndex(r => new { r.GeographicScope, r.Category });
    }
}
