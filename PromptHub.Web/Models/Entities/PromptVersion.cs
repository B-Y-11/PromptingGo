namespace PromptHub.Web.Models.Entities;

using PromptHub.Web.Models.Enums;

public sealed class PromptVersion
{
    public int Id { get; set; }

    public int PromptId { get; set; }

    public Prompt Prompt { get; set; } = null!;

    public int VersionNumber { get; set; }


    public string Title { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string LanguageCode { get; set; }
        = "ru";

    public PromptDifficulty Difficulty { get; set; }
        = PromptDifficulty.Beginner;

    public int CategoryId { get; set; }

    public Category Category { get; set; }
        = null!;

    public PromptVersionStatus Status { get; set; }
    = PromptVersionStatus.Draft;

    public string ReadyText { get; set; } = string.Empty;

    public string TemplateText { get; set; } = string.Empty;

    public string UsageInstructions { get; set; } = string.Empty;

    public string ExampleResult { get; set; } = string.Empty;

    public string ChangeSummary { get; set; } = string.Empty;

    public bool IsCurrent { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? ModeratedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public string? ModerationComment { get; set; }

    public string? ModeratedById { get; set; }

    public ApplicationUser? ModeratedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PromptVersionTag> VersionTags { get; set; }
= new List<PromptVersionTag>();

    public ICollection<PromptVariable> Variables { get; set; }
        = new List<PromptVariable>();

    public ICollection<PromptMedia> MediaItems { get; set; }
    = new List<PromptMedia>();


}
