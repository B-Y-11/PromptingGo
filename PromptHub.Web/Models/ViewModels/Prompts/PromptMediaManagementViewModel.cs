using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class PromptMediaManagementViewModel
{

    public int PromptId { get; set; }

    public string PromptTitle { get; set; }
        = string.Empty;

    public PromptStatus PromptStatus { get; set; }

    public string PromptStatusName { get; set; }
        = string.Empty;

    public bool CanManage { get; set; }

    public bool CanUpload { get; set; }

    public string UploadBlockedReason { get; set; }
        = string.Empty;


    public int MaxImagesPerPrompt { get; set; }

    public int ActiveImageCount { get; set; }

    public int RemainingImageSlots { get; set; }

    public IReadOnlyList<PromptMediaItemViewModel> MediaItems
    {
        get;
        set;
    } = Array.Empty<PromptMediaItemViewModel>();


    public bool CanReorder { get; set; }
}

public sealed class PromptMediaItemViewModel
{

    public int Id { get; set; }

    public PromptMediaStatus Status { get; set; }

    public string StatusName { get; set; }
        = string.Empty;

    public string StatusBadgeClass { get; set; }
        = "text-bg-secondary";

    public string OriginalFileName { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }


    public bool HasStoredFile { get; set; }

    public DateTime? ScheduledPurgeAtUtc { get; set; }

    public DateTime? StoragePurgedAtUtc { get; set; }


    public string Caption { get; set; }
        = string.Empty;

    public string AltText { get; set; }
        = string.Empty;

    public string EditableCaption { get; set; }
        = string.Empty;

    public string EditableAltText { get; set; }
        = string.Empty;

    public bool HasPendingMetadataReview { get; set; }

    public string MetadataModerationComment { get; set; }
        = string.Empty;

    public string ModerationComment { get; set; }
        = string.Empty;


    public bool IsCover { get; set; }

    public int SortOrder { get; set; }

    public bool CanEditMetadata { get; set; }

    public bool CanSetCover { get; set; }

    public bool CanMoveUp { get; set; }

    public bool CanMoveDown { get; set; }

    public bool IsReorderable { get; set; }

    public bool CanArchive { get; set; }
}
