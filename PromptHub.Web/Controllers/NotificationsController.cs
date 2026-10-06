using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Notifications;

namespace PromptHub.Web.Controllers;

[Authorize]
[Route("notifications")]
public sealed class NotificationsController : Controller
{

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }


    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var unreadCount =
            await CountUnreadNotificationsAsync(
                userId,
                cancellationToken);

        var readCount =
            await CountReadNotificationsAsync(
                userId,
                cancellationToken);

        var notifications =
            await LoadUserNotificationsAsync(
                userId,
                cancellationToken);

        var model =
            new UserNotificationsViewModel
            {
                UnreadCount =
                    unreadCount,

                ReadCount =
                    readCount,

                Notifications = notifications
                    .Select(MapNotificationItem)
                    .ToArray()
            };

        return View(model);
    }


    [HttpPost("{id:int}/read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(
        int id,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var notification =
            await FindUserNotificationAsync(
                id,
                userId,
                cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc =
                DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(notification.Url))
        {
            return Redirect(notification.Url);
        }

        return RedirectToAction(
            nameof(Index));
    }

    [HttpPost("read-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var unreadNotifications =
            await _dbContext.UserNotifications
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.ReadAtUtc == null)
                .ToListAsync(
                    cancellationToken);

        var now =
            DateTime.UtcNow;

        foreach (var notification in unreadNotifications)
        {
            notification.ReadAtUtc =
                now;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return RedirectToAction(
            nameof(Index));
    }


    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotification(
        int id,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var notification =
            await FindUserNotificationAsync(
                id,
                userId,
                cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        _dbContext.UserNotifications.Remove(
            notification);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Уведомление удалено.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpPost("delete-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReadNotifications(
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var readNotifications =
            await _dbContext.UserNotifications
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.ReadAtUtc != null)
                .ToListAsync(
                    cancellationToken);

        if (readNotifications.Count == 0)
        {
            TempData["SuccessMessage"] =
                "Прочитанных уведомлений нет.";

            return RedirectToAction(
                nameof(Index));
        }

        _dbContext.UserNotifications.RemoveRange(
            readNotifications);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            $"Удалено прочитанных уведомлений: {readNotifications.Count}.";

        return RedirectToAction(
            nameof(Index));
    }


    private async Task<int> CountUnreadNotificationsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserNotifications
            .AsNoTracking()
            .CountAsync(
                notification =>
                    notification.UserId == userId &&
                    notification.ReadAtUtc == null,
                cancellationToken);
    }

    private async Task<int> CountReadNotificationsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserNotifications
            .AsNoTracking()
            .CountAsync(
                notification =>
                    notification.UserId == userId &&
                    notification.ReadAtUtc != null,
                cancellationToken);
    }

    private async Task<List<UserNotification>> LoadUserNotificationsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserNotifications
            .AsNoTracking()
            .Where(notification =>
                notification.UserId == userId)
            .OrderBy(notification =>
                notification.ReadAtUtc.HasValue)
            .ThenByDescending(notification =>
                notification.CreatedAtUtc)
            .Take(100)
            .ToListAsync(
                cancellationToken);
    }

    private async Task<UserNotification?> FindUserNotificationAsync(
        int id,
        string userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserNotifications
            .SingleOrDefaultAsync(
                notification =>
                    notification.Id == id &&
                    notification.UserId == userId,
                cancellationToken);
    }


    private static UserNotificationItemViewModel MapNotificationItem(
        UserNotification notification)
    {
        return new UserNotificationItemViewModel
        {
            Id =
                notification.Id,

            TypeName =
                GetNotificationTypeName(
                    notification.Type),

            Title =
                notification.Title,

            Message =
                notification.Message,

            Url =
                notification.Url,

            CreatedAtUtc =
                notification.CreatedAtUtc,

            ReadAtUtc =
                notification.ReadAtUtc
        };
    }

    private static string GetNotificationTypeName(
        UserNotificationType type)
    {
        return type switch
        {
            UserNotificationType.PromptApproved =>
                "Публикация",

            UserNotificationType.PromptRejected =>
                "Исправление",

            UserNotificationType.PromptArchived =>
                "Скрытие",

            UserNotificationType.PromptRestored =>
                "Восстановление",

            UserNotificationType.PromptSubmitted =>
                "Модерация",

            UserNotificationType.PromptCommentAdded =>
                "Комментарий",

            UserNotificationType.PromptCommentHidden =>
                "Модерация",

            UserNotificationType.PromptMediaApproved =>
                "Медиа",

            UserNotificationType.PromptMediaRejected =>
                "Медиа",

            _ =>
                "Уведомление"
        };
    }
}
