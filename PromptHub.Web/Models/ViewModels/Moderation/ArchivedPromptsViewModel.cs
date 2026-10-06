namespace PromptHub.Web.Models.ViewModels.Moderation;

public sealed class ArchivedPromptsViewModel
{
    public IReadOnlyList<ArchivedPromptItemViewModel> Prompts { get; set; }
        = Array.Empty<ArchivedPromptItemViewModel>();
}

public sealed class ArchivedPromptItemViewModel
{
    public int Id { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;

    public string ArchivedByName { get; set; }
        = string.Empty;

    public string ArchiveReason { get; set; }
        = string.Empty;

    public DateTime? ArchivedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public long ViewCount { get; set; }

    public long CopyCount { get; set; }
}
