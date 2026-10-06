using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.ViewComponents;

public sealed class NotificationBadgeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationBadgeViewComponent(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var userId =
            _userManager.GetUserId(
                HttpContext.User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Content(string.Empty);
        }

        var cancellationToken =
            HttpContext.RequestAborted;

        var unreadCount =
            await _dbContext.UserNotifications
                .AsNoTracking()
                .CountAsync(
                    notification =>
                        notification.UserId == userId &&
                        notification.ReadAtUtc == null,
                    cancellationToken);

        return View(unreadCount);
    }
}
