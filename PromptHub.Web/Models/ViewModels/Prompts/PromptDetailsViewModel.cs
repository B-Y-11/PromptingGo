using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.PromptRendering;
using PromptHub.Web.Models.ViewModels.AuthorSupport;

namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class PromptDetailsViewModel
{

    public string Slug { get; set; }
        = string.Empty;

    public int PromptId { get; set; }

    public bool IsPreview { get; set; }

    public bool CanEdit { get; set; }

    public bool CanSubmitForModeration { get; set; }

    public bool CanUnpublish { get; set; }

    public bool IsFavorite { get; set; }

    public bool HasCurrentUserOpenReport { get; set; }


    public double AverageRating { get; set; }

    public int RatingCount { get; set; }

    public int? CurrentUserRating { get; set; }


    public string Title { get; set; }
        = string.Empty;

    public string Summary { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public string CategorySlug { get; set; }
    = string.Empty;

    public IReadOnlyList<PromptTagBadgeViewModel> Tags
    {
        get;
        set;
    } = Array.Empty<PromptTagBadgeViewModel>();

    public string AuthorId { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;

    public AuthorSupportViewModel? AuthorSupport { get; set; }

    public string DifficultyName { get; set; }
        = string.Empty;

    public string DifficultyValue { get; set; }
    = string.Empty;

    public string LanguageCode { get; set; }
        = string.Empty;

    public int VersionNumber { get; set; }

    public long ViewCount { get; set; }

    public long CopyCount { get; set; }


    public string ReadyText { get; set; }
        = string.Empty;

    public bool HasConstructor { get; set; }

    public string TemplateText { get; set; }
        = string.Empty;

    public string UsageInstructions { get; set; }
        = string.Empty;

    public string ExampleResult { get; set; }
        = string.Empty;

    public IReadOnlyList<PromptVariableFieldViewModel> Variables
    {
        get;
        set;
    } = Array.Empty<PromptVariableFieldViewModel>();

    public Dictionary<string, string?> Values { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    public string? RenderedText { get; set; }

    public bool HasRenderedResult =>
        !string.IsNullOrWhiteSpace(
            RenderedText);

    public IReadOnlyList<PromptRenderError> Errors
    {
        get;
        set;
    } = Array.Empty<PromptRenderError>();


    public IReadOnlyList<PromptMediaGalleryItemViewModel> MediaItems
    {
        get;
        set;
    } = Array.Empty<PromptMediaGalleryItemViewModel>();

    public bool HasMedia =>
        MediaItems.Count > 0;

    public bool HasExampleContent =>
        !string.IsNullOrWhiteSpace(
            ExampleResult) ||
        HasMedia;


    public IReadOnlyList<PromptCommentItemViewModel> Comments
    {
        get;
        set;
    } = Array.Empty<PromptCommentItemViewModel>();

    public int CommentCount =>
        Comments.Count;
}

public sealed class PromptMediaGalleryItemViewModel
{

    public int Id { get; set; }

    public string Caption { get; set; }
        = string.Empty;

    public string AltText { get; set; }
        = string.Empty;

    public int SortOrder { get; set; }

    public bool IsCover { get; set; }

    public PromptMediaStatus Status { get; set; }

    public string StatusName { get; set; }
        = string.Empty;
}

public sealed class PromptCommentItemViewModel
{

    public int CommentId { get; set; }

    public string AuthorId { get; set; }
        = string.Empty;

    public string AuthorName { get; set; }
        = string.Empty;


    public string Body { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool WasEdited =>
        UpdatedAtUtc.HasValue;

    public bool IsHiddenByModerator { get; set; }

    public string? ModerationReason { get; set; }


    public bool CanDelete { get; set; }

    public bool CanHide { get; set; }

    public bool CanReport { get; set; }

    public bool HasOpenReportByCurrentUser { get; set; }
}

public sealed class PromptVariableFieldViewModel
{

    public string Key { get; set; }
        = string.Empty;

    public string Label { get; set; }
        = string.Empty;

    public PromptVariableType VariableType { get; set; }

    public string Placeholder { get; set; }
        = string.Empty;

    public string HelpText { get; set; }
        = string.Empty;

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }
        = string.Empty;

    public IReadOnlyList<PromptSelectOptionViewModel> Options
    {
        get;
        set;
    } = Array.Empty<PromptSelectOptionViewModel>();
}

public sealed class PromptSelectOptionViewModel
{
    public string Value { get; set; }
        = string.Empty;

    public string Label { get; set; }
        = string.Empty;
}
