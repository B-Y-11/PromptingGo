using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Moderation;
using PromptHub.Web.Security;
using PromptHub.Web.Services.Notifications;

namespace PromptHub.Web.Controllers;

[Authorize(
    Roles =
        ApplicationRoles.Moderator +
        "," +
        ApplicationRoles.Administrator)]
[Route("moderation/comment-reports")]
public sealed class CommentReportsModerationController
    : Controller
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IUserNotificationService
        _notificationService;

    public CommentReportsModerationController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IUserNotificationService notificationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? status,
        CancellationToken cancellationToken)
    {
        var selectedStatus =
            NormalizeCommentReportStatusFilter(status);

        var openCount = await _dbContext.PromptCommentReports
            .AsNoTracking()
            .CountAsync(
                report =>
                    report.Status == PromptCommentReportStatus.Open,
                cancellationToken);

        var resolvedCount = await _dbContext.PromptCommentReports
            .AsNoTracking()
            .CountAsync(
                report =>
                    report.Status == PromptCommentReportStatus.Resolved,
                cancellationToken);

        var dismissedCount = await _dbContext.PromptCommentReports
            .AsNoTracking()
            .CountAsync(
                report =>
                    report.Status == PromptCommentReportStatus.Dismissed,
                cancellationToken);

        var reportsQuery = _dbContext.PromptCommentReports
            .AsNoTracking()
            .Include(report => report.Reporter)
            .Include(report => report.ResolvedBy)
            .Include(report => report.Comment)
                .ThenInclude(comment => comment.Author)
            .Include(report => report.Comment)
                .ThenInclude(comment => comment.Prompt)
            .AsQueryable();

        reportsQuery = selectedStatus switch
        {
            "resolved" =>
                reportsQuery.Where(
                    report =>
                        report.Status == PromptCommentReportStatus.Resolved),

            "dismissed" =>
                reportsQuery.Where(
                    report =>
                        report.Status == PromptCommentReportStatus.Dismissed),

            "all" =>
                reportsQuery,

            _ =>
                reportsQuery.Where(
                    report =>
                        report.Status == PromptCommentReportStatus.Open)
        };

        var reports = await reportsQuery
            .OrderByDescending(report => report.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var model = new CommentReportsViewModel
        {
            SelectedStatus =
                selectedStatus,

            OpenCount =
                openCount,

            ResolvedCount =
                resolvedCount,

            DismissedCount =
                dismissedCount,

            Reports = reports
                .Select(report =>
                    new CommentReportItemViewModel
                    {
                        ReportId =
                            report.Id,

                        CommentId =
                            report.CommentId,

                        PromptTitle =
                            report.Comment.Prompt.Title,

                        PromptSlug =
                            report.Comment.Prompt.Slug,

                        CommentAuthorId =
                            report.Comment.AuthorId,

                        CommentAuthorName =
                            GetAuthorName(report.Comment.Author),

                        ReporterId =
                            report.ReporterId,

                        ReporterName =
                            GetAuthorName(report.Reporter),

                        CommentBody =
                            report.Comment.Body,

                        ReasonName =
                            GetCommentReportReasonName(report.Reason),

                        StatusName =
                            GetCommentReportStatusName(report.Status),

                        Details =
                            report.Details,

                        CreatedAtUtc =
                            report.CreatedAtUtc,

                        ResolvedAtUtc =
                            report.ResolvedAtUtc,

                        ResolvedByName =
                            report.ResolvedBy is null
                                ? null
                                : GetAuthorName(report.ResolvedBy),

                        ResolutionComment =
                            report.ResolutionComment,

                        IsOpen =
                            report.Status == PromptCommentReportStatus.Open,

                        IsCommentAlreadyHidden =
                            report.Comment.Status == PromptCommentStatus.HiddenByModerator,

                        CommentModerationReason =
                            report.Comment.ModerationReason
                    })
                .ToArray()
        };

        return View(
            "~/Views/Moderation/CommentReports.cshtml",
            model);
    }

    [HttpPost("{reportId:int}/hide-comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideReportedComment(
        int reportId,
        string? resolutionComment,
        CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Challenge();
        }

        var report = await _dbContext.PromptCommentReports
            .Include(report => report.Comment)
                .ThenInclude(comment => comment.Prompt)
            .SingleOrDefaultAsync(
                report =>
                    report.Id == reportId,
                cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        if (report.Status != PromptCommentReportStatus.Open)
        {
            TempData["ErrorMessage"] =
                "Эта жалоба уже обработана.";

            return RedirectToAction(
                nameof(Index));
        }

        var moderatorNote =
            resolutionComment?.Trim();

        var moderationReason =
            string.IsNullOrWhiteSpace(moderatorNote)
                ? $"Жалоба на комментарий: {GetCommentReportReasonName(report.Reason)}"
                : moderatorNote;

        if (moderationReason.Length > 500)
        {
            TempData["ErrorMessage"] =
                "Комментарий модератора не должен быть длиннее 500 символов.";

            return RedirectToAction(
                nameof(Index));
        }

        if (report.Comment.Status == PromptCommentStatus.Visible)
        {
            report.Comment.Status =
                PromptCommentStatus.HiddenByModerator;

            report.Comment.HiddenAtUtc =
                DateTime.UtcNow;

            report.Comment.HiddenById =
                moderatorId;

            report.Comment.ModerationReason =
                moderationReason;

            var promptUrl = Url.Action(
                "Details",
                "Prompts",
                new
                {
                    slug = report.Comment.Prompt.Slug
                });

            var notificationUrl =
                promptUrl is null
                    ? null
                    : $"{promptUrl}#comments";

            await _notificationService.AddAsync(
                report.Comment.AuthorId,
                UserNotificationType.PromptCommentHidden,
                "Комментарий скрыт",
                $"Ваш комментарий к промпту «{report.Comment.Prompt.Title}» скрыт модератором. Причина: {moderationReason}",
                notificationUrl,
                cancellationToken);
        }

        var openReportsForComment = await _dbContext.PromptCommentReports
            .Where(item =>
                item.CommentId == report.CommentId &&
                item.Status == PromptCommentReportStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var openReport in openReportsForComment)
        {
            openReport.Status =
                PromptCommentReportStatus.Resolved;

            openReport.ResolvedAtUtc =
                DateTime.UtcNow;

            openReport.ResolvedById =
                moderatorId;

            openReport.ResolutionComment =
                moderationReason;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Комментарий скрыт, открытые жалобы по нему закрыты.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpPost("{reportId:int}/dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissCommentReport(
        int reportId,
        string? resolutionComment,
        CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Challenge();
        }

        var report = await _dbContext.PromptCommentReports
            .SingleOrDefaultAsync(
                report =>
                    report.Id == reportId,
                cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        if (report.Status != PromptCommentReportStatus.Open)
        {
            TempData["ErrorMessage"] =
                "Эта жалоба уже обработана.";

            return RedirectToAction(
                nameof(Index));
        }

        var comment =
            resolutionComment?.Trim();

        if (comment?.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Комментарий к решению не должен быть длиннее 1000 символов.";

            return RedirectToAction(
                nameof(Index));
        }

        report.Status =
            PromptCommentReportStatus.Dismissed;

        report.ResolvedAtUtc =
            DateTime.UtcNow;

        report.ResolvedById =
            moderatorId;

        report.ResolutionComment =
            string.IsNullOrWhiteSpace(comment)
                ? "Жалоба отклонена."
                : comment;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Жалоба отклонена.";

        return RedirectToAction(
            nameof(Index));
    }

    private static string GetAuthorName(
        ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
        }

        return "Пользователь";
    }

    private static string GetCommentReportReasonName(
        PromptCommentReportReason reason)
    {
        return reason switch
        {
            PromptCommentReportReason.Spam =>
                "Спам или реклама",

            PromptCommentReportReason.InsultOrHarassment =>
                "Оскорбление или травля",

            PromptCommentReportReason.HateOrExtremism =>
                "Ненависть или экстремизм",

            PromptCommentReportReason.PersonalData =>
                "Личные данные",

            PromptCommentReportReason.IllegalContent =>
                "Запрещённый контент",

            PromptCommentReportReason.OffTopic =>
                "Не по теме",

            PromptCommentReportReason.Other =>
                "Другое",

            _ =>
                "Другое"
        };
    }

    private static string NormalizeCommentReportStatusFilter(
        string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "open";
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "resolved" =>
                "resolved",

            "dismissed" =>
                "dismissed",

            "all" =>
                "all",

            _ =>
                "open"
        };
    }

    private static string GetCommentReportStatusName(
        PromptCommentReportStatus status)
    {
        return status switch
        {
            PromptCommentReportStatus.Open =>
                "Открыта",

            PromptCommentReportStatus.Resolved =>
                "Решена",

            PromptCommentReportStatus.Dismissed =>
                "Отклонена",

            _ =>
                "Неизвестно"
        };
    }
}
