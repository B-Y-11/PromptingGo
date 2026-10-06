using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.Notifications;

public sealed class UserNotificationService : IUserNotificationService
{
    private readonly ApplicationDbContext _dbContext;

    public UserNotificationService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(
        string userId,
        UserNotificationType type,
        string title,
        string message,
        string? url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.CompletedTask;
        }

        _dbContext.UserNotifications.Add(
            new UserNotification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Url = url,
                CreatedAtUtc = DateTime.UtcNow
            });

        return Task.CompletedTask;
    }
}
