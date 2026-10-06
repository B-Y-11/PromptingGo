using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Security;
using PromptHub.Web.Services.Notifications;

namespace PromptHub.Web.Controllers;

[Route("prompts")]
public sealed class PromptCommentsController : Controller
{

    private readonly ApplicationDbContext _dbContext;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IUserNotificationService _notificationService;

    public PromptCommentsController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IUserNotificationService notificationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _notificationService = notificationService;
    }



    [HttpPost("{slug}/comments")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        string slug,
        string? body,
        CancellationToken cancellationToken)
    {
        var prompt = await _dbContext.Prompts
            .AsNoTracking()
            .Where(prompt =>
    prompt.Slug == slug &&
    prompt.Status ==
        PromptStatus.Published &&
    prompt.Category.IsActive)
            .Select(prompt => new
            {
                prompt.Id,
                prompt.Slug,
                prompt.Title,
                prompt.AuthorId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var normalizedBody =
            body?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            TempData["ErrorMessage"] =
                "Комментарий не может быть пустым.";

            return RedirectToPromptComments(prompt.Slug);
        }

        if (normalizedBody.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Комментарий не должен быть длиннее 1000 символов.";

            return RedirectToPromptComments(prompt.Slug);
        }

        _dbContext.PromptComments.Add(
            new PromptComment
            {
                PromptId = prompt.Id,
                AuthorId = userId,
                Body = normalizedBody,
                Status = PromptCommentStatus.Visible,
                CreatedAtUtc = DateTime.UtcNow
            });

        if (!string.Equals(
                prompt.AuthorId,
                userId,
                StringComparison.Ordinal))
        {
            var promptUrl = Url.Action(
                nameof(PromptsController.Details),
                "Prompts",
                new
                {
                    slug = prompt.Slug
                });

            var notificationUrl =
                promptUrl is null
                    ? null
                    : $"{promptUrl}#comments";

            await _notificationService.AddAsync(
                prompt.AuthorId,
                UserNotificationType.PromptCommentAdded,
                "Новый комментарий",
                $"К вашему промпту «{prompt.Title}» оставили комментарий.",
                notificationUrl,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Комментарий опубликован.";

        return RedirectToPromptComments(prompt.Slug);
    }



    [HttpPost("{slug}/comments/{commentId:int}/delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(
        string slug,
        int commentId,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var comment = await _dbContext.PromptComments
            .Include(comment => comment.Prompt)
            .SingleOrDefaultAsync(
                comment =>
                    comment.Id == commentId &&
                    comment.Prompt.Slug == slug,
                cancellationToken);

        if (comment is null)
        {
            return NotFound();
        }

        if (!string.Equals(
                comment.AuthorId,
                userId,
                StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (comment.Status != PromptCommentStatus.Visible)
        {
            TempData["ErrorMessage"] =
                "Этот комментарий уже недоступен для удаления.";

            return RedirectToPromptComments(slug);
        }

        comment.Status =
            PromptCommentStatus.DeletedByAuthor;

        comment.DeletedAtUtc =
            DateTime.UtcNow;

        comment.DeletedById =
            userId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Комментарий удалён.";

        return RedirectToPromptComments(slug);
    }



    [HttpPost("{slug}/comments/{commentId:int}/hide")]
    [Authorize(Roles = ApplicationRoles.Moderator + "," + ApplicationRoles.Administrator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideComment(
        string slug,
        int commentId,
        string? moderationReason,
        CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Challenge();
        }

        var comment = await _dbContext.PromptComments
            .Include(comment => comment.Prompt)
            .SingleOrDefaultAsync(
                comment =>
                    comment.Id == commentId &&
                    comment.Prompt.Slug == slug,
                cancellationToken);

        if (comment is null)
        {
            return NotFound();
        }

        if (comment.Status != PromptCommentStatus.Visible)
        {
            TempData["ErrorMessage"] =
                "Этот комментарий уже нельзя скрыть.";

            return RedirectToPromptComments(slug);
        }

        var reason =
            moderationReason?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] =
                "Укажите причину скрытия комментария.";

            return RedirectToPromptComments(slug);
        }

        if (reason.Length > 500)
        {
            TempData["ErrorMessage"] =
                "Причина скрытия не должна быть длиннее 500 символов.";

            return RedirectToPromptComments(slug);
        }

        comment.Status =
            PromptCommentStatus.HiddenByModerator;

        comment.HiddenAtUtc =
            DateTime.UtcNow;

        comment.HiddenById =
            moderatorId;

        comment.ModerationReason =
            reason;

        var promptUrl = Url.Action(
            nameof(PromptsController.Details),
            "Prompts",
            new
            {
                slug = comment.Prompt.Slug
            });

        var notificationUrl =
            promptUrl is null
                ? null
                : $"{promptUrl}#comments";

        await _notificationService.AddAsync(
            comment.AuthorId,
            UserNotificationType.PromptCommentHidden,
            "Комментарий скрыт",
            $"Ваш комментарий к промпту «{comment.Prompt.Title}» скрыт модератором. Причина: {reason}",
            notificationUrl,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Комментарий скрыт.";

        return RedirectToPromptComments(slug);
    }



    [HttpPost("{slug}/comments/{commentId:int}/report")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportComment(
        string slug,
        int commentId,
        PromptCommentReportReason reason,
        string? details,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (CanModerateComments())
        {
            TempData["ErrorMessage"] =
                "Модератор может скрыть комментарий напрямую без отправки жалобы.";

            return RedirectToPromptComments(slug);
        }

        if (!Enum.IsDefined(reason))
        {
            TempData["ErrorMessage"] =
                "Выберите корректную причину жалобы.";

            return RedirectToPromptComments(slug);
        }

        var comment = await _dbContext.PromptComments
            .Include(comment => comment.Prompt)
            .SingleOrDefaultAsync(
    comment =>
        comment.Id == commentId &&
        comment.Prompt.Slug == slug &&
        comment.Prompt.Status ==
            PromptStatus.Published &&
        comment.Prompt.Category.IsActive,
    cancellationToken);

        if (comment is null)
        {
            return NotFound();
        }

        if (comment.Status != PromptCommentStatus.Visible)
        {
            TempData["ErrorMessage"] =
                "На этот комментарий уже нельзя пожаловаться.";

            return RedirectToPromptComments(slug);
        }

        if (string.Equals(
                comment.AuthorId,
                userId,
                StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] =
                "Нельзя пожаловаться на свой комментарий.";

            return RedirectToPromptComments(slug);
        }

        var hasOpenReport = await _dbContext.PromptCommentReports
            .AnyAsync(
                report =>
                    report.CommentId == comment.Id &&
                    report.ReporterId == userId &&
                    report.Status == PromptCommentReportStatus.Open,
                cancellationToken);

        if (hasOpenReport)
        {
            TempData["ErrorMessage"] =
                "Вы уже отправили жалобу на этот комментарий.";

            return RedirectToPromptComments(slug);
        }

        var normalizedDetails =
            details?.Trim();

        if (normalizedDetails?.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Описание жалобы не должно быть длиннее 1000 символов.";

            return RedirectToPromptComments(slug);
        }

        _dbContext.PromptCommentReports.Add(
            new PromptCommentReport
            {
                CommentId = comment.Id,
                ReporterId = userId,
                Reason = reason,
                Details = string.IsNullOrWhiteSpace(normalizedDetails)
                    ? null
                    : normalizedDetails,
                Status = PromptCommentReportStatus.Open,
                CreatedAtUtc = DateTime.UtcNow
            });

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Жалоба отправлена на проверку.";

        return RedirectToPromptComments(slug);
    }



    private IActionResult RedirectToPromptComments(
        string slug)
    {
        var url = Url.Action(
            nameof(PromptsController.Details),
            "Prompts",
            new
            {
                slug
            });

        return Redirect(
            $"{url ?? $"/prompts/{slug}"}#comments");
    }

    private bool CanModerateComments()
    {
        return User.IsInRole(ApplicationRoles.Moderator) ||
               User.IsInRole(ApplicationRoles.Administrator);
    }
}
