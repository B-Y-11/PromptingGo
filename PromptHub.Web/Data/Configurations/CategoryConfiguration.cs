using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class CategoryConfiguration
    : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(category => category.Slug)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(category => category.SortOrder)
            .HasDefaultValue(0);

        builder.Property(category => category.IsActive)
            .HasDefaultValue(true);

        builder.Property(category => category.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(category => category.Slug)
            .IsUnique();

        builder.HasIndex(category => new
        {
            category.ParentId,
            category.IsActive,
            category.SortOrder
        });

        builder.HasOne(category => category.Parent)
            .WithMany(category => category.Children)
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
