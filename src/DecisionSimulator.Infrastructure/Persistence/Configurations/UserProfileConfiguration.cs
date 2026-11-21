using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.CountryCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(p => p.CountryName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.City)
            .HasMaxLength(120);

        builder.Property(p => p.TimeZoneId)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(p => p.PreferredLanguage)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.CalendarSystem)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(p => p.DefaultCurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(p => p.CurrencyUnitConvention)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(p => p.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(p => p.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        // Location is a computed value object over the stored columns; it must
        // not become a shadow column.
        builder.Ignore(p => p.Location);
    }
}
