using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptFavoriteConfiguration
    : IEntityTypeConfiguration<PromptFavorite>
{
    public void Configure(
        EntityTypeBuilder<PromptFavorite> builder)
    {
        builder.ToTable("PromptFavorites");

        builder.HasKey(favorite => new
        {
            favorite.UserId,
            favorite.PromptId
        });

        builder.Property(favorite =>
                favorite.CreatedAtUtc)
            .HasDefaultValueSql(
                "CURRENT_TIMESTAMP");

        builder.HasIndex(favorite => new
        {
            favorite.UserId,
            favorite.CreatedAtUtc
        });

        builder.HasOne(favorite =>
                favorite.User)
            .WithMany(user =>
                user.FavoritePrompts)
            .HasForeignKey(favorite =>
                favorite.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(favorite =>
                favorite.Prompt)
            .WithMany(prompt =>
                prompt.Favorites)
            .HasForeignKey(favorite =>
                favorite.PromptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
