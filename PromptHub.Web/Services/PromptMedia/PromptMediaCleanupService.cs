using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.MediaStorage;
namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaCleanupService
    : IPromptMediaCleanupService
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly IMediaStorageService
        _mediaStorageService;

    private readonly PromptMediaCleanupOptions
        _options;

    private readonly TimeProvider
    _timeProvider;

    private readonly ILogger<PromptMediaCleanupService>
        _logger;

    public PromptMediaCleanupService(
        ApplicationDbContext dbContext,
        IMediaStorageService mediaStorageService,
        IOptions<PromptMediaCleanupOptions> options,
        ILogger<PromptMediaCleanupService> logger,
        TimeProvider timeProvider)
    {
        _dbContext =
            dbContext;

        _mediaStorageService =
            mediaStorageService;

        _options =
            options.Value;

        _logger =
            logger;
        _timeProvider = timeProvider;
    }


    public async Task<PromptMediaCleanupResult> CleanupAsync(
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var cleanupStartedAtUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var result =
            new PromptMediaCleanupResult
            {
                IsDryRun =
                    dryRun
            };

        var eligibleQuery =
            CreateEligibleQuery(
                cleanupStartedAtUtc);

        result.EligibleCount =
            await eligibleQuery.CountAsync(
                cancellationToken);

        result.EligibleArchivedCount =
            await eligibleQuery.CountAsync(
                media =>
                    media.Status ==
                        PromptMediaStatus.Archived,
                cancellationToken);

        result.EligibleRejectedCount =
            await eligibleQuery.CountAsync(
                media =>
                    media.Status ==
                        PromptMediaStatus.Rejected,
                cancellationToken);

        result.EligibleBytes =
            await eligibleQuery.SumAsync(
                media =>
                    (long?)(
                        (media.FileSizeBytes ?? 0L) +
                        (media.ThumbnailFileSizeBytes ?? 0L)),
                cancellationToken)
            ?? 0L;

        if (dryRun ||
            result.EligibleCount == 0)
        {
            result.RemainingEligibleCount =
                result.EligibleCount;

            return result;
        }

        var lastProcessedId =
            0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mediaItems =
                await CreateEligibleQuery(
                        cleanupStartedAtUtc)
                    .Where(media =>
                        media.Id >
                            lastProcessedId)
                    .OrderBy(media =>
                        media.Id)
                    .Take(
                        _options.BatchSize)
                    .ToListAsync(
                        cancellationToken);

            if (mediaItems.Count == 0)
            {
                break;
            }

            lastProcessedId =
                mediaItems[^1].Id;

            foreach (var media in mediaItems)
            {
                await PurgeMediaAsync(
                    media,
                    result,
                    cleanupStartedAtUtc,
                    cancellationToken);
            }
        }

        result.RemainingEligibleCount =
            await CreateEligibleQuery(
                    cleanupStartedAtUtc)
                .CountAsync(
                    cancellationToken);

        return result;
    }


    private async Task PurgeMediaAsync(
        PromptMediaEntity media,
        PromptMediaCleanupResult result,
        DateTime purgedAtUtc,
        CancellationToken cancellationToken)
    {
        result.ProcessedCount++;

        var originalStorageKey =
            media.StorageKey;

        var thumbnailStorageKey =
            media.ThumbnailStorageKey;

        var storedBytes =
            (media.FileSizeBytes ?? 0L) +
            (media.ThumbnailFileSizeBytes ?? 0L);

        try
        {
            // -----------------------------------------------------------------
            // EN: Delete the derived file first, then the private original.
            // RU: Сначала удаляем производный файл, затем приватный оригинал.
            // -----------------------------------------------------------------

            await DeleteIfPresentAsync(
                thumbnailStorageKey,
                cancellationToken);

            await DeleteIfPresentAsync(
                originalStorageKey,
                cancellationToken);

            media.StorageKey =
                null;

            media.ThumbnailStorageKey =
                null;

            media.PublicUrl =
                null;

            media.ThumbnailUrl =
                null;

            media.IsCover =
                false;

            media.StoragePurgedAtUtc =
                purgedAtUtc;

            media.StoragePurgeReason =
                GetPurgeReason(
                    media.Status);

            media.UpdatedAtUtc =
                purgedAtUtc;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            result.PurgedCount++;

            result.PurgedBytes +=
                storedBytes;

            _logger.LogInformation(
                "Purged physical files for prompt media {PromptMediaId}.",
                media.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            result.FailedCount++;

            result.Errors.Add(
                new PromptMediaCleanupError
                {
                    MediaId =
                        media.Id,

                    Message =
                        exception.Message
                });

            _logger.LogError(
                exception,
                "Failed to purge physical files for prompt media {PromptMediaId}.",
                media.Id);
        }
        finally
        {
            // ---------------------------------------------------------------------
            // EN: Detach only the processed row. Clearing the entire tracker would
            //     detach the remaining rows from the current cleanup batch.
            // RU: Отсоединяем только обработанную запись. Полная очистка трекера
            //     отсоединила бы остальные записи текущего пакета очистки.
            // ---------------------------------------------------------------------

            _dbContext.Entry(
                    media)
                .State =
                    EntityState.Detached;
        }
    }


    private IQueryable<PromptMediaEntity> CreateEligibleQuery(
        DateTime nowUtc)
    {
        var archivedCutoffUtc =
            nowUtc.AddDays(
                -_options.ArchivedRetentionDays);

        var rejectedCutoffUtc =
            nowUtc.AddDays(
                -_options.RejectedRetentionDays);

        return _dbContext.PromptMediaItems
            .Where(media =>
                media.StorageProvider ==
                    MediaStorageProvider.Local &&
                media.StoragePurgedAtUtc ==
                    null &&
                (
                    (
                        media.StorageKey !=
                            null &&
                        media.StorageKey !=
                            string.Empty
                    ) ||
                    (
                        media.ThumbnailStorageKey !=
                            null &&
                        media.ThumbnailStorageKey !=
                            string.Empty
                    )
                ) &&
                (
                    (
                        media.Status ==
                            PromptMediaStatus.Archived &&
                        media.ArchivedAtUtc !=
                            null &&
                        media.ArchivedAtUtc <=
                            archivedCutoffUtc
                    ) ||
                    (
                        media.Status ==
                            PromptMediaStatus.Rejected &&
                        media.ModeratedAtUtc !=
                            null &&
                        media.ModeratedAtUtc <=
                            rejectedCutoffUtc
                    )
                ));
    }

    // -------------------------------------------------------------------------
    // EN: Idempotent storage deletion
    // RU: Повторяемое удаление из хранилища
    // -------------------------------------------------------------------------

    private async Task DeleteIfPresentAsync(
        string? storageKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                storageKey))
        {
            return;
        }

        await _mediaStorageService.DeleteAsync(
            storageKey,
            cancellationToken);
    }

    private static PromptMediaStoragePurgeReason
        GetPurgeReason(
            PromptMediaStatus mediaStatus)
    {
        return mediaStatus switch
        {
            PromptMediaStatus.Archived =>
                PromptMediaStoragePurgeReason
                    .ArchivedRetentionExpired,

            PromptMediaStatus.Rejected =>
                PromptMediaStoragePurgeReason
                    .RejectedRetentionExpired,

            _ =>
                throw new InvalidOperationException(
                    "Текущее состояние медиа не поддерживает очистку.")
        };
    }
}
