using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("DataSources");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.ProviderType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(s => s.ReliabilityLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(s => s.BaseUrl)
            .HasMaxLength(2000);

        builder.Property(s => s.Description)
            .HasMaxLength(4000);

        builder.Property(s => s.UpdateFrequency)
            .HasMaxLength(200);

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(s => s.Name)
            .IsUnique();

        builder.HasIndex(s => s.IsActive);
    }
}
