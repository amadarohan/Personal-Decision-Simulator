using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AppSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(s => s.Key)
            .IsUnique();
    }
}
