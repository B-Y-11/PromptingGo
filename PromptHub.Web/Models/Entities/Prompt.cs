using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class Prompt
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = "ru";

    public PromptStatus Status { get; set; } = PromptStatus.Draft;

    public PromptDifficulty Difficulty { get; set; }
        = PromptDifficulty.Beginner;

    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public long ViewCount { get; set; }

    public long CopyCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? ModeratedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public string? ModerationComment { get; set; }

    public string? ModeratedById { get; set; }

    public ApplicationUser? ModeratedBy { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public string? ArchivedById { get; set; }

    public ApplicationUser? ArchivedBy { get; set; }

    public string? ArchiveReason { get; set; }

    public ICollection<PromptVersion> Versions { get; set; }
    = new List<PromptVersion>();

    public ICollection<PromptFavorite> Favorites { get; set; }
    = new List<PromptFavorite>();


    public ICollection<PromptTag> PromptTags { get; set; }
    = new List<PromptTag>();

    public ICollection<PromptRating> Ratings { get; set; }
    = new List<PromptRating>();

    public ICollection<PromptReport> Reports { get; set; }
    = new List<PromptReport>();

    public ICollection<PromptComment> Comments { get; set; }
    = new List<PromptComment>();

    public ICollection<PromptMedia> MediaItems { get; set; }
    = new List<PromptMedia>();
}
