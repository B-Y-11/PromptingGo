using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptRatingConfiguration
    : IEntityTypeConfiguration<PromptRating>
{
    public void Configure(
        EntityTypeBuilder<PromptRating> builder)
    {
        builder.ToTable(
            "PromptRatings",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PromptRatings_Score",
                    "\"Score\" >= 1 AND \"Score\" <= 5");
            });

        builder.HasKey(rating => new
        {
            rating.UserId,
            rating.PromptId
        });

        builder.Property(rating =>
                rating.Score)
            .IsRequired();

        builder.Property(rating =>
                rating.CreatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        builder.Property(rating =>
                rating.UpdatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        builder.HasIndex(rating =>
            rating.PromptId);

        builder.HasIndex(rating => new
        {
            rating.UserId,
            rating.UpdatedAtUtc
        });

        builder.HasOne(rating =>
                rating.User)
            .WithMany(user =>
                user.PromptRatings)
            .HasForeignKey(rating =>
                rating.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rating =>
                rating.Prompt)
            .WithMany(prompt =>
                prompt.Ratings)
            .HasForeignKey(rating =>
                rating.PromptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
