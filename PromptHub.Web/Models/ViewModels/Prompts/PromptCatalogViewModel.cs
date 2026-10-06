namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class PromptCatalogViewModel
{

    public string Search { get; set; }
        = string.Empty;

    public string Category { get; set; }
        = string.Empty;

    public string Difficulty { get; set; }
    = string.Empty;

    public string Tag { get; set; }
        = string.Empty;

    public string TagName { get; set; }
        = string.Empty;

    public string Sort { get; set; }
        = "newest";



    public int Page { get; set; }
        = 1;

    public int PageSize { get; set; }
        = 24;

    public int TotalCount { get; set; }

    public int TotalPages =>
        TotalCount == 0
            ? 1
            : (int)Math.Ceiling(
                TotalCount /
                (double)PageSize);

    public bool HasPreviousPage =>
        Page > 1;

    public bool HasNextPage =>
        Page < TotalPages;


    public IReadOnlyList<PromptCatalogItemViewModel> Prompts
    {
        get;
        set;
    } = Array.Empty<PromptCatalogItemViewModel>();

    public IReadOnlyList<PromptCategoryFilterViewModel> Categories
    {
        get;
        set;
    } = Array.Empty<PromptCategoryFilterViewModel>();
}

public sealed class PromptCatalogItemViewModel
{

    public int PromptId { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public string CategorySlug { get; set; }
    = string.Empty;

    public string DifficultyName { get; set; }
        = string.Empty;

    public string DifficultyValue { get; set; }
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

    public bool IsFavorite { get; set; }

    public int? CurrentUserRating { get; set; }


    public bool HasConstructor { get; set; }

    public double AverageRating { get; set; }

    public int RatingCount { get; set; }

    public int VisibleCommentCount { get; set; }

    public long ViewCount { get; set; }

    public long CopyCount { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
}

public sealed class PromptCategoryFilterViewModel
{
    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;
}
