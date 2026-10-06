using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(
        EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");

        builder.HasKey(tag =>
            tag.Id);

        builder.Property(tag =>
                tag.Name)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(tag =>
                tag.Slug)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(tag =>
                tag.Description)
            .HasMaxLength(400)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(tag =>
                tag.IsActive)
            .HasDefaultValue(true);

        builder.Property(tag =>
                tag.SortOrder)
            .HasDefaultValue(0);

        builder.Property(tag =>
                tag.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(tag =>
                tag.Slug)
            .IsUnique();

        builder.HasIndex(tag =>
            new
            {
                tag.IsActive,
                tag.SortOrder,
                tag.Name
            });
    }
}
