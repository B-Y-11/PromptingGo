namespace PromptHub.Web.Models.ViewModels.Moderation;

using PromptHub.Web.Models.ViewModels.Prompts;


public sealed class ModerationQueueViewModel
{
    public IReadOnlyList<
        ModerationQueueItemViewModel> Prompts
    {
        get;
        set;
    } = Array.Empty<
        ModerationQueueItemViewModel>();
}

public sealed class ModerationQueueItemViewModel
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

    public string AuthorEmail { get; set; }
        = string.Empty;

    public bool HasConstructor { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
}
