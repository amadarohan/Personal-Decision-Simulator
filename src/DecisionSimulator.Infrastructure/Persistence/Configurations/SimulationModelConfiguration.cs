using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class SimulationModelConfiguration : IEntityTypeConfiguration<SimulationModel>
{
    public void Configure(EntityTypeBuilder<SimulationModel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SimulationModels");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(m => m.Description)
            .HasMaxLength(4000);

        builder.Property(m => m.ModelType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(m => m.ModelVersion)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(m => m.FormulaDefinitionJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.RequiredVariablesJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.OutputDefinitionsJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(m => m.UpdatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        // A model is identified by its name and version together: the same model
        // may exist in several versions so old runs stay reproducible.
        builder.HasIndex(m => new { m.Name, m.ModelVersion })
            .IsUnique();

        builder.HasIndex(m => m.ModelType);
    }
}
