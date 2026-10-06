using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptCommentConfiguration
    : IEntityTypeConfiguration<PromptComment>
{
    public void Configure(
        EntityTypeBuilder<PromptComment> builder)
    {
        builder.ToTable("PromptComments");

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Body)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(comment => comment.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(comment => comment.ModerationReason)
            .HasMaxLength(500);

        builder.Property(comment => comment.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(comment => new
        {
            comment.PromptId,
            comment.Status,
            comment.CreatedAtUtc
        });

        builder.HasIndex(comment => new
        {
            comment.AuthorId,
            comment.CreatedAtUtc
        });

        builder.HasIndex(comment => comment.ParentCommentId);

        builder.HasOne(comment => comment.Prompt)
            .WithMany(prompt => prompt.Comments)
            .HasForeignKey(comment => comment.PromptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.Author)
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(comment => comment.ParentComment)
            .WithMany(comment => comment.Replies)
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(comment => comment.DeletedBy)
            .WithMany()
            .HasForeignKey(comment => comment.DeletedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(comment => comment.HiddenBy)
            .WithMany()
            .HasForeignKey(comment => comment.HiddenById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
