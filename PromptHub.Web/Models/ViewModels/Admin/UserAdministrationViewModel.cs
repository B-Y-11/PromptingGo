namespace PromptHub.Web.Models.ViewModels.Admin;

public sealed class UserAdministrationViewModel
{
    public IReadOnlyList<UserAdministrationItemViewModel> Users { get; set; }
        = Array.Empty<UserAdministrationItemViewModel>();
}

public sealed class UserAdministrationItemViewModel
{
    public string Id { get; set; }
        = string.Empty;

    public string DisplayName { get; set; }
        = string.Empty;

    public string Email { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public bool IsCurrentUser { get; set; }

    public bool IsAdministrator { get; set; }

    public bool IsModerator { get; set; }

    public int TotalPromptsCount { get; set; }

    public int DraftPromptsCount { get; set; }

    public int PendingPromptsCount { get; set; }

    public int PublishedPromptsCount { get; set; }

    public int ArchivedPromptsCount { get; set; }

    public long TotalViewCount { get; set; }

    public long TotalCopyCount { get; set; }

    public int SubmittedReportsCount { get; set; }

    public int OpenReportsCount { get; set; }
}
