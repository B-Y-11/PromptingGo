using PromptHub.Web.Models.ViewModels.Prompts;
using PromptHub.Web.Models.ViewModels.AuthorSupport;

namespace PromptHub.Web.Models.ViewModels.Authors;

public sealed class AuthorProfileViewModel
{
    public string AuthorId { get; set; }
        = string.Empty;

    public string DisplayName { get; set; }
        = string.Empty;

    public string Bio { get; set; }
        = string.Empty;

    public AuthorSupportViewModel? AuthorSupport { get; set; }

    public string? AvatarContentUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int PublishedPromptsCount { get; set; }

    public long TotalViewCount { get; set; }

    public long TotalCopyCount { get; set; }

    public double AverageRating { get; set; }

    public int RatingCount { get; set; }

    public int VisibleCommentCount { get; set; }

    public IReadOnlyList<PromptCatalogItemViewModel> Prompts { get; set; }
        = Array.Empty<PromptCatalogItemViewModel>();
}
