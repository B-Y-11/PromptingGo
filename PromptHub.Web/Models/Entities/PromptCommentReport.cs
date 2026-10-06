using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PromptCommentReport
{
    public int Id { get; set; }

    public int CommentId { get; set; }

    public PromptComment Comment { get; set; } = null!;

    public string ReporterId { get; set; } = string.Empty;

    public ApplicationUser Reporter { get; set; } = null!;

    public PromptCommentReportReason Reason { get; set; }

    public string? Details { get; set; }

    public PromptCommentReportStatus Status { get; set; }
        = PromptCommentReportStatus.Open;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    public string? ResolvedById { get; set; }

    public ApplicationUser? ResolvedBy { get; set; }

    public string? ResolutionComment { get; set; }
}
