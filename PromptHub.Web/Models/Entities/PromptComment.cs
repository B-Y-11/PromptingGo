using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PromptComment
{
    public int Id { get; set; }

    public int PromptId { get; set; }

    public Prompt Prompt { get; set; } = null!;

    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;

    public int? ParentCommentId { get; set; }

    public PromptComment? ParentComment { get; set; }

    public ICollection<PromptComment> Replies { get; set; }
        = new List<PromptComment>();


    public ICollection<PromptCommentReport> Reports { get; set; }
    = new List<PromptCommentReport>();

    public string Body { get; set; } = string.Empty;

    public PromptCommentStatus Status { get; set; }
        = PromptCommentStatus.Visible;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public string? DeletedById { get; set; }

    public ApplicationUser? DeletedBy { get; set; }

    public DateTime? HiddenAtUtc { get; set; }

    public string? HiddenById { get; set; }

    public ApplicationUser? HiddenBy { get; set; }

    public string? ModerationReason { get; set; }
}
