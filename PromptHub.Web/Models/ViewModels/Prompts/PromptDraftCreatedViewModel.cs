namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class PromptDraftCreatedViewModel
{
    public int PromptId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public IReadOnlyList<CreatedPromptVariableViewModel>
        Variables
    { get; set; }
            = Array.Empty<CreatedPromptVariableViewModel>();
}

public sealed class CreatedPromptVariableViewModel
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
}
