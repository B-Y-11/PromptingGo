using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Moderation;
using PromptHub.Web.Models.ViewModels.Prompts;
using PromptHub.Web.Security;
using PromptHub.Web.Services.Notifications;
using PromptHub.Web.Services.Moderation;

namespace PromptHub.Web.Controllers;

[Authorize(
    Roles =
        ApplicationRoles.Moderator +
        "," +
        ApplicationRoles.Administrator)]
[Route("moderation")]
public sealed class ModerationController
    : Controller
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IUserNotificationService _notificationService;

    private readonly IPromptDuplicateDetectionService
    _duplicateDetectionService;

    public ModerationController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IUserNotificationService notificationService,
        IPromptDuplicateDetectionService duplicateDetectionService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _notificationService = notificationService;
        _duplicateDetectionService = duplicateDetectionService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
    CancellationToken cancellationToken)
    {
        var pendingVersions =
            await _dbContext.PromptVersions
                .AsNoTracking()

                .Include(version =>
                    version.Prompt)
                    .ThenInclude(prompt =>
                        prompt.Author)

                .Include(version =>
                    version.Category)

                .Include(version =>
                    version.VersionTags)
                    .ThenInclude(versionTag =>
                        versionTag.Tag)

                .Include(version =>
                    version.Variables)

                .Where(version =>
                    version.Status ==
                        PromptVersionStatus.PendingModeration)

                .OrderBy(version =>
                    version.SubmittedAtUtc ??
                    version.UpdatedAtUtc ??
                    version.CreatedAtUtc)

                .AsSplitQuery()

                .ToListAsync(
                    cancellationToken);

        var model =
            new ModerationQueueViewModel
            {
                Prompts =
                    pendingVersions
                        .Select(version =>
                            new ModerationQueueItemViewModel
                            {
                                Id =
                                    version.PromptId,

                                Title =
                                    version.Title,

                                Summary =
                                    version.Summary,

                                CategoryName =
                                    version.Category.Name,

                                Tags =
                                    CreateModerationTagBadges(
                                        version),

                                AuthorName =
                                    GetAuthorName(
                                        version.Prompt.Author),

                                AuthorEmail =
                                    version.Prompt.Author.Email
                                    ?? string.Empty,

                                HasConstructor =
                                    !string.IsNullOrWhiteSpace(
                                        version.TemplateText) &&
                                    version.Variables.Count > 0,

                                SubmittedAtUtc =
                                    version.SubmittedAtUtc
                            })
                        .ToArray()
            };

        return View(
            model);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(
    CancellationToken cancellationToken)
    {

        var pendingPromptsCount =
            await _dbContext.PromptVersions
                .AsNoTracking()
                .CountAsync(
                    version =>
                        version.Status ==
                            PromptVersionStatus.PendingModeration,
                    cancellationToken);


        var pendingMediaCount =
    await _dbContext.PromptMediaItems
        .AsNoTracking()
        .CountAsync(
            media =>
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
                ),
            cancellationToken);


        var newReportsCount =
            await _dbContext.PromptReports
                .AsNoTracking()
                .CountAsync(
                    report =>
                        report.Status ==
                            PromptReportStatus.New,
                    cancellationToken);

        var archivedPromptsCount =
            await _dbContext.Prompts
                .AsNoTracking()
                .CountAsync(
                    prompt =>
                        prompt.Status ==
                            PromptStatus.Archived,
                    cancellationToken);

        var openCommentReportCount =
            await _dbContext.PromptCommentReports
                .AsNoTracking()
                .CountAsync(
                    report =>
                        report.Status ==
                            PromptCommentReportStatus.Open,
                    cancellationToken);


        var pendingPrompts =
            await _dbContext.PromptVersions
                .AsNoTracking()

                .Include(version =>
                    version.Prompt)
                    .ThenInclude(prompt =>
                        prompt.Author)

                .Include(version =>
                    version.Category)

                .Include(version =>
                    version.VersionTags)
                    .ThenInclude(versionTag =>
                        versionTag.Tag)

                .Where(version =>
                    version.Status ==
                        PromptVersionStatus.PendingModeration)

                .OrderByDescending(version =>
                    version.SubmittedAtUtc)

                .ThenByDescending(version =>
                    version.CreatedAtUtc)

                .Take(5)

                .AsSplitQuery()

                .ToListAsync(
                    cancellationToken);

        var newReports =
            await _dbContext.PromptReports
                .AsNoTracking()
                .Include(report =>
                    report.Prompt)
                .Include(report =>
                    report.Reporter)
                .Where(report =>
                    report.Status ==
                        PromptReportStatus.New)
                .OrderByDescending(report =>
                    report.CreatedAtUtc)
                .Take(5)
                .ToListAsync(
                    cancellationToken);

        var archivedPrompts =
            await _dbContext.Prompts
                .AsNoTracking()
                .Include(prompt =>
                    prompt.Author)
                .Include(prompt =>
                    prompt.ArchivedBy)
                .Where(prompt =>
                    prompt.Status ==
                        PromptStatus.Archived)
                .OrderByDescending(prompt =>
                    prompt.ArchivedAtUtc)
                .ThenByDescending(prompt =>
                    prompt.UpdatedAtUtc)
                .Take(5)
                .ToListAsync(
                    cancellationToken);

        var model =
            new ModerationDashboardViewModel
            {
                PendingPromptsCount =
                    pendingPromptsCount,

                PendingMediaCount =
                    pendingMediaCount,

                NewReportsCount =
                    newReportsCount,

                ArchivedPromptsCount =
                    archivedPromptsCount,

                OpenCommentReportCount =
                    openCommentReportCount,

                PendingPrompts =
                    pendingPrompts
                        .Select(version =>
                            new ModerationDashboardPromptItemViewModel
                            {
                                Id =
                                    version.PromptId,

                                Title =
                                    version.Title,

                                Summary =
                                    version.Summary,

                                CategoryName =
                                    version.Category.Name,

                                Tags =
                                    CreateModerationTagBadges(
                                        version),

                                AuthorName =
                                    GetAuthorName(
                                        version.Prompt.Author),

                                DifficultyName =
                                    GetDifficultyName(
                                        version.Difficulty),

                                SubmittedAtUtc =
                                    version.SubmittedAtUtc
                            })
                        .ToArray(),

                NewReports =
                    newReports
                        .Select(report =>
                            new ModerationDashboardReportItemViewModel
                            {
                                Id =
                                    report.Id,

                                PromptTitle =
                                    report.Prompt.Title,

                                PromptSlug =
                                    report.Prompt.Slug,

                                ReporterName =
                                    GetAuthorName(
                                        report.Reporter),

                                ReasonName =
                                    GetReportReasonName(
                                        report.Reason),

                                Details =
                                    report.Details,

                                CreatedAtUtc =
                                    report.CreatedAtUtc
                            })
                        .ToArray(),

                ArchivedPrompts =
                    archivedPrompts
                        .Select(prompt =>
                            new ModerationDashboardArchivedPromptItemViewModel
                            {
                                Id =
                                    prompt.Id,

                                Title =
                                    prompt.Title,

                                Slug =
                                    prompt.Slug,

                                AuthorName =
                                    GetAuthorName(
                                        prompt.Author),

                                ArchivedByName =
                                    prompt.ArchivedBy is null
                                        ? "Неизвестный модератор"
                                        : GetAuthorName(
                                            prompt.ArchivedBy),

                                ArchiveReason =
                                    prompt.ArchiveReason
                                    ?? string.Empty,

                                ArchivedAtUtc =
                                    prompt.ArchivedAtUtc
                            })
                        .ToArray()
            };

        return View(
            model);
    }


    [HttpGet("{id:int}")]
    public async Task<IActionResult> Review(
    int id,
    bool checkDuplicates = false,
    CancellationToken cancellationToken = default)
    {
        var model =
            await LoadReviewModelAsync(
                id,
                cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: Duplicate detection is executed only when explicitly requested by
        //     the moderator. Opening the review page itself remains inexpensive.
        //
        // RU: Проверка запускается только по явному запросу модератора.
        //     Обычное открытие страницы модерации не выполняет тяжёлый поиск.
        // -------------------------------------------------------------------------

        if (checkDuplicates)
        {
            var matches =
                await _duplicateDetectionService
                    .FindSimilarPublishedAsync(
                        id,
                        cancellationToken);

            model.DuplicateCheckPerformed =
                true;

            model.DuplicateMatches =
                matches
                    .Select(match =>
                        new ModerationDuplicateMatchViewModel
                        {
                            PromptId =
                                match.PromptId,

                            Title =
                                match.Title,

                            Slug =
                                match.Slug,

                            AuthorName =
                                match.AuthorName,

                            PublishedAtUtc =
                                match.PublishedAtUtc,

                            SimilarityPercent =
                                match.SimilarityPercent,

                            ReadyTextSimilarityPercent =
                                match.ReadyTextSimilarityPercent,

                            TemplateTextSimilarityPercent =
                                match.TemplateTextSimilarityPercent
                        })
                    .ToArray();
        }

        return View(
            model);
    }
    [HttpPost("{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
    int id,
    CancellationToken cancellationToken)
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
            var version =
                await _dbContext.PromptVersions

                    .Include(versionEntity =>
                        versionEntity.VersionTags)

                    .Include(versionEntity =>
                        versionEntity.Prompt)
                        .ThenInclude(prompt =>
                            prompt.Versions)

                    .Include(versionEntity =>
                        versionEntity.Prompt)
                        .ThenInclude(prompt =>
                            prompt.PromptTags)

                    .Include(versionEntity =>
                        versionEntity.Prompt)
                        .ThenInclude(prompt =>
                            prompt.MediaItems)

                    .AsSplitQuery()

                    .SingleOrDefaultAsync(
                        versionEntity =>
                            versionEntity.PromptId ==
                                id &&
                            versionEntity.Status ==
                                PromptVersionStatus.PendingModeration,
                        cancellationToken);

            if (version is null)
            {
                TempData["ErrorMessage"] =
                    "Версия промпта не найдена или уже обработана.";

                await transaction.RollbackAsync(
                    CancellationToken.None);

                return RedirectToAction(
                    nameof(Index));
            }

            var prompt =
                version.Prompt;

            // ---------------------------------------------------------------------
            // EN: Detect whether this is the first publication or an update of an
            //     already published prompt.
            //
            // RU: Определяем: это первая публикация или обновление уже
            //     опубликованного промпта.
            // ---------------------------------------------------------------------

            var previousPublishedVersion =
                prompt.Versions
                    .SingleOrDefault(
                        promptVersion =>
                            promptVersion.IsCurrent &&
                            promptVersion.Status ==
                                PromptVersionStatus.Published);

            var isPublishedUpdate =
                previousPublishedVersion is not null;

            // ---------------------------------------------------------------------
            // EN: A prompt can only be approved into an active category.
            // RU: Публиковать можно только в активной категории.
            // ---------------------------------------------------------------------

            var categoryIsActive =
                await _dbContext.Categories
                    .AsNoTracking()
                    .AnyAsync(
                        category =>
                            category.Id ==
                                version.CategoryId &&
                            category.IsActive,
                        cancellationToken);

            if (!categoryIsActive)
            {
                TempData["ErrorMessage"] =
                    "Нельзя опубликовать промпт в скрытой категории. Выберите активную категорию.";

                await transaction.RollbackAsync(
                    CancellationToken.None);

                return RedirectToAction(
                    nameof(Review),
                    new
                    {
                        id
                    });
            }

            var now =
                DateTime.UtcNow;

            // ---------------------------------------------------------------------
            // EN: The approved version becomes the only current published version.
            //
            // RU: Одобренная версия становится единственной текущей
            //     опубликованной версией.
            // ---------------------------------------------------------------------

            foreach (var promptVersion in
                     prompt.Versions)
            {
                promptVersion.IsCurrent =
                    promptVersion.Id ==
                        version.Id;
            }

            version.Status =
                PromptVersionStatus.Published;

            version.IsCurrent =
                true;

            version.PublishedAtUtc =
                now;

            version.ModeratedAtUtc =
                now;

            version.ModeratedById =
                moderatorId;

            version.ModerationComment =
                null;

            version.UpdatedAtUtc =
                now;

            // ---------------------------------------------------------------------
            // EN: Only now copy the moderated version into the public Prompt
            //     snapshot. Until this point v2 was invisible publicly.
            //
            // RU: Только после одобрения переносим данные версии в публичный
            //     Prompt. До этого изменения v2 публично не отображались.
            // ---------------------------------------------------------------------

            prompt.Title =
                version.Title;

            prompt.Summary =
                version.Summary;

            prompt.LanguageCode =
                version.LanguageCode;

            prompt.Difficulty =
                version.Difficulty;

            prompt.CategoryId =
                version.CategoryId;

            prompt.Status =
                PromptStatus.Published;

            prompt.SubmittedAtUtc =
                version.SubmittedAtUtc;

            prompt.ModeratedAtUtc =
                now;

            prompt.ModeratedById =
                moderatorId;

            prompt.ModerationComment =
                null;

            prompt.UpdatedAtUtc =
                now;

            // EN: Preserve the date of the very first publication.
            // RU: Не меняем дату первой публикации при последующих обновлениях.
            prompt.PublishedAtUtc ??=
                now;

            SyncPromptTagsFromVersion(
                prompt,
                version);

            // ---------------------------------------------------------------------
            // EN: Publish only media submitted with this exact version.
            //
            // RU: Публикуем только медиа, отправленные вместе именно с этой
            //     версией. Медиа предыдущей публичной версии не трогаем.
            // ---------------------------------------------------------------------

            var submittedMedia =
                prompt.MediaItems
                    .Where(media =>
                        media.PromptVersionId ==
                            version.Id &&
                        media.Status ==
                            PromptMediaStatus.PendingModeration)
                    .OrderBy(media =>
                        media.SortOrder)
                    .ThenBy(media =>
                        media.CreatedAtUtc)
                    .ToList();

            var requestedCover =
                submittedMedia
                    .FirstOrDefault(
                        media =>
                            media.IsCover);

            foreach (var media in
                     submittedMedia)
            {
                media.Status =
                    PromptMediaStatus.Published;

                media.ModeratedAtUtc =
                    now;

                media.ModeratedById =
                    moderatorId;

                media.ModerationComment =
                    null;

                media.UpdatedAtUtc =
                    now;
            }

            // ---------------------------------------------------------------------
            // EN: The prompt must have no more than one published cover.
            //     A cover selected in the new version has priority.
            //
            // RU: У промпта должна быть максимум одна опубликованная обложка.
            //     Обложка, выбранная в новой версии, имеет приоритет.
            // ---------------------------------------------------------------------

            var publishedMedia =
                prompt.MediaItems
                    .Where(media =>
                        media.Status ==
                            PromptMediaStatus.Published)
                    .OrderBy(media =>
                        media.SortOrder)
                    .ThenBy(media =>
                        media.CreatedAtUtc)
                    .ToList();

            if (publishedMedia.Count > 0)
            {
                var selectedCover =
                    requestedCover
                    ?? publishedMedia
                        .FirstOrDefault(
                            media =>
                                media.IsCover)
                    ?? publishedMedia[0];

                foreach (var media in
                         publishedMedia)
                {
                    media.IsCover =
                        media.Id ==
                            selectedCover.Id;
                }
            }

            await _notificationService.AddAsync(
                prompt.AuthorId,
                UserNotificationType.PromptApproved,

                isPublishedUpdate
                    ? "Изменения промпта опубликованы"
                    : "Промпт опубликован",

                isPublishedUpdate
                    ? $"Изменения промпта «{version.Title}» прошли модерацию и опубликованы."
                    : $"Ваш промпт «{version.Title}» прошёл модерацию и опубликован в каталоге.",

                $"/prompts/{prompt.Slug}",
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                isPublishedUpdate
                    ? "Изменения одобрены и опубликованы."
                    : "Промпт опубликован.";

            return RedirectToAction(
                nameof(Index));
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
    CancellationToken cancellationToken)
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

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        if (normalizedComment.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Причина отклонения не должна превышать 1000 символов.";

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var version =
                await _dbContext.PromptVersions

                    .Include(versionEntity =>
                        versionEntity.Prompt)
                        .ThenInclude(prompt =>
                            prompt.Versions)

                    .Include(versionEntity =>
                        versionEntity.Prompt)
                        .ThenInclude(prompt =>
                            prompt.MediaItems)

                    .AsSplitQuery()

                    .SingleOrDefaultAsync(
                        versionEntity =>
                            versionEntity.PromptId ==
                                id &&
                            versionEntity.Status ==
                                PromptVersionStatus.PendingModeration,
                        cancellationToken);

            if (version is null)
            {
                TempData["ErrorMessage"] =
                    "Версия промпта не найдена или уже обработана.";

                await transaction.RollbackAsync(
                    CancellationToken.None);

                return RedirectToAction(
                    nameof(Index));
            }

            var prompt =
                version.Prompt;

            var publishedVersion =
                prompt.Versions
                    .SingleOrDefault(
                        promptVersion =>
                            promptVersion.IsCurrent &&
                            promptVersion.Status ==
                                PromptVersionStatus.Published);

            var isPublishedUpdate =
                publishedVersion is not null;

            var now =
                DateTime.UtcNow;


            version.Status =
                PromptVersionStatus.Rejected;

            version.ModerationComment =
                normalizedComment;

            version.ModeratedAtUtc =
                now;

            version.ModeratedById =
                moderatorId;

            version.UpdatedAtUtc =
                now;

            // ---------------------------------------------------------------------
            // EN: For the first publication Prompt follows the version lifecycle.
            //
            //     For an update of an already published prompt we leave Prompt
            //     completely published and untouched.
            //
            // RU: При первой публикации Prompt повторяет состояние версии.
            //
            //     Если отклонено обновление опубликованного промпта, сам Prompt
            //     остаётся опубликованным и старая версия продолжает работать.
            // ---------------------------------------------------------------------

            if (!isPublishedUpdate)
            {
                prompt.Status =
                    PromptStatus.Rejected;

                prompt.ModerationComment =
                    normalizedComment;

                prompt.ModeratedAtUtc =
                    now;

                prompt.ModeratedById =
                    moderatorId;

                prompt.UpdatedAtUtc =
                    now;
            }

            // ---------------------------------------------------------------------
            // EN: Return only this version's pending media to Draft.
            // RU: Возвращаем в Draft только медиа этой версии.
            // ---------------------------------------------------------------------

            foreach (var media in
                     prompt.MediaItems
                         .Where(media =>
                             media.PromptVersionId ==
                                 version.Id &&
                             media.Status ==
                                 PromptMediaStatus.PendingModeration))
            {
                media.Status =
                    PromptMediaStatus.Draft;

                media.SubmittedAtUtc =
                    null;

                media.ModeratedAtUtc =
                    null;

                media.ModeratedById =
                    null;

                media.ModerationComment =
                    null;

                media.UpdatedAtUtc =
                    now;
            }

            await _notificationService.AddAsync(
                prompt.AuthorId,
                UserNotificationType.PromptRejected,

                isPublishedUpdate
                    ? "Изменения промпта отклонены"
                    : "Промпт возвращён на исправление",

                isPublishedUpdate
                    ? $"Изменения промпта «{version.Title}» не прошли модерацию. Причина: {normalizedComment}"
                    : $"Ваш промпт «{version.Title}» не прошёл модерацию. Причина: {normalizedComment}",

                "/prompts/my",
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                isPublishedUpdate
                    ? "Изменения отклонены. Текущая публикация осталась без изменений."
                    : "Промпт отклонён и возвращён автору.";

            return RedirectToAction(
                nameof(Index));
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }

    [HttpPost("{id:int}/category")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePromptCategory(
    int id,
    [FromForm] int categoryId,
    CancellationToken cancellationToken)
    {
        // -------------------------------------------------------------------------
        // EN: Editorial moderation changes belong to the version being reviewed.
        //
        // RU: Редакционные правки модератора применяются к проверяемой версии,
        //     а не к публичному Prompt.
        // -------------------------------------------------------------------------

        var version =
            await _dbContext.PromptVersions
                .SingleOrDefaultAsync(
                    versionEntity =>
                        versionEntity.PromptId ==
                            id &&
                        versionEntity.Status ==
                            PromptVersionStatus.PendingModeration,
                    cancellationToken);

        if (version is null)
        {
            TempData["ErrorMessage"] =
                "Версия промпта не найдена или уже не находится на модерации.";

            return RedirectToAction(
                nameof(Index));
        }

        var category =
            await _dbContext.Categories
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    categoryEntity =>
                        categoryEntity.Id ==
                            categoryId &&
                        categoryEntity.IsActive,
                    cancellationToken);

        if (category is null)
        {
            TempData["ErrorMessage"] =
                "Выберите активную категорию.";

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        if (version.CategoryId ==
            categoryId)
        {
            TempData["SuccessMessage"] =
                "Категория уже выбрана.";

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        version.CategoryId =
            categoryId;

        version.UpdatedAtUtc =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            $"Категория изменена на «{category.Name}».";

        return RedirectToAction(
            nameof(Review),
            new
            {
                id
            });
    }

    [HttpPost("{id:int}/editorial")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePromptEditorialData(
    int id,
    [FromForm] string title,
    [FromForm] string summary,
    [FromForm] PromptDifficulty difficulty,
    CancellationToken cancellationToken)
    {
        var normalizedTitle =
            title?.Trim()
            ?? string.Empty;

        var normalizedSummary =
            summary?.Trim()
            ?? string.Empty;

        if (normalizedTitle.Length is
            < 5 or > 160)
        {
            TempData["ErrorMessage"] =
                "Название должно содержать от 5 до 160 символов.";

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        if (normalizedSummary.Length is
            < 10 or > 500)
        {
            TempData["ErrorMessage"] =
                "Краткое описание должно содержать от 10 до 500 символов.";

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        if (!Enum.IsDefined(
                typeof(PromptDifficulty),
                difficulty))
        {
            TempData["ErrorMessage"] =
                "Выберите корректный уровень сложности.";

            return RedirectToAction(
                nameof(Review),
                new
                {
                    id
                });
        }

        // -------------------------------------------------------------------------
        // EN: Update only the pending version. The public Prompt snapshot will be
        //     updated later by Approve.
        //
        // RU: Изменяем только версию на модерации. Публичный Prompt будет
        //     синхронизирован с ней только после Approve.
        // -------------------------------------------------------------------------

        var version =
            await _dbContext.PromptVersions
                .SingleOrDefaultAsync(
                    versionEntity =>
                        versionEntity.PromptId ==
                            id &&
                        versionEntity.Status ==
                            PromptVersionStatus.PendingModeration,
                    cancellationToken);

        if (version is null)
        {
            TempData["ErrorMessage"] =
                "Версия промпта не найдена или уже не находится на модерации.";

            return RedirectToAction(
                nameof(Index));
        }

        version.Title =
            normalizedTitle;

        version.Summary =
            normalizedSummary;

        version.Difficulty =
            difficulty;

        version.UpdatedAtUtc =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Редакционные правки сохранены.";

        return RedirectToAction(
            nameof(Review),
            new
            {
                id
            });
    }


    [HttpGet("reports")]
    public async Task<IActionResult> Reports(
    CancellationToken cancellationToken)
    {
        var reports =
            await _dbContext.PromptReports
                .AsNoTracking()
                .Include(report => report.Prompt)
                    .ThenInclude(prompt => prompt.Category)
                .Include(report => report.Reporter)
                .Where(report =>
                    report.Status == PromptReportStatus.New)
                .OrderBy(report =>
                    report.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        var model =
            new PromptReportsViewModel
            {
                Reports =
                    reports
                        .Select(report =>
                            new PromptReportItemViewModel
                            {
                                Id = report.Id,

                                PromptTitle =
                                    report.Prompt.Title,

                                PromptSlug =
                                    report.Prompt.Slug,

                                PromptSummary =
                                    report.Prompt.Summary,

                                CategoryName =
                                    report.Prompt.Category.Name,

                                ReporterName =
                                    GetAuthorName(
                                        report.Reporter),

                                ReasonName =
                                    GetReportReasonName(
                                        report.Reason),

                                Details =
                                    report.Details,

                                CreatedAtUtc =
                                    report.CreatedAtUtc
                            })
                        .ToArray()
            };

        return View(model);
    }

    [HttpPost("reports/{id:int}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(
        int id,
        string? resolutionComment,
        CancellationToken cancellationToken)
    {
        return await UpdateReportStatusAsync(
            id,
            PromptReportStatus.Resolved,
            resolutionComment,
            "Жалоба отмечена как обработанная.",
            cancellationToken);
    }

    [HttpPost("reports/{id:int}/dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReport(
        int id,
        string? resolutionComment,
        CancellationToken cancellationToken)
    {
        return await UpdateReportStatusAsync(
            id,
            PromptReportStatus.Dismissed,
            resolutionComment,
            "Жалоба отклонена.",
            cancellationToken);
    }

    [HttpPost("reports/{id:int}/archive-prompt")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveReportedPrompt(
    int id,
    string? archiveReason,
    CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Challenge();
        }

        var normalizedReason =
            archiveReason?.Trim()
            ?? string.Empty;

        if (normalizedReason.Length < 5)
        {
            TempData["ErrorMessage"] =
                "Укажите причину скрытия промпта. Минимум 5 символов.";

            return RedirectToAction(
                nameof(Reports));
        }

        if (normalizedReason.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Причина скрытия не должна превышать 1000 символов.";

            return RedirectToAction(
                nameof(Reports));
        }

        var report =
            await _dbContext.PromptReports
                .Include(reportEntity =>
                    reportEntity.Prompt)
                .SingleOrDefaultAsync(
                    reportEntity =>
                        reportEntity.Id == id &&
                        reportEntity.Status ==
                            PromptReportStatus.New,
                    cancellationToken);

        if (report is null)
        {
            TempData["ErrorMessage"] =
                "Жалоба не найдена или уже обработана.";

            return RedirectToAction(
                nameof(Reports));
        }

        if (report.Prompt.Status != PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "Этот промпт уже не опубликован.";

            return RedirectToAction(
                nameof(Reports));
        }

        var now =
            DateTime.UtcNow;

        report.Prompt.Status =
            PromptStatus.Archived;

        report.Prompt.ArchivedAtUtc =
            now;

        report.Prompt.ArchivedById =
            moderatorId;

        report.Prompt.ArchiveReason =
            normalizedReason;

        report.Prompt.UpdatedAtUtc =
            now;

        report.Status =
            PromptReportStatus.Resolved;

        report.ResolvedAtUtc =
            now;

        report.ResolvedById =
            moderatorId;

        report.ResolutionComment =
            $"Промпт скрыт модератором. Причина: {normalizedReason}";

        await _notificationService.AddAsync(
            report.Prompt.AuthorId,
            UserNotificationType.PromptArchived,
            "Промпт скрыт модератором",
            $"Ваш промпт «{report.Prompt.Title}» скрыт из публичного каталога. Причина: {normalizedReason}",
            "/prompts/my",
            cancellationToken);


        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Промпт скрыт из публичного каталога, жалоба отмечена обработанной.";

        return RedirectToAction(
            nameof(Reports));
    }

    private async Task<IActionResult> UpdateReportStatusAsync(
        int id,
        PromptReportStatus status,
        string? resolutionComment,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Challenge();
        }

        var report =
            await _dbContext.PromptReports
                .SingleOrDefaultAsync(
                    reportEntity =>
                        reportEntity.Id == id &&
                        reportEntity.Status == PromptReportStatus.New,
                    cancellationToken);

        if (report is null)
        {
            TempData["ErrorMessage"] =
                "Жалоба не найдена или уже обработана.";

            return RedirectToAction(
                nameof(Reports));
        }

        var normalizedComment =
            resolutionComment?.Trim();

        if (normalizedComment?.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Комментарий не должен превышать 1000 символов.";

            return RedirectToAction(
                nameof(Reports));
        }

        report.Status = status;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedById = moderatorId;
        report.ResolutionComment = normalizedComment;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            successMessage;

        return RedirectToAction(
            nameof(Reports));
    }



    [HttpPost("prompts/{id:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchivePrompt(
    int id,
    string? archiveReason,
    CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                moderatorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                        id,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        if (prompt.Status !=
            PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "Скрыть можно только опубликованный промпт.";

            return RedirectToAction(
                nameof(Dashboard));
        }

        var normalizedReason =
            archiveReason?.Trim()
            ?? string.Empty;

        if (normalizedReason.Length < 5)
        {
            TempData["ErrorMessage"] =
                "Укажите причину скрытия промпта. Минимум 5 символов.";

            return RedirectToAction(
                "Details",
                "Prompts",
                new
                {
                    slug =
                        prompt.Slug
                });
        }

        if (normalizedReason.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Причина скрытия не должна превышать 1000 символов.";

            return RedirectToAction(
                "Details",
                "Prompts",
                new
                {
                    slug =
                        prompt.Slug
                });
        }

        var nowUtc =
            DateTime.UtcNow;

        prompt.Status =
            PromptStatus.Archived;

        prompt.ArchivedAtUtc =
            nowUtc;

        prompt.ArchivedById =
            moderatorId;

        prompt.ArchiveReason =
            normalizedReason;

        prompt.UpdatedAtUtc =
            nowUtc;

        // -------------------------------------------------------------------------
        // EN: Resolve remaining open reports because the prompt is already hidden.
        // RU: Закрываем открытые жалобы, так как промпт уже скрыт.
        // -------------------------------------------------------------------------

        var openReports =
            await _dbContext.PromptReports
                .Where(report =>
                    report.PromptId ==
                        prompt.Id &&
                    report.Status ==
                        PromptReportStatus.New)
                .ToListAsync(
                    cancellationToken);

        foreach (var report in openReports)
        {
            report.Status =
                PromptReportStatus.Resolved;

            report.ResolvedAtUtc =
                nowUtc;

            report.ResolvedById =
                moderatorId;

            report.ResolutionComment =
                $"Промпт скрыт модератором. Причина: {normalizedReason}";
        }

        await _notificationService.AddAsync(
            prompt.AuthorId,
            UserNotificationType.PromptArchived,
            "Промпт скрыт модератором",
            $"Ваш промпт «{prompt.Title}» скрыт из публичного каталога. Причина: {normalizedReason}",
            "/prompts/my",
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Промпт скрыт из публичного каталога.";

        return RedirectToAction(
            nameof(ArchivedPrompts));
    }



    [HttpGet("archived")]
    public async Task<IActionResult> ArchivedPrompts(
    CancellationToken cancellationToken)
    {
        var prompts =
            await _dbContext.Prompts
                .AsNoTracking()
                .Include(prompt =>
                    prompt.Author)
                .Include(prompt =>
                    prompt.Category)
                .Include(prompt =>
                    prompt.ArchivedBy)
                .Where(prompt =>
                    prompt.Status == PromptStatus.Archived)
                .OrderByDescending(prompt =>
                    prompt.ArchivedAtUtc)
                .ToListAsync(
                    cancellationToken);

        var model =
            new ArchivedPromptsViewModel
            {
                Prompts =
                    prompts
                        .Select(prompt =>
                            new ArchivedPromptItemViewModel
                            {
                                Id = prompt.Id,
                                Title = prompt.Title,
                                Slug = prompt.Slug,
                                Summary = prompt.Summary,

                                CategoryName =
                                    prompt.Category.Name,

                                AuthorName =
                                    GetAuthorName(
                                        prompt.Author),

                                ArchivedByName =
                                    prompt.ArchivedBy is null
                                        ? "Неизвестный модератор"
                                        : GetAuthorName(
                                            prompt.ArchivedBy),

                                ArchiveReason =
                                    prompt.ArchiveReason
                                    ?? string.Empty,

                                ArchivedAtUtc =
                                    prompt.ArchivedAtUtc,

                                PublishedAtUtc =
                                    prompt.PublishedAtUtc,

                                ViewCount =
                                    prompt.ViewCount,

                                CopyCount =
                                    prompt.CopyCount
                            })
                        .ToArray()
            };

        return View(model);
    }

    [HttpPost("archived/{id:int}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreArchivedPrompt(
        int id,
        string? restoreComment,
        CancellationToken cancellationToken)
    {
        var moderatorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(moderatorId))
        {
            return Challenge();
        }

        var normalizedComment =
            restoreComment?.Trim()
            ?? string.Empty;

        if (normalizedComment.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Комментарий не должен превышать 1000 символов.";

            return RedirectToAction(
                nameof(ArchivedPrompts));
        }

        var prompt =
            await _dbContext.Prompts
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id == id &&
                        promptEntity.Status == PromptStatus.Archived,
                    cancellationToken);

        if (prompt is null)
        {
            TempData["ErrorMessage"] =
                "Скрытый промпт не найден или уже восстановлен.";

            return RedirectToAction(
                nameof(ArchivedPrompts));
        }

        var now =
            DateTime.UtcNow;

        prompt.Status =
            PromptStatus.Published;

        prompt.ArchivedAtUtc =
            null;

        prompt.ArchivedById =
            null;

        prompt.ArchiveReason =
            null;

        prompt.ModeratedAtUtc =
            now;

        prompt.ModeratedById =
            moderatorId;

        prompt.ModerationComment =
            string.IsNullOrWhiteSpace(normalizedComment)
                ? null
                : normalizedComment;

        prompt.UpdatedAtUtc =
            now;

        await _notificationService.AddAsync(
            prompt.AuthorId,
            UserNotificationType.PromptRestored,
            "Промпт восстановлен",
            $"Ваш промпт «{prompt.Title}» снова доступен в публичном каталоге.",
            $"/prompts/{prompt.Slug}", cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Промпт восстановлен и снова доступен в публичном каталоге.";

        return RedirectToAction(
            nameof(ArchivedPrompts));
    }


    private async Task<ModerationReviewViewModel?>
    LoadReviewModelAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var version =
            await _dbContext.PromptVersions
                .AsNoTracking()
                .AsSplitQuery()

                .Include(versionEntity =>
                    versionEntity.Prompt)
                    .ThenInclude(prompt =>
                        prompt.Author)

                .Include(versionEntity =>
                    versionEntity.Category)

                .Include(versionEntity =>
                    versionEntity.VersionTags)
                    .ThenInclude(versionTag =>
                        versionTag.Tag)

                .Include(versionEntity =>
                    versionEntity.Variables)

                .Include(versionEntity =>
                    versionEntity.MediaItems)

                .SingleOrDefaultAsync(
                    versionEntity =>
                        versionEntity.PromptId ==
                            id &&
                        versionEntity.Status ==
                            PromptVersionStatus.PendingModeration,
                    cancellationToken);

        if (version is null)
        {
            return null;
        }

        var prompt =
            version.Prompt;

        return new ModerationReviewViewModel
        {
            Id =
                prompt.Id,


            Title =
                version.Title,

            Summary =
                version.Summary,

            CategoryName =
                version.Category.Name,

            Tags =
                CreateModerationTagBadges(
                    version),

            CategoryId =
                version.CategoryId,

            CategoryIsActive =
                version.Category.IsActive,

            CategoryOptions =
                await LoadPromptCategoryOptionsAsync(
                    version.CategoryId,
                    cancellationToken),

            DifficultyName =
                GetDifficultyName(
                    version.Difficulty),

            Difficulty =
                version.Difficulty,

            DifficultyOptions =
                BuildDifficultyOptions(
                    version.Difficulty),


            AuthorName =
                GetAuthorName(
                    prompt.Author),

            AuthorEmail =
                prompt.Author.Email
                ?? string.Empty,

            SubmittedAtUtc =
                version.SubmittedAtUtc,


            ReadyText =
                version.ReadyText,

            ConstructorText =
                CreateDisplayTemplate(
                    version.TemplateText,
                    version.Variables),

            UsageInstructions =
                version.UsageInstructions,

            ExampleResult =
                version.ExampleResult,

            Variables =
                version.Variables
                    .OrderBy(variable =>
                        variable.SortOrder)
                    .Select(variable =>
                        new ModerationVariableViewModel
                        {
                            Label =
                                variable.Label,

                            TypeName =
                                GetVariableTypeName(
                                    variable.VariableType),

                            IsRequired =
                                variable.IsRequired,

                            Placeholder =
                                variable.Placeholder
                                ?? string.Empty
                        })
                    .ToArray(),

            // ---------------------------------------------------------------------
            // EN: Only media belonging to this version.
            // RU: Только медиа проверяемой версии.
            // ---------------------------------------------------------------------

            MediaItems =
                version.MediaItems
                    .Where(media =>
                        media.Status !=
                            PromptMediaStatus.Archived &&
                        media.Status !=
                            PromptMediaStatus.Rejected)
                    .OrderBy(media =>
                        media.SortOrder)
                    .ThenBy(media =>
                        media.CreatedAtUtc)
                    .Select(media =>
                        new ModerationReviewMediaItemViewModel
                        {
                            Id =
                                media.Id,

                            Caption =
                                media.Caption,

                            AltText =
                                media.AltText,

                            StatusName =
                                GetMediaStatusName(
                                    media.Status),

                            IsCover =
                                media.IsCover,

                            SortOrder =
                                media.SortOrder
                        })
                    .ToArray()
        };
    }


    private static string CreateDisplayTemplate(
        string templateText,
        IEnumerable<PromptVariable> variables)
    {
        if (string.IsNullOrWhiteSpace(
            templateText))
        {
            return string.Empty;
        }

        var displayTemplate =
            Regex.Replace(
                templateText,
                @"\[\[if\s+(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\]\](?<content>.*?)\[\[endif\]\]",
                match =>
                    match.Groups["content"]
                        .Value
                        .Trim('\r', '\n'),
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline |
                RegexOptions.CultureInvariant);

        foreach (var variable in variables
                     .OrderByDescending(
                         variable =>
                             variable.Key.Length))
        {
            var pattern =
                @"\{\{\s*" +
                Regex.Escape(
                    variable.Key) +
                @"\s*\}\}";

            displayTemplate =
                Regex.Replace(
                    displayTemplate,
                    pattern,
                    _ =>
                        $"[{variable.Label}]",
                    RegexOptions.IgnoreCase |
                    RegexOptions
                        .CultureInvariant);
        }

        return displayTemplate.Trim();
    }

    private static IReadOnlyList<SelectListItem>
    BuildDifficultyOptions(
        PromptDifficulty currentDifficulty)
    {
        return new[]
        {
        new SelectListItem
        {
            Value =
                PromptDifficulty.Beginner.ToString(),

            Text =
                GetDifficultyName(
                    PromptDifficulty.Beginner),

            Selected =
                currentDifficulty ==
                PromptDifficulty.Beginner
        },

        new SelectListItem
        {
            Value =
                PromptDifficulty.Intermediate.ToString(),

            Text =
                GetDifficultyName(
                    PromptDifficulty.Intermediate),

            Selected =
                currentDifficulty ==
                PromptDifficulty.Intermediate
        },

        new SelectListItem
        {
            Value =
                PromptDifficulty.Advanced.ToString(),

            Text =
                GetDifficultyName(
                    PromptDifficulty.Advanced),

            Selected =
                currentDifficulty ==
                PromptDifficulty.Advanced
        }
    };
    }

    private async Task<IReadOnlyList<SelectListItem>>
    LoadPromptCategoryOptionsAsync(
        int currentCategoryId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                category.IsActive ||
                category.Id == currentCategoryId)
            .OrderBy(category =>
                category.SortOrder)
            .ThenBy(category =>
                category.Name)
            .Select(category =>
                new SelectListItem
                {
                    Value =
                        category.Id.ToString(),

                    Text =
                        category.IsActive
                            ? category.Name
                            : category.Name + " · скрыта",

                    Selected =
                        category.Id == currentCategoryId
                })
            .ToListAsync(
                cancellationToken);
    }


    private static IReadOnlyList<PromptTagBadgeViewModel>
    CreateModerationTagBadges(
        Prompt prompt)
    {
        return prompt.PromptTags
            .OrderBy(promptTag =>
                promptTag.Tag.SortOrder)
            .ThenBy(promptTag =>
                promptTag.Tag.Name)
            .Select(promptTag =>
                new PromptTagBadgeViewModel
                {
                    Name =
                        promptTag.Tag.Name,

                    Slug =
                        promptTag.Tag.Slug
                })
            .ToArray();
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

    private static string GetDifficultyName(
        PromptDifficulty difficulty)
    {
        return difficulty switch
        {
            PromptDifficulty.Beginner =>
                "Для новичков",

            PromptDifficulty.Intermediate =>
                "Средний",

            PromptDifficulty.Advanced =>
                "Продвинутый",

            _ => difficulty.ToString()
        };
    }


    private static IReadOnlyList<PromptTagBadgeViewModel>
    CreateModerationTagBadges(
        PromptVersion version)
    {
        return version.VersionTags
            .OrderBy(versionTag =>
                versionTag.Tag.SortOrder)
            .ThenBy(versionTag =>
                versionTag.Tag.Name)
            .Select(versionTag =>
                new PromptTagBadgeViewModel
                {
                    Name =
                        versionTag.Tag.Name,

                    Slug =
                        versionTag.Tag.Slug
                })
            .ToArray();
    }
    private static string GetVariableTypeName(
        PromptVariableType variableType)
    {
        return variableType switch
        {
            PromptVariableType.Text =>
                "Короткий текст",

            PromptVariableType.TextArea =>
                "Большой текст",

            PromptVariableType.Number =>
                "Число",

            PromptVariableType.Select =>
                "Выбор из списка",

            PromptVariableType.Checkbox =>
                "Да или нет",

            _ => variableType.ToString()
        };
    }

    private static string GetReportReasonName(
        PromptReportReason reason)
    {
        return reason switch
        {
            PromptReportReason.Spam =>
                "Спам или реклама",

            PromptReportReason.IncorrectContent =>
                "Некорректный или бесполезный промпт",

            PromptReportReason.UnsafeContent =>
                "Небезопасное содержание",

            PromptReportReason.Copyright =>
                "Нарушение авторских прав",

            PromptReportReason.Other =>
                "Другое",

            _ =>
                "Не указано"
        };
    }


    private static string GetMediaStatusName(
    PromptMediaStatus status)
    {
        return status switch
        {
            PromptMediaStatus.Draft =>
                "Черновик",

            PromptMediaStatus.PendingModeration =>
                "На модерации",

            PromptMediaStatus.Published =>
                "Опубликовано",

            PromptMediaStatus.Rejected =>
                "Отклонено",

            PromptMediaStatus.Archived =>
                "В архиве",

            _ =>
                status.ToString()
        };
    }

    private static void SyncPromptTagsFromVersion(
    Prompt prompt,
    PromptVersion version)
    {
        prompt.PromptTags.Clear();

        foreach (var versionTag in
                 version.VersionTags)
        {
            prompt.PromptTags.Add(
                new PromptTag
                {
                    Prompt =
                        prompt,

                    TagId =
                        versionTag.TagId
                });
        }
    }


}
