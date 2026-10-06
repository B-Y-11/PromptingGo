using Microsoft.AspNetCore.Mvc.Rendering;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Prompts;

namespace PromptHub.Web.Models.ViewModels.Moderation;

public sealed class ModerationReviewViewModel
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

    public int CategoryId { get; set; }

    public bool CategoryIsActive { get; set; } = true;

    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; }
        = Array.Empty<SelectListItem>();

    public string DifficultyName { get; set; }
        = string.Empty;

    public PromptDifficulty Difficulty { get; set; }

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; set; }
        = Array.Empty<SelectListItem>();

    public string AuthorName { get; set; }
        = string.Empty;

    public string AuthorEmail { get; set; }
        = string.Empty;

    public DateTime? SubmittedAtUtc { get; set; }

    public string ReadyText { get; set; }
        = string.Empty;

    public string ConstructorText { get; set; }
        = string.Empty;

    public string UsageInstructions { get; set; }
        = string.Empty;

    public string ExampleResult { get; set; }
        = string.Empty;

    public bool HasConstructor =>
        !string.IsNullOrWhiteSpace(
            ConstructorText);

    public IReadOnlyList<
        ModerationVariableViewModel> Variables
    {
        get;
        set;
    } = Array.Empty<
        ModerationVariableViewModel>();


    public IReadOnlyList<ModerationReviewMediaItemViewModel> MediaItems
    {
        get;
        set;
    } = Array.Empty<ModerationReviewMediaItemViewModel>();



    public bool DuplicateCheckPerformed { get; set; }

    public IReadOnlyList<ModerationDuplicateMatchViewModel>
        DuplicateMatches
    {
        get;
        set;
    } = Array.Empty<ModerationDuplicateMatchViewModel>();
}


public sealed class ModerationDuplicateMatchViewModel
{
    public int PromptId { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;

    public DateTime? PublishedAtUtc { get; set; }

    public int SimilarityPercent { get; set; }

    public int ReadyTextSimilarityPercent { get; set; }

    public int TemplateTextSimilarityPercent { get; set; }
}


public sealed class ModerationReviewMediaItemViewModel
{
    public int Id { get; set; }

    public string Caption { get; set; }
        = string.Empty;

    public string AltText { get; set; }
        = string.Empty;

    public string StatusName { get; set; }
        = string.Empty;

    public bool IsCover { get; set; }

    public int SortOrder { get; set; }
}

public sealed class ModerationVariableViewModel
{
    public string Label { get; set; }
        = string.Empty;

    public string TypeName { get; set; }
        = string.Empty;

    public bool IsRequired { get; set; }

    public string Placeholder { get; set; }
        = string.Empty;
}
