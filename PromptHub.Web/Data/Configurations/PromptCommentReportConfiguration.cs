using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptCommentReportConfiguration
    : IEntityTypeConfiguration<PromptCommentReport>
{
    public void Configure(
        EntityTypeBuilder<PromptCommentReport> builder)
    {
        builder.ToTable("PromptCommentReports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(report => report.Details)
            .HasMaxLength(1000);

        builder.Property(report => report.ResolutionComment)
            .HasMaxLength(1000);

        builder.Property(report => report.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(report => new
        {
            report.CommentId,
            report.Status,
            report.CreatedAtUtc
        });

        builder.HasIndex(report => new
        {
            report.ReporterId,
            report.CreatedAtUtc
        });

        builder.HasIndex(report => new
        {
            report.Status,
            report.CreatedAtUtc
        });

        builder.HasOne(report => report.Comment)
            .WithMany(comment => comment.Reports)
            .HasForeignKey(report => report.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(report => report.Reporter)
            .WithMany()
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.ResolvedBy)
            .WithMany()
            .HasForeignKey(report => report.ResolvedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
