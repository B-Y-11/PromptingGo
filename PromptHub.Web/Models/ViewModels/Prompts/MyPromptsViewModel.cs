using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class MyPromptsViewModel
{
    public IReadOnlyList<MyPromptItemViewModel> Prompts
    {
        get;
        set;
    } = Array.Empty<MyPromptItemViewModel>();
}

public sealed class MyPromptItemViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string DifficultyName { get; set; }
    = string.Empty;

    public IReadOnlyList<PromptTagBadgeViewModel> Tags { get; set; }
    = Array.Empty<PromptTagBadgeViewModel>();

    public PromptStatus Status { get; set; }

    public PromptVersionStatus? WorkingVersionStatus
    {
        get;
        set;
    }

    public string WorkingModerationComment
    {
        get;
        set;
    } = string.Empty;

    public bool HasWorkingCopy =>
        Status == PromptStatus.Published &&
        WorkingVersionStatus.HasValue;

    public bool CanEdit =>
        Status is
            PromptStatus.Draft or
            PromptStatus.Rejected ||
        HasWorkingCopy &&
        WorkingVersionStatus is
            PromptVersionStatus.Draft or
            PromptVersionStatus.Rejected;

    public bool CanSubmitForModeration =>
        Status == PromptStatus.Draft ||
        HasWorkingCopy &&
        WorkingVersionStatus ==
            PromptVersionStatus.Draft;

    public bool IsPendingModeration =>
        Status ==
            PromptStatus.PendingModeration ||
        HasWorkingCopy &&
        WorkingVersionStatus ==
            PromptVersionStatus.PendingModeration;

    public string StatusName { get; set; } = string.Empty;

    public string StatusBadgeClass { get; set; }
        = "text-bg-secondary";

    public bool HasConstructor { get; set; }

    public long ViewCount { get; set; }

    public int VisibleCommentCount { get; set; }

    public long CopyCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public string ModerationComment { get; set; }
    = string.Empty;

    public DateTime? ArchivedAtUtc { get; set; }

    public string ArchiveReason { get; set; }
        = string.Empty;
}
