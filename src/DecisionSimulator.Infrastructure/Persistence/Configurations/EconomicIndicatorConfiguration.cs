using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class EconomicIndicatorConfiguration : IEntityTypeConfiguration<EconomicIndicator>
{
    public void Configure(EntityTypeBuilder<EconomicIndicator> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EconomicIndicators");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(i => i.IndicatorCode)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(i => i.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(i => i.CountryCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(i => i.City)
            .HasMaxLength(120);

        builder.Property(i => i.GeographicScope)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(i => i.Unit)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(i => i.CurrentValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(i => i.PreviousValue)
            .HasColumnType(ColumnPrecision.Rate);

        builder.Property(i => i.ObservationDate)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(i => i.RetrievedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(i => i.Confidence)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(i => i.Notes)
            .HasMaxLength(4000);

        builder.Property(i => i.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(i => i.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        // FreshnessWindow is a TimeSpan. Stored as ticks so the value round-trips
        // exactly; a SQL "time" column would cap it at 24 hours.
        builder.Property(i => i.FreshnessWindow)
            .HasConversion(
                v => v.Ticks,
                v => TimeSpan.FromTicks(v))
            .HasColumnType("bigint");

        builder.HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(i => i.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // An indicator is identified by its code within a geography. The city is
        // part of the key because "rent index" means something different in each
        // city, and the application must never conflate them. Two filtered
        // indexes are needed because SQL Server treats NULLs as equal in a
        // unique index, which would otherwise allow only one city-less row.
        builder.HasIndex(i => new { i.IndicatorCode, i.CountryCode, i.City })
            .IsUnique()
            .HasFilter("[City] IS NOT NULL");

        builder.HasIndex(i => new { i.IndicatorCode, i.CountryCode })
            .HasDatabaseName("IX_EconomicIndicators_Code_Country_NoCity")
            .IsUnique()
            .HasFilter("[City] IS NULL");

        builder.HasIndex(i => i.Category);

        builder.HasIndex(i => i.SourceId);
    }
}
