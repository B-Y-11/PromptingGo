using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Prompts;
using PromptHub.Web.Security;
using PromptHub.Web.Services.MediaStorage;
using PromptHub.Web.Services.PromptMedia;
using Npgsql;

namespace PromptHub.Web.Controllers;

public sealed class PromptMediaController
    : Controller
{
    private readonly ApplicationDbContext _dbContext;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IMediaStorageService _mediaStorageService;

    private readonly MediaStorageOptions _options;

    private readonly PromptMediaCleanupOptions
    _cleanupOptions;

    private readonly IPromptMediaAccessService
    _promptMediaAccessService;

    private readonly IPromptMediaArrangementService
    _promptMediaArrangementService;

    public PromptMediaController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IMediaStorageService mediaStorageService,
        IOptions<MediaStorageOptions> options,
        IOptions<PromptMediaCleanupOptions> cleanupOptions,
        IPromptMediaAccessService promptMediaAccessService,
        IPromptMediaArrangementService promptMediaArrangementService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _mediaStorageService = mediaStorageService;
        _options = options.Value;
        _cleanupOptions = cleanupOptions.Value;
        _promptMediaAccessService = promptMediaAccessService;
        _promptMediaArrangementService = promptMediaArrangementService;
    }

    // -------------------------------------------------------------------------
    // EN: Secure prompt media delivery
    // RU: Безопасная выдача медиа промптов
    // -------------------------------------------------------------------------

    [HttpGet(
        "media/prompt/{id:int}",
        Name = "PromptMediaContent")]
    public async Task<IActionResult> Content(
        int id,
        CancellationToken cancellationToken)
    {
        var media =
    await _dbContext.PromptMediaItems
        .AsNoTracking()
        .Include(item =>
            item.Prompt)
            .ThenInclude(prompt =>
                prompt.Category)
        .SingleOrDefaultAsync(
                    item =>
                        item.Id == id,
                    cancellationToken);

        if (media is null ||
            media.StorageProvider !=
                MediaStorageProvider.Local ||
            string.IsNullOrWhiteSpace(
                media.StorageKey))
        {
            return NotFound();
        }


        var isPubliclyVisible =
            _promptMediaAccessService
                .IsPubliclyVisible(
                    media);

        if (!_promptMediaAccessService.CanView(
                media,
                User))
        {
            return NotFound();
        }

        Response.Headers.CacheControl =
            isPubliclyVisible
                ? "public,max-age=300"
                : "private,no-store";

        try
        {
            var stream =
                await _mediaStorageService.OpenReadAsync(
                    media.StorageKey,
                    cancellationToken);

            var contentType =
                string.IsNullOrWhiteSpace(
                    media.ContentType)
                    ? "application/octet-stream"
                    : media.ContentType;

            return File(
                stream,
                contentType,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (MediaStorageException)
        {
            return NotFound();
        }
    }


    // -----------------------------------------------------------------------------
    // EN: Secure prompt media thumbnail delivery
    // RU: Безопасная выдача миниатюр медиа промптов
    // -----------------------------------------------------------------------------

    [HttpGet(
        "media/prompt/{id:int}/thumbnail",
        Name = "PromptMediaThumbnail")]
    public async Task<IActionResult> Thumbnail(
        int id,
        CancellationToken cancellationToken)
    {
        var media =
            await _dbContext.PromptMediaItems
                .AsNoTracking()
                .Include(item =>
    item.Prompt)
    .ThenInclude(prompt =>
        prompt.Category)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == id,
                    cancellationToken);

        if (media is null ||
            media.StorageProvider !=
                MediaStorageProvider.Local)
        {
            return NotFound();
        }


        var isPubliclyVisible =
            _promptMediaAccessService
                .IsPubliclyVisible(
                    media);

        if (!_promptMediaAccessService.CanView(
                media,
                User))
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: Old database records fall back to the original until backfilled.
        // RU: Старые записи используют оригинал до создания миниатюры.
        // -------------------------------------------------------------------------

        var storageKey =
            !string.IsNullOrWhiteSpace(
                media.ThumbnailStorageKey)
                ? media.ThumbnailStorageKey
                : media.StorageKey;

        var contentType =
            !string.IsNullOrWhiteSpace(
                media.ThumbnailStorageKey)
                ? media.ThumbnailContentType
                : media.ContentType;

        if (string.IsNullOrWhiteSpace(
            storageKey))
        {
            return NotFound();
        }

        Response.Headers.CacheControl =
            isPubliclyVisible
                ? "public,max-age=300"
                : "private,no-store";

        try
        {
            var stream =
                await _mediaStorageService.OpenReadAsync(
                    storageKey,
                    cancellationToken);

            return File(
                stream,
                string.IsNullOrWhiteSpace(
                    contentType)
                    ? "application/octet-stream"
                    : contentType,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (MediaStorageException)
        {
            return NotFound();
        }
    }




    [Authorize]
    [HttpGet("prompts/{promptId:int}/media")]
    public async Task<IActionResult> Manage(
    int promptId,
    CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(item =>
                    item.Id == promptId &&
                    item.AuthorId == userId)
                .Select(item =>
                    new
                    {
                        item.Id,
                        item.Title,
                        item.Status,

                        HasPendingVersion =
                            item.Versions.Any(version =>
                                version.Status ==
                                    PromptVersionStatus.PendingModeration)
                    })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        var mediaEntities =
            await _dbContext.PromptMediaItems
                .AsNoTracking()
                .Where(media =>
                    media.PromptId == prompt.Id)
                .OrderBy(media =>
                    media.Status ==
                        PromptMediaStatus.Archived)
                .ThenBy(media =>
                    media.SortOrder)
                .ThenBy(media =>
                    media.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        var activeImageCount =
            mediaEntities.Count(media =>
                media.Status !=
                    PromptMediaStatus.Archived &&
                media.Status !=
                    PromptMediaStatus.Rejected);

        // -------------------------------------------------------------------------
        // EN: While a prompt version is under moderation, media becomes read-only.
        //
        // RU: Пока версия промпта находится на модерации, медиа доступно
        //     только для просмотра.
        // -------------------------------------------------------------------------

        var canManage =
            CanManageMedia(
                prompt.Status) &&
            !prompt.HasPendingVersion;

        var canUpload =
            CanUploadMedia(
                prompt.Status) &&
            !prompt.HasPendingVersion;

        var reorderableMediaIds =
            mediaEntities
                .Where(media =>
                    media.Status !=
                        PromptMediaStatus.Archived &&
                    media.Status !=
                        PromptMediaStatus.Rejected)
                .OrderBy(media =>
                    media.SortOrder)
                .ThenBy(media =>
                    media.CreatedAtUtc)
                .ThenBy(media =>
                    media.Id)
                .Select(media =>
                    media.Id)
                .ToArray();

        var uploadBlockedReason =
            prompt.HasPendingVersion
                ? "Изменения промпта находятся на модерации. До решения модератора изображения доступны только для просмотра."
                : GetUploadBlockedReason(
                    prompt.Status);

        var model =
            new PromptMediaManagementViewModel
            {
                PromptId =
                    prompt.Id,

                PromptTitle =
                    prompt.Title,

                PromptStatus =
                    prompt.Status,

                PromptStatusName =
                    GetPromptStatusName(
                        prompt.Status),

                CanManage =
                    canManage,

                CanReorder =
                    canManage &&
                    reorderableMediaIds.Length > 1,

                CanUpload =
                    canUpload,

                UploadBlockedReason =
                    uploadBlockedReason,

                MaxImagesPerPrompt =
                    _options.MaxImagesPerPrompt,

                ActiveImageCount =
                    activeImageCount,

                RemainingImageSlots =
                    Math.Max(
                        0,
                        _options.MaxImagesPerPrompt -
                        activeImageCount),

                MediaItems =
                    mediaEntities
                        .Select(media =>
                        {
                            var reorderIndex =
                                Array.IndexOf(
                                    reorderableMediaIds,
                                    media.Id);

                            var hasStoredFile =
                                !media.StoragePurgedAtUtc.HasValue &&
                                (
                                    !string.IsNullOrWhiteSpace(
                                        media.StorageKey) ||
                                    !string.IsNullOrWhiteSpace(
                                        media.ExternalUrl)
                                );

                            var scheduledPurgeAtUtc =
                                GetScheduledPurgeAtUtc(
                                    media);

                            return new PromptMediaItemViewModel
                            {
                                Id =
                                    media.Id,

                                Status =
                                    media.Status,

                                StatusName =
                                    GetMediaStatusName(
                                        media.Status),

                                StatusBadgeClass =
                                    GetMediaStatusBadgeClass(
                                        media.Status),

                                OriginalFileName =
                                    media.OriginalFileName
                                    ?? string.Empty,

                                Caption =
                                    media.Caption,

                                AltText =
                                    media.AltText,

                                EditableCaption =
                                    media.PendingCaption
                                    ?? media.Caption,

                                EditableAltText =
                                    media.PendingAltText
                                    ?? media.AltText,

                                HasPendingMetadataReview =
                                    media.MetadataSubmittedAtUtc
                                        .HasValue,

                                MetadataModerationComment =
                                    media.MetadataModerationComment
                                    ?? string.Empty,

                                ModerationComment =
                                    media.ModerationComment
                                    ?? string.Empty,

                                IsCover =
                                    media.IsCover,

                                SortOrder =
                                    media.SortOrder,

                                IsReorderable =
                                    canManage &&
                                    reorderIndex >= 0 &&
                                    reorderableMediaIds.Length > 1,

                                CreatedAtUtc =
                                    media.CreatedAtUtc,

                                CanEditMetadata =
                                    canManage &&
                                    IsMetadataEditable(
                                        media.Status),

                                CanSetCover =
                                    canManage &&
                                    !media.IsCover &&
                                    IsCoverEligible(
                                        prompt.Status,
                                        media.Status),

                                HasStoredFile =
                                    hasStoredFile,

                                ScheduledPurgeAtUtc =
                                    scheduledPurgeAtUtc,

                                StoragePurgedAtUtc =
                                    media.StoragePurgedAtUtc,

                                CanMoveUp =
                                    canManage &&
                                    reorderIndex > 0,

                                CanMoveDown =
                                    canManage &&
                                    reorderIndex >= 0 &&
                                    reorderIndex <
                                        reorderableMediaIds.Length - 1,

                                CanArchive =
                                    canManage &&
                                    hasStoredFile &&
                                    media.Status is
                                        PromptMediaStatus.Draft or
                                        PromptMediaStatus.PendingModeration or
                                        PromptMediaStatus.Published
                            };
                        })
                        .ToArray()
            };

        return View(model);
    }


    [Authorize]
    [HttpPost("prompts/{promptId:int}/media/upload")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(
    MultipartBodyLengthLimit =
        35 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
    int promptId,
    List<IFormFile>? images,
    CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                userId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .Include(item =>
                    item.Versions)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == promptId &&
                        item.AuthorId == userId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        if (!CanUploadMedia(
                prompt.Status))
        {
            TempData["ErrorMessage"] =
                GetUploadBlockedReason(
                    prompt.Status);

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        // -------------------------------------------------------------------------
        // EN: A published prompt may have one hidden working version.
        //     Media uploaded while editing that version must belong to it.
        //
        // RU: У опубликованного промпта может быть одна скрытая рабочая версия.
        //     Загруженные во время её редактирования медиа должны принадлежать ей.
        // -------------------------------------------------------------------------

        var workingVersion =
            prompt.Versions
                .Where(version =>
                    version.Status is
                        PromptVersionStatus.Draft or
                        PromptVersionStatus.PendingModeration or
                        PromptVersionStatus.Rejected)
                .OrderByDescending(version =>
                    version.VersionNumber)
                .SingleOrDefault();

        // -------------------------------------------------------------------------
        // EN: Do not allow adding new files while the whole working version is
        //     already being reviewed.
        //
        // RU: Пока рабочая версия уже находится у модератора, новые файлы
        //     добавлять в неё нельзя.
        // -------------------------------------------------------------------------

        if (workingVersion?.Status ==
            PromptVersionStatus.PendingModeration)
        {
            TempData["ErrorMessage"] =
                "Изменения промпта уже находятся на модерации. Новые изображения сейчас добавить нельзя.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        // -------------------------------------------------------------------------
        // EN:
        //     Draft/Rejected prompt      -> its working version.
        //     Published + working copy   -> hidden working version.
        //     Published without changes  -> current published version.
        //
        // RU:
        //     Draft/Rejected             -> его рабочая версия.
        //     Published + рабочая копия  -> скрытая рабочая версия.
        //     Published без изменений    -> текущая опубликованная версия.
        // -------------------------------------------------------------------------

        var targetVersion =
            workingVersion
            ?? prompt.Versions
                .SingleOrDefault(version =>
                    version.IsCurrent);

        if (targetVersion is null)
        {
            TempData["ErrorMessage"] =
                "Не удалось определить версию промпта для загрузки изображения.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var isWorkingCopyUpload =
            prompt.Status ==
                PromptStatus.Published &&
            workingVersion is not null;

        var submittedFiles =
            images?
                .Where(file =>
                    file is not null &&
                    file.Length > 0)
                .ToList()
            ?? new List<IFormFile>();

        if (submittedFiles.Count == 0)
        {
            TempData["ErrorMessage"] =
                "Выберите хотя бы одно изображение.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var activeImageCount =
            await _dbContext.PromptMediaItems
                .CountAsync(
                    media =>
                        media.PromptId ==
                            promptId &&
                        media.Status !=
                            PromptMediaStatus.Archived &&
                        media.Status !=
                            PromptMediaStatus.Rejected,
                    cancellationToken);

        if (activeImageCount +
            submittedFiles.Count >
            _options.MaxImagesPerPrompt)
        {
            var remainingSlots =
                Math.Max(
                    0,
                    _options.MaxImagesPerPrompt -
                    activeImageCount);

            TempData["ErrorMessage"] =
                remainingSlots == 0
                    ? $"Для промпта уже загружено максимальное количество изображений: {_options.MaxImagesPerPrompt}."
                    : $"Сейчас можно загрузить ещё не более {remainingSlots} изображений.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var maxSortOrder =
            await _dbContext.PromptMediaItems
                .Where(media =>
                    media.PromptId ==
                        promptId)
                .MaxAsync(
                    media =>
                        (int?)media.SortOrder,
                    cancellationToken)
            ?? -1;

        var nextSortOrder =
            maxSortOrder + 1;

        var createdAtUtc =
            DateTime.UtcNow;

        // -------------------------------------------------------------------------
        // EN: Media belonging to a hidden working copy stays Draft until the
        //     working version itself is submitted.
        //
        //     Standalone media uploaded to an already published prompt keeps the
        //     existing separate moderation workflow.
        //
        // RU: Медиа скрытой рабочей версии остаётся Draft до отправки самой версии.
        //
        //     Обычная загрузка в опубликованный промпт без рабочей версии продолжает
        //     использовать существующую отдельную модерацию медиа.
        // -------------------------------------------------------------------------

        var initialStatus =
            isWorkingCopyUpload
                ? PromptMediaStatus.Draft
                : GetInitialMediaStatus(
                    prompt.Status);

        var storedFiles =
            new List<StoredMediaFile>();

        try
        {
            for (var index = 0;
                 index < submittedFiles.Count;
                 index++)
            {
                var storedFile =
                    await _mediaStorageService
                        .SavePromptImageAsync(
                            submittedFiles[index],
                            cancellationToken);

                storedFiles.Add(
                    storedFile);

                var media =
                    new PromptMedia
                    {
                        PromptId =
                            prompt.Id,

                        PromptVersionId =
                            targetVersion.Id,

                        UploadedById =
                            userId,

                        MediaType =
                            storedFile.MediaType,

                        Status =
                            initialStatus,

                        StorageProvider =
                            storedFile.StorageProvider,

                        OriginalFileName =
                            storedFile.OriginalFileName,

                        ContentType =
                            storedFile.ContentType,

                        FileSizeBytes =
                            storedFile.FileSizeBytes,

                        StorageKey =
                            storedFile.StorageKey,

                        PublicUrl =
                            storedFile.PublicUrl,

                        ThumbnailUrl =
                            storedFile.ThumbnailUrl,

                        OriginalWidth =
                            storedFile.OriginalWidth,

                        OriginalHeight =
                            storedFile.OriginalHeight,

                        ThumbnailStorageKey =
                            storedFile.ThumbnailStorageKey,

                        ThumbnailContentType =
                            storedFile.ThumbnailContentType,

                        ThumbnailFileSizeBytes =
                            storedFile.ThumbnailFileSizeBytes,

                        ThumbnailWidth =
                            storedFile.ThumbnailWidth,

                        ThumbnailHeight =
                            storedFile.ThumbnailHeight,

                        Caption =
                            string.Empty,

                        AltText =
                            string.Empty,

                        SortOrder =
                            nextSortOrder +
                            index,

                        IsCover =
                            activeImageCount == 0 &&
                            index == 0,

                        CreatedAtUtc =
                            createdAtUtc,

                        SubmittedAtUtc =
                            initialStatus ==
                                PromptMediaStatus.PendingModeration
                                ? createdAtUtc
                                : null
                    };

                _dbContext.PromptMediaItems.Add(
                    media);
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (MediaStorageException exception)
        {
            await DeleteStoredFilesQuietlyAsync(
                storedFiles);

            TempData["ErrorMessage"] =
                exception.UserMessage;

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }
        catch
        {
            await DeleteStoredFilesQuietlyAsync(
                storedFiles);

            throw;
        }

        TempData["SuccessMessage"] =
            isWorkingCopyUpload
                ? "Изображения добавлены в неопубликованные изменения. Они будут отправлены на модерацию вместе с промптом."
                : initialStatus ==
                    PromptMediaStatus.PendingModeration
                    ? "Изображения загружены и отправлены на модерацию. Опубликованные изображения пока остаются без изменений."
                    : "Изображения добавлены в черновик промпта.";

        return RedirectToAction(
            nameof(Manage),
            new
            {
                promptId
            });
    }



    [Authorize]
    [HttpPost(
        "prompts/{promptId:int}/media/{mediaId:int}/metadata")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMetadata(
        int promptId,
        int mediaId,
        UpdatePromptMediaMetadataViewModel model,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                ModelState.Values
                    .SelectMany(value =>
                        value.Errors)
                    .Select(error =>
                        error.ErrorMessage)
                    .FirstOrDefault()
                ?? "Проверьте введённые данные.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var media =
            await _dbContext.PromptMediaItems
                .Include(item =>
                    item.Prompt)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == mediaId &&
                        item.PromptId == promptId &&
                        item.Prompt.AuthorId == userId,
                    cancellationToken);

        if (media is null)
        {
            return NotFound();
        }


        if (await HasPendingPromptVersionAsync(
        promptId,
        cancellationToken))
        {
            TempData["ErrorMessage"] =
                "Изменения промпта уже находятся на модерации. Редактирование изображений временно недоступно.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (!CanManageMedia(
                media.Prompt.Status))
        {
            TempData["ErrorMessage"] =
                GetUploadBlockedReason(
                    media.Prompt.Status);

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (!IsMetadataEditable(
                media.Status))
        {
            TempData["ErrorMessage"] =
                "Для этого изображения редактирование данных недоступно.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var normalizedCaption =
            model.Caption?.Trim()
            ?? string.Empty;

        var normalizedAltText =
            model.AltText?.Trim()
            ?? string.Empty;

        var now =
            DateTime.UtcNow;

        var requiresSeparateModeration =
            media.Prompt.Status ==
                PromptStatus.Published &&
            media.Status ==
                PromptMediaStatus.Published;

        if (requiresSeparateModeration)
        {
            var comparisonCaption =
                media.MetadataSubmittedAtUtc.HasValue
                    ? media.PendingCaption
                      ?? string.Empty
                    : media.Caption;

            var comparisonAltText =
                media.MetadataSubmittedAtUtc.HasValue
                    ? media.PendingAltText
                      ?? string.Empty
                    : media.AltText;

            if (string.Equals(
                    comparisonCaption,
                    normalizedCaption,
                    StringComparison.Ordinal) &&
                string.Equals(
                    comparisonAltText,
                    normalizedAltText,
                    StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] =
                    "Подпись и alt-текст не изменились.";

                return RedirectToAction(
                    nameof(Manage),
                    new
                    {
                        promptId
                    });
            }

            media.PendingCaption =
                normalizedCaption;

            media.PendingAltText =
                normalizedAltText;

            media.MetadataSubmittedAtUtc =
                now;

            media.MetadataModeratedAtUtc =
                null;

            media.MetadataModeratedById =
                null;

            media.MetadataModerationComment =
                null;

            media.UpdatedAtUtc =
                now;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                "Изменения подписи и alt-текста отправлены на модерацию.";
        }
        else
        {
            if (string.Equals(
                    media.Caption,
                    normalizedCaption,
                    StringComparison.Ordinal) &&
                string.Equals(
                    media.AltText,
                    normalizedAltText,
                    StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] =
                    "Подпись и alt-текст не изменились.";

                return RedirectToAction(
                    nameof(Manage),
                    new
                    {
                        promptId
                    });
            }

            media.Caption =
                normalizedCaption;

            media.AltText =
                normalizedAltText;

            media.PendingCaption =
                null;

            media.PendingAltText =
                null;

            media.MetadataSubmittedAtUtc =
                null;

            media.MetadataModeratedAtUtc =
                null;

            media.MetadataModeratedById =
                null;

            media.MetadataModerationComment =
                null;

            media.UpdatedAtUtc =
                now;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                "Подпись и alt-текст сохранены.";
        }

        return RedirectToAction(
            nameof(Manage),
            new
            {
                promptId
            });
    }



    [Authorize]
    [HttpPost(
        "prompts/{promptId:int}/media/{mediaId:int}/cover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(
        int promptId,
        int mediaId,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .Include(item =>
                    item.MediaItems)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == promptId &&
                        item.AuthorId == userId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        if (await HasPendingPromptVersionAsync(
        promptId,
        cancellationToken))
        {
            TempData["ErrorMessage"] =
                "Изменения промпта уже находятся на модерации. Редактирование изображений временно недоступно.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (!CanManageMedia(
                prompt.Status))
        {
            TempData["ErrorMessage"] =
                GetUploadBlockedReason(
                    prompt.Status);

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var media =
            prompt.MediaItems
                .SingleOrDefault(item =>
                    item.Id == mediaId);

        if (media is null)
        {
            return NotFound();
        }

        if (!IsCoverEligible(
                prompt.Status,
                media.Status))
        {
            TempData["ErrorMessage"] =
                prompt.Status ==
                    PromptStatus.Published
                    ? "Обложкой опубликованного промпта можно назначить только одобренное изображение."
                    : "Это изображение нельзя назначить обложкой.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        // -------------------------------------------------------------------------
        // EN: Avoid unnecessary database updates when the selected image is already
        //     the only cover.
        // RU: Не выполняем лишнее обновление, если выбранное изображение уже
        //     является единственной обложкой.
        // -------------------------------------------------------------------------

        var currentCoverCount =
            prompt.MediaItems.Count(item =>
                item.IsCover);

        if (media.IsCover &&
            currentCoverCount == 1)
        {
            TempData["SuccessMessage"] =
                "Это изображение уже является обложкой.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var now =
                DateTime.UtcNow;

            var coverUpdateResult =
                _promptMediaArrangementService
                    .SetCover(
                        prompt.MediaItems,
                        media.Id,
                        now);

            if (coverUpdateResult ==
                PromptMediaCoverUpdateResult.MediaNotFound)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                return NotFound();
            }

            if (coverUpdateResult ==
                PromptMediaCoverUpdateResult.NoChanges)
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                TempData["SuccessMessage"] =
                    "Это изображение уже является обложкой.";

                return RedirectToAction(
                    nameof(Manage),
                    new
                    {
                        promptId
                    });
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                "Обложка промпта обновлена.";
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _dbContext.ChangeTracker.Clear();

            TempData["ErrorMessage"] =
                "Изображения промпта были изменены в другой вкладке. " +
                "Страница обновлена, повторите выбор обложки.";
        }
        catch (DbUpdateException exception)
            when (IsSingleCoverConstraintViolation(
                exception))
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _dbContext.ChangeTracker.Clear();

            TempData["ErrorMessage"] =
                "Обложка была одновременно изменена другим запросом. " +
                "Страница обновлена, повторите действие.";
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }

        return RedirectToAction(
            nameof(Manage),
            new
            {
                promptId
            });
    }




    [Authorize]
    [HttpPost(
        "prompts/{promptId:int}/media/{mediaId:int}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(
        int promptId,
        int mediaId,
        string direction,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var normalizedDirection =
            direction?.Trim().ToLowerInvariant()
            ?? string.Empty;

        if (normalizedDirection is not
            ("up" or "down"))
        {
            return BadRequest();
        }

        var prompt =
            await _dbContext.Prompts
                .Include(item =>
                    item.MediaItems)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == promptId &&
                        item.AuthorId == userId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }


        if (await HasPendingPromptVersionAsync(
        promptId,
        cancellationToken))
        {
            TempData["ErrorMessage"] =
                "Изменения промпта уже находятся на модерации. Редактирование изображений временно недоступно.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }


        if (!CanManageMedia(
                prompt.Status))
        {
            TempData["ErrorMessage"] =
                GetUploadBlockedReason(
                    prompt.Status);

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var orderedMedia =
            prompt.MediaItems
                .Where(item =>
                    item.Status !=
                        PromptMediaStatus.Archived &&
                    item.Status !=
                        PromptMediaStatus.Rejected)
                .OrderBy(item =>
                    item.SortOrder)
                .ThenBy(item =>
                    item.CreatedAtUtc)
                .ThenBy(item =>
                    item.Id)
                .ToList();

        var currentIndex =
            orderedMedia.FindIndex(item =>
                item.Id == mediaId);

        if (currentIndex < 0)
        {
            return NotFound();
        }

        var targetIndex =
            normalizedDirection == "up"
                ? currentIndex - 1
                : currentIndex + 1;

        if (targetIndex < 0 ||
            targetIndex >= orderedMedia.Count)
        {
            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        // -------------------------------------------------------------------------
        // EN: Normalize the sequence before swapping two adjacent items.
        // RU: Нормализуем порядок перед перестановкой двух соседних элементов.
        // -------------------------------------------------------------------------

        for (var index = 0;
             index < orderedMedia.Count;
             index++)
        {
            orderedMedia[index].SortOrder =
                index;
        }

        var currentMedia =
            orderedMedia[currentIndex];

        var targetMedia =
            orderedMedia[targetIndex];

        (currentMedia.SortOrder,
         targetMedia.SortOrder) =
            (targetMedia.SortOrder,
             currentMedia.SortOrder);

        var now =
            DateTime.UtcNow;

        currentMedia.UpdatedAtUtc =
            now;

        targetMedia.UpdatedAtUtc =
            now;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Порядок изображений обновлён.";

        return RedirectToAction(
            nameof(Manage),
            new
            {
                promptId
            });
    }



    [Authorize]
    [HttpPost(
        "prompts/{promptId:int}/media/reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(
        int promptId,
        ReorderPromptMediaViewModel model,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                "Не удалось определить новый порядок изображений.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var prompt =
            await _dbContext.Prompts
                .Include(item =>
                    item.MediaItems)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == promptId &&
                        item.AuthorId == userId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        if (await HasPendingPromptVersionAsync(
        promptId,
        cancellationToken))
        {
            TempData["ErrorMessage"] =
                "Изменения промпта уже находятся на модерации. Редактирование изображений временно недоступно.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (!CanManageMedia(
                prompt.Status))
        {
            TempData["ErrorMessage"] =
                GetUploadBlockedReason(
                    prompt.Status);

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var orderUpdateResult =
            _promptMediaArrangementService
                .ApplyOrder(
                    prompt.MediaItems,
                    model.OrderedMediaIds,
                    DateTime.UtcNow);

        if (orderUpdateResult ==
            PromptMediaOrderUpdateResult.InvalidOrder)
        {
            TempData["ErrorMessage"] =
                "Состав изображений изменился. " +
                "Обновите страницу и повторите попытку.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (orderUpdateResult ==
            PromptMediaOrderUpdateResult.NoChanges)
        {
            TempData["SuccessMessage"] =
                "Порядок изображений не изменился.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                "Новый порядок изображений сохранён.";
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();

            TempData["ErrorMessage"] =
                "Порядок изображений был изменён в другой вкладке. " +
                "Страница обновлена, повторите действие.";
        }

        return RedirectToAction(
            nameof(Manage),
            new
            {
                promptId
            });
    }




    // -------------------------------------------------------------------------
    // EN: Author media archiving
    // RU: Архивация медиа автором
    // -------------------------------------------------------------------------

    [Authorize]
    [HttpPost(
        "prompts/{promptId:int}/media/{mediaId:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(
        int promptId,
        int mediaId,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var media =
            await _dbContext.PromptMediaItems
                .Include(item =>
                    item.Prompt)
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == mediaId &&
                        item.PromptId == promptId &&
                        item.Prompt.AuthorId == userId,
                    cancellationToken);

        if (media is null)
        {
            return NotFound();
        }

        if (await HasPendingPromptVersionAsync(
        promptId,
        cancellationToken))
        {
            TempData["ErrorMessage"] =
                "Изменения промпта уже находятся на модерации. Редактирование изображений временно недоступно.";

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (!CanManageMedia(
        media.Prompt.Status))
        {
            TempData["ErrorMessage"] =
                GetUploadBlockedReason(
                    media.Prompt.Status);

            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        if (media.Status ==
            PromptMediaStatus.Archived)
        {
            return RedirectToAction(
                nameof(Manage),
                new
                {
                    promptId
                });
        }

        var wasCover =
            media.IsCover;

        media.Status =
            PromptMediaStatus.Archived;

        media.IsCover =
            false;

        media.ArchivedAtUtc =
            DateTime.UtcNow;

        media.ArchivedById =
            userId;

        media.ArchiveReason =
            "Скрыто автором.";


        // -----------------------------------------------------------------------------
        // EN: Archived media must leave the metadata moderation queue.
        // RU: Архивное медиа должно быть удалено из очереди метаданных.
        // -----------------------------------------------------------------------------

        media.PendingCaption =
            null;

        media.PendingAltText =
            null;

        media.MetadataSubmittedAtUtc =
            null;

        media.MetadataModeratedAtUtc =
            null;

        media.MetadataModeratedById =
            null;

        media.MetadataModerationComment =
            null;

        media.UpdatedAtUtc =
            DateTime.UtcNow;

        if (wasCover)
        {
            var replacementCover =
                await _dbContext.PromptMediaItems
                    .Where(item =>
                        item.PromptId == promptId &&
                        item.Id != media.Id &&
                        item.Status !=
                            PromptMediaStatus.Archived &&
                        item.Status !=
                            PromptMediaStatus.Rejected &&
                        (
                            media.Prompt.Status !=
                                PromptStatus.Published ||
                            item.Status ==
                                PromptMediaStatus.Published
                        ))
                    .OrderBy(item =>
                        item.Status ==
                        PromptMediaStatus.Published
                            ? 0
                            : 1)
                    .ThenBy(item =>
                        item.SortOrder)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (replacementCover is not null)
            {
                replacementCover.IsCover =
                    true;
            }
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Изображение убрано из галереи.";

        return RedirectToAction(
            nameof(Manage),
            new
            {
                promptId
            });
    }



    private DateTime? GetScheduledPurgeAtUtc(
        PromptMedia media)
    {
        if (media.StoragePurgedAtUtc.HasValue)
        {
            return null;
        }

        return media.Status switch
        {
            PromptMediaStatus.Archived
                when media.ArchivedAtUtc.HasValue =>
                    media.ArchivedAtUtc.Value.AddDays(
                        _cleanupOptions.ArchivedRetentionDays),

            PromptMediaStatus.Rejected
                when media.ModeratedAtUtc.HasValue =>
                    media.ModeratedAtUtc.Value.AddDays(
                        _cleanupOptions.RejectedRetentionDays),

            _ =>
                null
        };
    }





    private async Task<bool> HasPendingPromptVersionAsync(
    int promptId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.PromptVersions
            .AsNoTracking()
            .AnyAsync(
                version =>
                    version.PromptId ==
                        promptId &&
                    version.Status ==
                        PromptVersionStatus.PendingModeration,
                cancellationToken);
    }


    private static bool CanManageMedia(
    PromptStatus promptStatus)
    {
        return promptStatus is
            PromptStatus.Draft or
            PromptStatus.Rejected or
            PromptStatus.Published;
    }

    private static bool IsMetadataEditable(
        PromptMediaStatus mediaStatus)
    {
        return mediaStatus is
            PromptMediaStatus.Draft or
            PromptMediaStatus.PendingModeration or
            PromptMediaStatus.Published;
    }

    private static bool IsCoverEligible(
        PromptStatus promptStatus,
        PromptMediaStatus mediaStatus)
    {
        if (promptStatus ==
            PromptStatus.Published)
        {
            return mediaStatus ==
                PromptMediaStatus.Published;
        }

        return mediaStatus is
            PromptMediaStatus.Draft or
            PromptMediaStatus.PendingModeration;
    }


    private static bool CanUploadMedia(
    PromptStatus promptStatus)
    {
        return CanManageMedia(
            promptStatus);
    }

    private static PromptMediaStatus GetInitialMediaStatus(
        PromptStatus promptStatus)
    {
        return promptStatus ==
               PromptStatus.Published
            ? PromptMediaStatus.PendingModeration
            : PromptMediaStatus.Draft;
    }

    private static string GetUploadBlockedReason(
        PromptStatus promptStatus)
    {
        return promptStatus switch
        {
            PromptStatus.PendingModeration =>
                "Пока промпт находится на модерации, добавлять изображения нельзя.",

            PromptStatus.Archived =>
                "Для скрытого промпта загрузка изображений недоступна.",

            _ =>
                string.Empty
        };
    }

    private static string GetPromptStatusName(
        PromptStatus status)
    {
        return status switch
        {
            PromptStatus.Draft =>
                "Черновик",

            PromptStatus.PendingModeration =>
                "На модерации",

            PromptStatus.Published =>
                "Опубликован",

            PromptStatus.Rejected =>
                "Отклонён",

            PromptStatus.Archived =>
                "Скрыт",

            _ =>
                status.ToString()
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

    private static string GetMediaStatusBadgeClass(
        PromptMediaStatus status)
    {
        return status switch
        {
            PromptMediaStatus.Draft =>
                "text-bg-secondary",

            PromptMediaStatus.PendingModeration =>
                "text-bg-warning",

            PromptMediaStatus.Published =>
                "text-bg-success",

            PromptMediaStatus.Rejected =>
                "text-bg-danger",

            PromptMediaStatus.Archived =>
                "text-bg-dark",

            _ =>
                "text-bg-secondary"
        };
    }


    // -----------------------------------------------------------------------------
    // EN: PostgreSQL single-cover constraint conflict detection
    // RU: Определение конфликта ограничения единственной обложки в PostgreSQL
    // -----------------------------------------------------------------------------

    private static bool IsSingleCoverConstraintViolation(
        DbUpdateException exception)
    {
        return
            exception.InnerException is
                PostgresException postgresException &&
            postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation &&
            string.Equals(
                postgresException.ConstraintName,
                "UX_PromptMedia_PromptId_IsCover",
                StringComparison.Ordinal);
    }

    private async Task DeleteStoredFilesQuietlyAsync(
    IEnumerable<StoredMediaFile> storedFiles)
    {
        foreach (var storedFile in storedFiles)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(
                        storedFile.ThumbnailStorageKey))
                {
                    await _mediaStorageService.DeleteAsync(
                        storedFile.ThumbnailStorageKey,
                        CancellationToken.None);
                }

                if (!string.IsNullOrWhiteSpace(
                        storedFile.StorageKey))
                {
                    await _mediaStorageService.DeleteAsync(
                        storedFile.StorageKey,
                        CancellationToken.None);
                }
            }
            catch
            {
                // EN: Cleanup must not hide the original upload error.
                // RU: Ошибка очистки не должна скрывать исходную ошибку загрузки.
            }
        }
    }
}
