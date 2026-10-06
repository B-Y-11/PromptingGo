using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptConfiguration
    : IEntityTypeConfiguration<Prompt>
{
    public void Configure(EntityTypeBuilder<Prompt> builder)
    {
        builder.ToTable("Prompts");

        builder.HasKey(prompt => prompt.Id);

        builder.Property(prompt => prompt.Title)
            .HasMaxLength(180)
            .IsRequired();

        builder.Property(prompt => prompt.Slug)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(prompt => prompt.Summary)
            .HasMaxLength(700)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(prompt => prompt.LanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("ru")
            .IsRequired();

        builder.Property(prompt => prompt.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(prompt => prompt.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(prompt => prompt.ViewCount)
            .HasDefaultValue(0L);

        builder.Property(prompt => prompt.CopyCount)
            .HasDefaultValue(0L);

        builder.Property(prompt => prompt.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(prompt => prompt.Slug)
            .IsUnique();

        builder.HasIndex(prompt => new
        {
            prompt.CategoryId,
            prompt.Status,
            prompt.CreatedAtUtc
        });

        builder.HasIndex(prompt => new
        {
            prompt.AuthorId,
            prompt.Status
        });

        builder.HasIndex(prompt => new
        {
            prompt.Status,
            prompt.PublishedAtUtc
        });

        builder.HasOne(prompt => prompt.Author)
            .WithMany(user => user.AuthoredPrompts)
            .HasForeignKey(prompt => prompt.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(prompt => prompt.Category)
            .WithMany(category => category.Prompts)
            .HasForeignKey(prompt => prompt.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(prompt =>
        prompt.ModerationComment).HasMaxLength(1000);

        builder.HasOne(prompt =>
                prompt.ModeratedBy)
            .WithMany()
            .HasForeignKey(prompt =>
                prompt.ModeratedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(prompt =>
                prompt.ArchiveReason)
            .HasMaxLength(1000);

        builder.HasOne(prompt =>
                prompt.ArchivedBy)
            .WithMany()
            .HasForeignKey(prompt =>
                prompt.ArchivedById)
            .OnDelete(DeleteBehavior.SetNull);
    }


}
