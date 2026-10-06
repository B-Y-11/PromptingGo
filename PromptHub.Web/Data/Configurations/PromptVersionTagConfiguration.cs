using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptVersionTagConfiguration
    : IEntityTypeConfiguration<PromptVersionTag>
{
    public void Configure(
        EntityTypeBuilder<PromptVersionTag> builder)
    {
        builder.ToTable(
            "PromptVersionTags");

        builder.HasKey(versionTag =>
            new
            {
                versionTag.PromptVersionId,
                versionTag.TagId
            });

        builder.HasOne(versionTag =>
                versionTag.PromptVersion)
            .WithMany(version =>
                version.VersionTags)
            .HasForeignKey(versionTag =>
                versionTag.PromptVersionId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasOne(versionTag =>
                versionTag.Tag)
            .WithMany()
            .HasForeignKey(versionTag =>
                versionTag.TagId)
            .OnDelete(
                DeleteBehavior.Restrict);

        builder.HasIndex(versionTag =>
            versionTag.TagId);
    }
}
