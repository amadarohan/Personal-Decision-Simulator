using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class DataObservationConfiguration : IEntityTypeConfiguration<DataObservation>
{
    public void Configure(EntityTypeBuilder<DataObservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("DataObservations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Value)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(o => o.Unit)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(o => o.ObservationDate)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(o => o.RetrievedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(o => o.ValidFromUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(o => o.ValidUntilUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(o => o.OriginalPayloadHash)
            .HasMaxLength(128);

        builder.Property(o => o.VerificationStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(o => o.Confidence)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(o => o.Notes)
            .HasMaxLength(4000);

        builder.Property(o => o.SourceReference)
            .HasMaxLength(1000);

        builder.HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(o => o.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EconomicIndicator>()
            .WithMany()
            .HasForeignKey(o => o.IndicatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VariableDefinition>()
            .WithMany()
            .HasForeignKey(o => o.VariableDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.DataSourceId, o.ObservationDate });

        builder.HasIndex(o => new { o.IndicatorId, o.ValidFromUtc });

        builder.HasIndex(o => new { o.VariableDefinitionId, o.ObservationDate });

        builder.HasIndex(o => o.VerificationStatus);
    }
}
