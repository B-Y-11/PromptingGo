using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.Notifications;

public interface IUserNotificationService
{
    Task AddAsync(
        string userId,
        UserNotificationType type,
        string title,
        string message,
        string? url,
        CancellationToken cancellationToken);
}
