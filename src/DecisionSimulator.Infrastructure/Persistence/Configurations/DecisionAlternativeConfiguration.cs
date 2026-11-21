using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class DecisionAlternativeConfiguration : IEntityTypeConfiguration<DecisionAlternative>
{
    public void Configure(EntityTypeBuilder<DecisionAlternative> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("DecisionAlternatives");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.Description)
            .HasMaxLength(4000);

        builder.Property(a => a.EstimatedInitialCost)
            .HasColumnType(ColumnPrecision.Money);

        builder.Property(a => a.EstimatedRecurringCost)
            .HasColumnType(ColumnPrecision.Money);

        builder.Property(a => a.ExpectedBenefits)
            .HasMaxLength(4000);

        builder.Property(a => a.KnownRisks)
            .HasMaxLength(4000);

        builder.Property(a => a.RequiredResourcesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.ConstraintsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(a => a.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(a => a.DecisionId);

        // A decision may only have one baseline alternative. The domain enforces
        // this too, but a filtered unique index makes the invariant survive a
        // direct database write or a concurrent insert.
        builder.HasIndex(a => new { a.DecisionId, a.IsBaseline })
            .HasDatabaseName("IX_DecisionAlternatives_OneBaselinePerDecision")
            .IsUnique()
            .HasFilter("[IsBaseline] = 1");
    }
}
