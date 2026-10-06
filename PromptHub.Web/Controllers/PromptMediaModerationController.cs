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
using PromptHub.Web.Services.PromptMedia;

namespace PromptHub.Web.Controllers;

[Authorize(
    Roles =
        ApplicationRoles.Moderator +
        "," +
        ApplicationRoles.Administrator)]
[Route("moderation/media")]
public sealed class PromptMediaModerationController
    : Controller
{
    private const int PageSize =
        18;

    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IUserNotificationService
        _notificationService;

    private readonly IPromptMediaModerationStateService
    _moderationStateService;

    public PromptMediaModerationController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IUserNotificationService notificationService,
        IPromptMediaModerationStateService moderationStateService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _notificationService = notificationService;
        _moderationStateService = moderationStateService;
    }


    [HttpGet(
    "",
    Name = "PromptMediaModerationQueue")]
    public async Task<IActionResult> Index(
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage =
            Math.Max(1, page);

        var query =
            _dbContext.PromptMediaItems
                .AsNoTracking()
                .Include(media =>
                    media.Prompt)
                .ThenInclude(prompt =>
                    prompt.Author)
                .Where(media =>
    media.Prompt.Status ==
        PromptStatus.Published &&
    (
        (
            media.Status ==
                PromptMediaStatus.PendingModeration &&
            (
                media.PromptVersionId ==
                    null ||
                media.PromptVersion!.Status ==
                    PromptVersionStatus.Published
            )
        )
        ||
        (
            media.Status ==
                PromptMediaStatus.Published &&
            media.MetadataSubmittedAtUtc !=
                null
        )
    ));

        var pendingCount =
            await query.CountAsync(
                cancellationToken);

        var totalPages =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    pendingCount /
                    (double)PageSize));

        normalizedPage =
            Math.Min(
                normalizedPage,
                totalPages);

        var mediaItems =
            await query
                .OrderBy(media =>
                    media.MetadataSubmittedAtUtc ??
                    media.SubmittedAtUtc ??
                    media.CreatedAtUtc)
                .ThenBy(media =>
                    media.Id)
                .Skip(
                    (normalizedPage - 1) *
                    PageSize)
                .Take(PageSize)
                .ToListAsync(
                    cancellationToken);

        var model =
            new PromptMediaModerationQueueViewModel
            {
                PendingCount =
                    pendingCount,

                Page =
                    normalizedPage,

                TotalPages =
                    totalPages,

                MediaItems =
                    mediaItems
                        .Select(media =>
                        {
                            var isMetadataReview =
                                media.Status ==
                                    PromptMediaStatus.Published &&
                                media.MetadataSubmittedAtUtc
                                    .HasValue;

                            return new PromptMediaModerationItemViewModel
                            {
                                Id =
                                    media.Id,

                                PromptId =
                                    media.PromptId,

                                PromptTitle =
                                    media.Prompt.Title,

                                PromptSlug =
                                    media.Prompt.Slug,

                                AuthorId =
                                    media.Prompt.AuthorId,

                                AuthorName =
                                    GetAuthorName(
                                        media.Prompt.Author),

                                IsNewMediaReview =
                                    media.Status ==
                                        PromptMediaStatus.PendingModeration,

                                IsMetadataReview =
                                    isMetadataReview,

                                OriginalFileName =
                                    media.OriginalFileName
                                    ?? string.Empty,

                                ContentType =
                                    media.ContentType
                                    ?? string.Empty,

                                FileSizeBytes =
                                    media.FileSizeBytes
                                    ?? 0,

                                IsCover =
                                    media.IsCover,

                                CurrentCaption =
                                    media.Caption,

                                CurrentAltText =
                                    media.AltText,

                                ProposedCaption =
                                    isMetadataReview
                                        ? media.PendingCaption
                                          ?? string.Empty
                                        : media.Caption,

                                ProposedAltText =
                                    isMetadataReview
                                        ? media.PendingAltText
                                          ?? string.Empty
                                        : media.AltText,

                                CreatedAtUtc =
                                    media.CreatedAtUtc,

                                ReviewSubmittedAtUtc =
                                    media.MetadataSubmittedAtUtc ??
                                    media.SubmittedAtUtc ??
                                    media.CreatedAtUtc
                            };
                        })
                .ToArray()
            };

        return View(model);
    }


    [HttpPost("{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id,
        int returnPage = 1,
        CancellationToken cancellationToken = default)
    {
        var moderatorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
            moderatorId))
        {
            return Challenge();
        }

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var media =
                await _dbContext.PromptMediaItems
                    .Include(item =>
                        item.Prompt)
                    .SingleOrDefaultAsync(
                        item =>
    item.Id ==
        id &&
    item.Prompt.Status ==
        PromptStatus.Published &&
    (
        (
            item.Status ==
                PromptMediaStatus.PendingModeration &&
            (
                item.PromptVersionId ==
                    null ||
                item.PromptVersion!.Status ==
                    PromptVersionStatus.Published
            )
        )
        ||
        (
            item.Status ==
                PromptMediaStatus.Published &&
            item.MetadataSubmittedAtUtc !=
                null
        )
    ),
                        cancellationToken);

            if (media is null)
            {
                TempData["ErrorMessage"] =
                    "Запрос не найден или уже обработан.";

                return RedirectToQueue(
                    returnPage);
            }

            var now =
                DateTime.UtcNow;

            var isMetadataReview =
                media.Status ==
                    PromptMediaStatus.Published &&
                media.MetadataSubmittedAtUtc
                    .HasValue;

            if (isMetadataReview)
            {

                _moderationStateService
                    .ApproveMetadata(
                        media,
                        moderatorId,
                        now);

                await _notificationService.AddAsync(
                    media.Prompt.AuthorId,
                    UserNotificationType.PromptMediaApproved,
                    "Описание изображения обновлено",
                    $"Изменения изображения для промпта «{media.Prompt.Title}» " +
                    "прошли модерацию.",
                    $"/prompts/{media.PromptId}/media",
                    cancellationToken);

                TempData["SuccessMessage"] =
                    "Изменения подписи и alt-текста одобрены.";
            }
            else
            {

                _moderationStateService
                    .ApproveNewMedia(
                        media,
                        moderatorId,
                        now);

                if (media.IsCover)
                {
                    var otherCoverItems =
                        await _dbContext.PromptMediaItems
                            .Where(item =>
                                item.PromptId ==
                                    media.PromptId &&
                                item.Id !=
                                    media.Id &&
                                item.IsCover &&
                                item.Status !=
                                    PromptMediaStatus.Archived)
                            .ToListAsync(
                                cancellationToken);

                    foreach (var otherCover in
                             otherCoverItems)
                    {
                        otherCover.IsCover =
                            false;

                        otherCover.UpdatedAtUtc =
                            now;
                    }
                }
                else
                {
                    var hasPublishedCover =
                        await _dbContext.PromptMediaItems
                            .AnyAsync(
                                item =>
                                    item.PromptId ==
                                        media.PromptId &&
                                    item.Id !=
                                        media.Id &&
                                    item.Status ==
                                        PromptMediaStatus.Published &&
                                    item.IsCover,
                                cancellationToken);

                    if (!hasPublishedCover)
                    {
                        media.IsCover =
                            true;
                    }
                }

                await _notificationService.AddAsync(
                    media.Prompt.AuthorId,
                    UserNotificationType.PromptMediaApproved,
                    "Изображение опубликовано",
                    $"Изображение для промпта «{media.Prompt.Title}» " +
                    "прошло модерацию и опубликовано.",
                    $"/prompts/{media.PromptId}/media",
                    cancellationToken);

                TempData["SuccessMessage"] =
                    "Изображение одобрено и опубликовано.";
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return RedirectToQueue(
                returnPage);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _dbContext.ChangeTracker.Clear();

            TempData["ErrorMessage"] =
                "Запрос уже обработан другим модератором. " +
                "Очередь была обновлена.";

            return RedirectToQueue(
                returnPage);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }


    [HttpPost("{id:int}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        int id,
        string moderationComment,
        int returnPage = 1,
        CancellationToken cancellationToken = default)
    {
        var moderatorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
            moderatorId))
        {
            return Challenge();
        }

        var normalizedComment =
            moderationComment?.Trim()
            ?? string.Empty;

        if (normalizedComment.Length < 5)
        {
            TempData["ErrorMessage"] =
                "Укажите понятную причину отклонения.";

            return RedirectToQueue(
                returnPage);
        }

        if (normalizedComment.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Причина отклонения не должна превышать 1000 символов.";

            return RedirectToQueue(
                returnPage);
        }

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var media =
                await _dbContext.PromptMediaItems
                    .Include(item =>
                        item.Prompt)
                    .SingleOrDefaultAsync(
                        item =>
    item.Id ==
        id &&
    item.Prompt.Status ==
        PromptStatus.Published &&
    (
        (
            item.Status ==
                PromptMediaStatus.PendingModeration &&
            (
                item.PromptVersionId ==
                    null ||
                item.PromptVersion!.Status ==
                    PromptVersionStatus.Published
            )
        )
        ||
        (
            item.Status ==
                PromptMediaStatus.Published &&
            item.MetadataSubmittedAtUtc !=
                null
        )
    ),
                        cancellationToken);

            if (media is null)
            {
                TempData["ErrorMessage"] =
                    "Запрос не найден или уже обработан.";

                return RedirectToQueue(
                    returnPage);
            }

            var now =
                DateTime.UtcNow;

            var isMetadataReview =
                media.Status ==
                    PromptMediaStatus.Published &&
                media.MetadataSubmittedAtUtc
                    .HasValue;

            if (isMetadataReview)
            {
                // -----------------------------------------------------------------
                // EN: Reject metadata changes and preserve public values.
                // RU: Отклоняем изменения метаданных и сохраняем публичные значения.
                // -----------------------------------------------------------------

                _moderationStateService
                    .RejectMetadata(
                        media,
                        moderatorId,
                        normalizedComment,
                        now);

                await _notificationService.AddAsync(
                    media.Prompt.AuthorId,
                    UserNotificationType.PromptMediaRejected,
                    "Изменения изображения отклонены",
                    $"Изменения изображения для промпта «{media.Prompt.Title}» " +
                    $"не прошли модерацию. Причина: {normalizedComment}",
                    $"/prompts/{media.PromptId}/media",
                    cancellationToken);

                TempData["SuccessMessage"] =
                    "Изменения подписи и alt-текста отклонены.";
            }
            else
            {
                var wasCover =
                    media.IsCover;


                _moderationStateService
                    .RejectNewMedia(
                        media,
                        moderatorId,
                        normalizedComment,
                        now);

                if (wasCover)
                {
                    var replacementCover =
                        await _dbContext.PromptMediaItems
                            .Where(item =>
                                item.PromptId ==
                                    media.PromptId &&
                                item.Id !=
                                    media.Id &&
                                item.Status ==
                                    PromptMediaStatus.Published &&
                                item.StoragePurgedAtUtc ==
                                    null)
                            .OrderBy(item =>
                                item.SortOrder)
                            .ThenBy(item =>
                                item.CreatedAtUtc)
                            .ThenBy(item =>
                                item.Id)
                            .FirstOrDefaultAsync(
                                cancellationToken);

                    if (replacementCover is not null)
                    {
                        replacementCover.IsCover =
                            true;

                        replacementCover.UpdatedAtUtc =
                            now;
                    }
                }

                await _notificationService.AddAsync(
                    media.Prompt.AuthorId,
                    UserNotificationType.PromptMediaRejected,
                    "Изображение отклонено",
                    $"Изображение для промпта «{media.Prompt.Title}» " +
                    $"не прошло модерацию. Причина: {normalizedComment}",
                    $"/prompts/{media.PromptId}/media",
                    cancellationToken);

                TempData["SuccessMessage"] =
                    "Изображение отклонено.";
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return RedirectToQueue(
                returnPage);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _dbContext.ChangeTracker.Clear();

            TempData["ErrorMessage"] =
                "Запрос уже обработан другим модератором. " +
                "Очередь была обновлена.";

            return RedirectToQueue(
                returnPage);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }


    private static string GetAuthorName(
        ApplicationUser author)
    {
        if (!string.IsNullOrWhiteSpace(
            author.DisplayName))
        {
            return author.DisplayName;
        }

        return author.UserName
            ?? author.Email
            ?? "Пользователь";
    }



    private IActionResult RedirectToQueue(
        int returnPage)
    {
        return RedirectToRoute(
            "PromptMediaModerationQueue",
            new
            {
                page =
                    Math.Max(
                        1,
                        returnPage)
            });
    }
}

