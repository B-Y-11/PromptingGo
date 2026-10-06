using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptReportConfiguration
    : IEntityTypeConfiguration<PromptReport>
{
    public void Configure(
        EntityTypeBuilder<PromptReport> builder)
    {
        builder.ToTable("PromptReports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(report => report.Details)
            .HasMaxLength(1000)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(report => report.ResolutionComment)
            .HasMaxLength(1000);

        builder.Property(report => report.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(report => new
        {
            report.Status,
            report.CreatedAtUtc
        });

        builder.HasIndex(report => new
        {
            report.PromptId,
            report.Status
        });

        builder.HasIndex(report => new
        {
            report.ReporterId,
            report.PromptId,
            report.Status
        });

        builder.HasOne(report => report.Prompt)
            .WithMany(prompt => prompt.Reports)
            .HasForeignKey(report => report.PromptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(report => report.Reporter)
            .WithMany(user => user.PromptReports)
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.ResolvedBy)
            .WithMany()
            .HasForeignKey(report => report.ResolvedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
