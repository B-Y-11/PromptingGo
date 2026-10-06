namespace PromptHub.Web.Models.ViewModels.Navigation;

public sealed class CatalogNavigationViewModel
{
    public IReadOnlyList<CatalogNavigationItemViewModel> Categories
    {
        get;
        set;
    } = Array.Empty<CatalogNavigationItemViewModel>();

    public IReadOnlyList<CatalogNavigationItemViewModel> PopularTags
    {
        get;
        set;
    } = Array.Empty<CatalogNavigationItemViewModel>();
}

public sealed class CatalogNavigationItemViewModel
{
    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public int PromptCount { get; set; }
}
