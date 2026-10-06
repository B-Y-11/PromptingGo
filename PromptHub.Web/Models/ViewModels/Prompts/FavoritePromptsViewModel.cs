namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class FavoritePromptsViewModel
{
    public IReadOnlyList<PromptCatalogItemViewModel>
        Prompts
    { get; set; }
        = Array.Empty<PromptCatalogItemViewModel>();
}
