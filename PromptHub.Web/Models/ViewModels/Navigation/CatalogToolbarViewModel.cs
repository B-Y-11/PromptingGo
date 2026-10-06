namespace PromptHub.Web.Models.ViewModels.Navigation;

public sealed class CatalogToolbarViewModel
{
    public IReadOnlyList<CatalogToolbarCategoryViewModel> Categories
    {
        get;
        set;
    } = Array.Empty<CatalogToolbarCategoryViewModel>();

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

    public string DefaultSort { get; set; }
        = "newest";

    public int ActiveFilterCount { get; set; }

    public bool HasTag =>
        !string.IsNullOrWhiteSpace(Tag);

    public bool HasCatalogParameters =>
        !string.IsNullOrWhiteSpace(Search) ||
        ActiveFilterCount > 0;

    public bool HasCustomSort =>
        !string.Equals(
            Sort,
            DefaultSort,
            StringComparison.OrdinalIgnoreCase);
}

public sealed class CatalogToolbarCategoryViewModel
{
    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;
}
