using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptVersionConfiguration
    : IEntityTypeConfiguration<PromptVersion>
{
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.ToTable("PromptVersions");

        builder.HasKey(version => version.Id);

        builder.Property(version => version.VersionNumber)
            .IsRequired();

        builder.Property(version =>
        version.Title)
    .HasMaxLength(180)
    .HasDefaultValue(string.Empty)
    .IsRequired();

        builder.Property(version =>
                version.Summary)
            .HasMaxLength(700)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(version =>
                version.LanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("ru")
            .IsRequired();

        builder.Property(version =>
                version.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(version =>
        version.Status)
    .HasConversion<string>()
    .HasMaxLength(30)
    .HasDefaultValue(
        PromptVersionStatus.Draft)
    .IsRequired();

        builder.Property(version => version.ReadyText)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(version => version.TemplateText)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        // -----------------------------------------------------------------------------
        // EN: GiST trigram indexes support fast nearest-neighbor duplicate searches.
        // RU: GiST trigram-индексы ускоряют поиск ближайших похожих промптов.
        // -----------------------------------------------------------------------------

        builder.HasIndex(
                version =>
                    version.ReadyText)
            .HasMethod(
                "gist")
            .HasOperators(
                "gist_trgm_ops");

        builder.HasIndex(
                version =>
                    version.TemplateText)
            .HasMethod(
                "gist")
            .HasOperators(
                "gist_trgm_ops");

        builder.Property(version => version.UsageInstructions)
            .HasDefaultValue(string.Empty)
            .IsRequired();



        builder.Property(version => version.ExampleResult)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(version => version.ChangeSummary)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(version =>
        version.ModerationComment)
    .HasMaxLength(1000);

        builder.Property(version => version.IsCurrent)
            .HasDefaultValue(false);

        builder.Property(version => version.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(version => new
        {
            version.PromptId,
            version.VersionNumber
        })
        .IsUnique();

        builder.HasIndex(version => new
        {
            version.PromptId,
            version.Status
        });

        // У одного промпта может быть не более одной текущей версии.
        builder.HasIndex(version => version.PromptId)
            .IsUnique()
            .HasFilter("\"IsCurrent\" = TRUE");

        builder.HasOne(version =>
        version.ModeratedBy)
    .WithMany()
    .HasForeignKey(version =>
        version.ModeratedById)
    .OnDelete(
        DeleteBehavior.SetNull);

        builder.HasOne(version =>
        version.Category)
    .WithMany()
    .HasForeignKey(version =>
        version.CategoryId)
    .OnDelete(
        DeleteBehavior.Restrict);

        builder.HasIndex(version =>
    version.CategoryId);

        // -------------------------------------------------------------------------
        // EN: A prompt can have only one active working version.
        // RU: У промпта может быть только одна активная рабочая версия.
        // -------------------------------------------------------------------------

        builder.HasIndex(version =>
                version.PromptId)
            .IsUnique()
            .HasFilter(
                "\"Status\" IN ('Draft', 'PendingModeration', 'Rejected')");

        builder.HasOne(version => version.Prompt)
            .WithMany(prompt => prompt.Versions)
            .HasForeignKey(version => version.PromptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
