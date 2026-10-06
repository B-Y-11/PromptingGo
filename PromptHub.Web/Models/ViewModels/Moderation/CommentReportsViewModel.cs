namespace PromptHub.Web.Models.ViewModels.Moderation;

public sealed class CommentReportsViewModel
{
    public string SelectedStatus { get; set; }
        = "open";

    public int OpenCount { get; set; }

    public int ResolvedCount { get; set; }

    public int DismissedCount { get; set; }

    public int TotalCount =>
        OpenCount + ResolvedCount + DismissedCount;

    public IReadOnlyList<CommentReportItemViewModel> Reports { get; set; }
        = Array.Empty<CommentReportItemViewModel>();
}

public sealed class CommentReportItemViewModel
{
    public int ReportId { get; set; }

    public int CommentId { get; set; }

    public string PromptTitle { get; set; }
        = string.Empty;

    public string PromptSlug { get; set; }
        = string.Empty;

    public string CommentAuthorId { get; set; }
        = string.Empty;

    public string CommentAuthorName { get; set; }
        = string.Empty;

    public string ReporterId { get; set; }
        = string.Empty;

    public string ReporterName { get; set; }
        = string.Empty;

    public string CommentBody { get; set; }
        = string.Empty;

    public string ReasonName { get; set; }
        = string.Empty;

    public string StatusName { get; set; }
        = string.Empty;

    public string? Details { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public string? ResolvedByName { get; set; }

    public string? ResolutionComment { get; set; }

    public bool IsOpen { get; set; }

    public bool IsCommentAlreadyHidden { get; set; }

    public string? CommentModerationReason { get; set; }
}
