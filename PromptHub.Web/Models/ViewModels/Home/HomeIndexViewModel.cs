using PromptHub.Web.Models.ViewModels.Prompts;

namespace PromptHub.Web.Models.ViewModels.Home;

public sealed class HomeIndexViewModel
{

    public IReadOnlyList<HomePromptCardViewModel> TopPrompts
    {
        get;
        set;
    } = Array.Empty<HomePromptCardViewModel>();
}

public sealed class HomePromptCardViewModel
{

    public string Title { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public IReadOnlyList<PromptTagBadgeViewModel> Tags
    {
        get;
        set;
    } = Array.Empty<PromptTagBadgeViewModel>();


    public PromptCardCoverViewModel? Cover { get; set; }


    public string AuthorId { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;

    public bool HasConstructor { get; set; }

    public bool IsFavorite { get; set; }

    public double AverageRating { get; set; }

    public int RatingCount { get; set; }

    public long ViewCount { get; set; }

    public long CopyCount { get; set; }

    public int VisibleCommentCount { get; set; }
}
