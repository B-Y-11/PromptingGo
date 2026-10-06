using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptTagConfiguration
    : IEntityTypeConfiguration<PromptTag>
{
    public void Configure(
        EntityTypeBuilder<PromptTag> builder)
    {
        builder.ToTable("PromptTags");

        builder.HasKey(promptTag =>
            new
            {
                promptTag.PromptId,
                promptTag.TagId
            });

        builder.HasOne(promptTag =>
                promptTag.Prompt)
            .WithMany(prompt =>
                prompt.PromptTags)
            .HasForeignKey(promptTag =>
                promptTag.PromptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(promptTag =>
                promptTag.Tag)
            .WithMany(tag =>
                tag.PromptTags)
            .HasForeignKey(promptTag =>
                promptTag.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(promptTag =>
            promptTag.TagId);
    }
}
