using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class UserNotification
{
    public int Id { get; set; }

    public string UserId { get; set; }
        = string.Empty;

    public ApplicationUser User { get; set; }
        = null!;

    public UserNotificationType Type { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Message { get; set; }
        = string.Empty;

    public string? Url { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ReadAtUtc { get; set; }
}
