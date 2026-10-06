namespace PromptHub.Web.Models.ViewModels.Notifications;

public sealed class UserNotificationsViewModel
{
    public int UnreadCount { get; set; }

    public int ReadCount { get; set; }

    public IReadOnlyList<UserNotificationItemViewModel> Notifications { get; set; }
        = Array.Empty<UserNotificationItemViewModel>();
}

public sealed class UserNotificationItemViewModel
{
    public int Id { get; set; }

    public string TypeName { get; set; }
        = string.Empty;

    public string Title { get; set; }
        = string.Empty;

    public string Message { get; set; }
        = string.Empty;

    public string? Url { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public bool IsRead =>
        ReadAtUtc.HasValue;
}
