using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PromptMedia
{

    public int Id { get; set; }

    public int PromptId { get; set; }

    public Prompt Prompt { get; set; } = null!;

    public int? PromptVersionId { get; set; }

    public PromptVersion? PromptVersion { get; set; }

    public string UploadedById { get; set; }
        = string.Empty;

    public ApplicationUser UploadedBy { get; set; }
        = null!;


    public PromptMediaType MediaType { get; set; }
        = PromptMediaType.Image;

    public PromptMediaStatus Status { get; set; }
        = PromptMediaStatus.Draft;

    public MediaStorageProvider StorageProvider { get; set; }
        = MediaStorageProvider.Local;

    public string? OriginalFileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public string? StorageKey { get; set; }

    public string? PublicUrl { get; set; }

    public string? ThumbnailUrl { get; set; }


    public int? OriginalWidth { get; set; }

    public int? OriginalHeight { get; set; }


    public string? ThumbnailStorageKey { get; set; }

    public string? ThumbnailContentType { get; set; }

    public long? ThumbnailFileSizeBytes { get; set; }

    public int? ThumbnailWidth { get; set; }

    public int? ThumbnailHeight { get; set; }

    public string? ExternalUrl { get; set; }



    public DateTime? StoragePurgedAtUtc { get; set; }

    public PromptMediaStoragePurgeReason? StoragePurgeReason
    {
        get;
        set;
    }




    public string Caption { get; set; }
        = string.Empty;

    public string AltText { get; set; }
        = string.Empty;

    public int SortOrder { get; set; }

    public bool IsCover { get; set; }


    public string? PendingCaption { get; set; }

    public string? PendingAltText { get; set; }

    public DateTime? MetadataSubmittedAtUtc { get; set; }

    public DateTime? MetadataModeratedAtUtc { get; set; }

    public string? MetadataModeratedById { get; set; }

    public ApplicationUser? MetadataModeratedBy { get; set; }

    public string? MetadataModerationComment { get; set; }


    public int? ReplacesMediaId { get; set; }

    public PromptMedia? ReplacesMedia { get; set; }

    public ICollection<PromptMedia> ReplacementMediaItems { get; set; }
        = new List<PromptMedia>();

    // -------------------------------------------------------------------------
    // EN: Optimistic concurrency control
    // RU: Оптимистическая блокировка конкурентных изменений
    // -------------------------------------------------------------------------

    public Guid ConcurrencyToken { get; set; }
        = Guid.NewGuid();



    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? ModeratedAtUtc { get; set; }

    public string? ModeratedById { get; set; }

    public ApplicationUser? ModeratedBy { get; set; }

    public string? ModerationComment { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public string? ArchivedById { get; set; }

    public ApplicationUser? ArchivedBy { get; set; }

    public string? ArchiveReason { get; set; }
}
