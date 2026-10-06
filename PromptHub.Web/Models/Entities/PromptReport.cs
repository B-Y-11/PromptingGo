using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PromptReport
{
    public int Id { get; set; }

    public int PromptId { get; set; }

    public Prompt Prompt { get; set; } = null!;

    public string ReporterId { get; set; } = string.Empty;

    public ApplicationUser Reporter { get; set; } = null!;

    public PromptReportReason Reason { get; set; }

    public string Details { get; set; } = string.Empty;

    public PromptReportStatus Status { get; set; }
        = PromptReportStatus.New;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    public string? ResolvedById { get; set; }

    public ApplicationUser? ResolvedBy { get; set; }

    public string? ResolutionComment { get; set; }
}
