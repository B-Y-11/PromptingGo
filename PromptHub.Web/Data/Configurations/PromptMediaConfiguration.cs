using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Data.Configurations;

public sealed class PromptMediaConfiguration
    : IEntityTypeConfiguration<PromptMedia>
{
    public void Configure(
        EntityTypeBuilder<PromptMedia> builder)
    {
        builder.ToTable("PromptMedia");

        builder.HasKey(media => media.Id);


        builder.Property(media => media.MediaType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(media => media.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(media => media.StorageProvider)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();


        // ---------------------------------------------------------------------
        // EN: Physical storage cleanup audit
        // RU: Аудит физической очистки хранилища
        // ---------------------------------------------------------------------

        builder.Property(media =>
                media.StoragePurgeReason)
            .HasConversion<string>()
            .HasMaxLength(64);


        builder.Property(media => media.OriginalFileName)
            .HasMaxLength(255);

        builder.Property(media => media.ContentType)
            .HasMaxLength(120);

        builder.Property(media => media.StorageKey)
            .HasMaxLength(700);

        builder.Property(media => media.PublicUrl)
            .HasMaxLength(1000);

        builder.Property(media => media.ThumbnailUrl)
            .HasMaxLength(1000);


        builder.Property(media => media.ThumbnailStorageKey)
            .HasMaxLength(700);

        builder.Property(media => media.ThumbnailContentType)
            .HasMaxLength(120);

        builder.Property(media => media.ExternalUrl)
            .HasMaxLength(1000);


        builder.Property(media => media.Caption)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(media => media.AltText)
            .HasMaxLength(300)
            .HasDefaultValue(string.Empty)
            .IsRequired();


        builder.Property(media => media.PendingCaption)
            .HasMaxLength(500);

        builder.Property(media => media.PendingAltText)
            .HasMaxLength(300);

        builder.Property(media => media.MetadataModerationComment)
            .HasMaxLength(1000);

        builder.Property(media => media.SortOrder)
            .HasDefaultValue(0);

        builder.Property(media => media.IsCover)
            .HasDefaultValue(false);


        builder.Property(media => media.ModerationComment)
            .HasMaxLength(1000);

        builder.Property(media => media.ArchiveReason)
            .HasMaxLength(1000);

        builder.Property(media => media.CreatedAtUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");


        builder.HasIndex(media => new
        {
            media.PromptId,
            media.Status,
            media.SortOrder
        });

        builder.HasIndex(media => new
        {
            media.PromptId,
            media.MediaType,
            media.Status
        });

        builder.HasIndex(media => new
        {
            media.UploadedById,
            media.CreatedAtUtc
        });

        builder.HasIndex(media => new
        {
            media.Status,
            media.CreatedAtUtc
        });

        builder.HasIndex(media => media.PromptVersionId);

        builder.HasIndex(media =>
            media.MetadataSubmittedAtUtc);

        builder.HasIndex(media => media.ReplacesMediaId);

        builder.HasIndex(media =>
            new
            {
                media.Status,
                media.StoragePurgedAtUtc,
                media.ArchivedAtUtc
            });

        builder.HasIndex(media =>
            new
            {
                media.Status,
                media.StoragePurgedAtUtc,
                media.ModeratedAtUtc
            });

        // ---------------------------------------------------------------------
        // EN: A prompt can have no more than one cover image.
        // RU: У промпта может быть не более одного изображения-обложки.
        // ---------------------------------------------------------------------

        builder.HasIndex(media =>
                media.PromptId)
            .HasDatabaseName(
                "UX_PromptMedia_PromptId_IsCover")
            .IsUnique()
            .HasFilter(
                "\"IsCover\" = TRUE");



        builder.HasOne(media => media.Prompt)
            .WithMany(prompt => prompt.MediaItems)
            .HasForeignKey(media => media.PromptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(media => media.PromptVersion)
            .WithMany(version => version.MediaItems)
            .HasForeignKey(media => media.PromptVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(media => media.UploadedBy)
            .WithMany()
            .HasForeignKey(media => media.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(media => media.ReplacesMedia)
            .WithMany(media => media.ReplacementMediaItems)
            .HasForeignKey(media => media.ReplacesMediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(media => media.ModeratedBy)
            .WithMany()
            .HasForeignKey(media => media.ModeratedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(media => media.MetadataModeratedBy)
            .WithMany()
            .HasForeignKey(media => media.MetadataModeratedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(media => media.ArchivedBy)
            .WithMany()
            .HasForeignKey(media => media.ArchivedById)
            .OnDelete(DeleteBehavior.SetNull);

        // ---------------------------------------------------------------------
        // EN: Optimistic concurrency token
        // RU: Токен оптимистической блокировки
        // ---------------------------------------------------------------------

        builder.Property(media =>
                media.ConcurrencyToken)
            .IsRequired()
            .IsConcurrencyToken();
    }
}
