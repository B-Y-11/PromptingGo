namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class AllTagsViewModel
{
    public int TagCount { get; set; }

    public IReadOnlyList<AllTagsGroupViewModel> Groups { get; set; }
        = Array.Empty<AllTagsGroupViewModel>();
}

public sealed class AllTagsGroupViewModel
{
    public string Letter { get; set; }
        = string.Empty;

    public IReadOnlyList<AllTagsItemViewModel> Tags { get; set; }
        = Array.Empty<AllTagsItemViewModel>();
}

public sealed class AllTagsItemViewModel
{
    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public int PromptCount { get; set; }
}
