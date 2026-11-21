using DecisionSimulator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DecisionSimulator.Infrastructure.Persistence.Configurations;

public sealed class DecisionReviewConfiguration : IEntityTypeConfiguration<DecisionReview>
{
    public void Configure(EntityTypeBuilder<DecisionReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("DecisionReviews");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReviewDate)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.Property(r => r.ActualOutcome)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(r => r.ExpectedOutcome)
            .HasMaxLength(8000);

        builder.Property(r => r.OutcomeDifference)
            .HasMaxLength(8000);

        builder.Property(r => r.WhatWentWell)
            .HasMaxLength(8000);

        builder.Property(r => r.WhatWentWrong)
            .HasMaxLength(8000);

        builder.Property(r => r.LessonsLearned)
            .HasMaxLength(8000);

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnType(ColumnPrecision.Timestamp);

        builder.HasIndex(r => new { r.DecisionId, r.ReviewDate });
    }
}
