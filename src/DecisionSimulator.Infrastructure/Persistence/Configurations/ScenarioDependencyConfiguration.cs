using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class ScenarioDependencyConfiguration : IEntityTypeConfiguration<ScenarioDependency>
{
    public void Configure(EntityTypeBuilder<ScenarioDependency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ScenarioDependencies");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.RelationshipType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.HasOne<Scenario>()
            .WithMany()
            .HasForeignKey(d => d.DependsOnScenarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.ScenarioId, d.DependsOnScenarioId })
            .IsUnique();

        builder.HasIndex(d => d.DependsOnScenarioId);
    }
}
