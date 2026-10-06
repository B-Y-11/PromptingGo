namespace PromptHub.Web.Models.ViewModels.Moderation;

public sealed class PromptMediaModerationQueueViewModel
{
    public int PendingCount { get; set; }

    public int Page { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyList<PromptMediaModerationItemViewModel> MediaItems
    {
        get;
        set;
    } = Array.Empty<PromptMediaModerationItemViewModel>();
}

public sealed class PromptMediaModerationItemViewModel
{

    public int Id { get; set; }

    public int PromptId { get; set; }

    public string PromptTitle { get; set; }
        = string.Empty;

    public string PromptSlug { get; set; }
        = string.Empty;

    public string AuthorId { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;


    public bool IsNewMediaReview { get; set; }

    public bool IsMetadataReview { get; set; }


    public string OriginalFileName { get; set; }
        = string.Empty;

    public string ContentType { get; set; }
        = string.Empty;

    public long FileSizeBytes { get; set; }

    public bool IsCover { get; set; }

    public string CurrentCaption { get; set; }
        = string.Empty;

    public string CurrentAltText { get; set; }
        = string.Empty;

    public string ProposedCaption { get; set; }
        = string.Empty;

    public string ProposedAltText { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ReviewSubmittedAtUtc { get; set; }
}
