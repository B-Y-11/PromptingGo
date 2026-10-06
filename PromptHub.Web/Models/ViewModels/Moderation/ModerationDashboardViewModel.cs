using PromptHub.Web.Models.ViewModels.Prompts;


namespace PromptHub.Web.Models.ViewModels.Moderation;

public sealed class ModerationDashboardViewModel
{
    public int PendingPromptsCount { get; set; }


    public int PendingMediaCount { get; set; }

    public int NewReportsCount { get; set; }

    public int ArchivedPromptsCount { get; set; }

    public int OpenCommentReportCount { get; set; }

    public IReadOnlyList<ModerationDashboardPromptItemViewModel> PendingPrompts { get; set; }
        = Array.Empty<ModerationDashboardPromptItemViewModel>();

    public IReadOnlyList<ModerationDashboardReportItemViewModel> NewReports { get; set; }
        = Array.Empty<ModerationDashboardReportItemViewModel>();

    public IReadOnlyList<ModerationDashboardArchivedPromptItemViewModel> ArchivedPrompts { get; set; }
        = Array.Empty<ModerationDashboardArchivedPromptItemViewModel>();
}

public sealed class ModerationDashboardPromptItemViewModel
{
    public int Id { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public IReadOnlyList<PromptTagBadgeViewModel> Tags { get; set; }
    = Array.Empty<PromptTagBadgeViewModel>();

    public string AuthorName { get; set; }
        = string.Empty;

    public string DifficultyName { get; set; }
        = string.Empty;

    public DateTime? SubmittedAtUtc { get; set; }
}

public sealed class ModerationDashboardReportItemViewModel
{
    public int Id { get; set; }

    public string PromptTitle { get; set; }
        = string.Empty;

    public string PromptSlug { get; set; }
        = string.Empty;

    public string ReporterName { get; set; }
        = string.Empty;

    public string ReasonName { get; set; }
        = string.Empty;

    public string Details { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ModerationDashboardArchivedPromptItemViewModel
{
    public int Id { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;

    public string ArchivedByName { get; set; }
        = string.Empty;

    public string ArchiveReason { get; set; }
        = string.Empty;

    public DateTime? ArchivedAtUtc { get; set; }
}
