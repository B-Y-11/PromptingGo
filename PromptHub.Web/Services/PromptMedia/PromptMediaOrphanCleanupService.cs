using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.MediaStorage;

namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaOrphanCleanupService
    : IPromptMediaOrphanCleanupService
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly ILocalMediaStorageCatalog
        _storageCatalog;

    private readonly IMediaStorageService
        _mediaStorageService;

    private readonly PromptMediaCleanupOptions
        _options;

    private readonly ILogger<
        PromptMediaOrphanCleanupService>
        _logger;

    private readonly TimeProvider
        _timeProvider;

    public PromptMediaOrphanCleanupService(
        ApplicationDbContext dbContext,
        ILocalMediaStorageCatalog storageCatalog,
        IMediaStorageService mediaStorageService,
        IOptions<PromptMediaCleanupOptions> options,
        ILogger<PromptMediaOrphanCleanupService> logger,
        TimeProvider timeProvider)
    {
        _dbContext =
            dbContext;

        _storageCatalog =
            storageCatalog;

        _mediaStorageService =
            mediaStorageService;

        _options =
            options.Value;

        _logger =
            logger;

        _timeProvider =
            timeProvider;
    }


    public async Task<PromptMediaOrphanCleanupResult>
        CleanupAsync(
            bool dryRun,
            CancellationToken cancellationToken)
    {
        var result =
            new PromptMediaOrphanCleanupResult
            {
                IsDryRun =
                    dryRun
            };

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var safetyCutoffUtc =
            nowUtc.AddHours(
                -_options.OrphanSafetyWindowHours);

        var storageKeyComparer =
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        var referencedStorageKeys =
            new HashSet<string>(
                storageKeyComparer);


        var originalStorageKeys =
            await _dbContext.PromptMediaItems
                .AsNoTracking()
                .Where(media =>
                    media.StorageProvider ==
                        MediaStorageProvider.Local &&
                    media.StorageKey !=
                        null &&
                    media.StorageKey !=
                        string.Empty)
                .Select(media =>
                    media.StorageKey!)
                .ToListAsync(
                    cancellationToken);

        foreach (var storageKey in
                 originalStorageKeys)
        {
            referencedStorageKeys.Add(
                NormalizeStorageKey(
                    storageKey));
        }


        var thumbnailStorageKeys =
            await _dbContext.PromptMediaItems
                .AsNoTracking()
                .Where(media =>
                    media.StorageProvider ==
                        MediaStorageProvider.Local &&
                    media.ThumbnailStorageKey !=
                        null &&
                    media.ThumbnailStorageKey !=
                        string.Empty)
                .Select(media =>
                    media.ThumbnailStorageKey!)
                .ToListAsync(
                    cancellationToken);

        foreach (var storageKey in
                 thumbnailStorageKeys)
        {
            referencedStorageKeys.Add(
                NormalizeStorageKey(
                    storageKey));
        }


        await foreach (var storedFile in
                       _storageCatalog
                           .EnumerateFilesAsync(
                               cancellationToken))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            result.ScannedFileCount++;

            var normalizedStorageKey =
                NormalizeStorageKey(
                    storedFile.StorageKey);

            if (referencedStorageKeys.Contains(
                    normalizedStorageKey))
            {
                result.ReferencedFileCount++;

                continue;
            }

            // -----------------------------------------------------------------
            // EN: A recently created unreferenced file may still belong to an
            //     upload whose database transaction has not completed.
            // RU: Недавно созданный файл может относиться к загрузке, чья
            //     транзакция базы данных ещё не завершилась.
            // -----------------------------------------------------------------

            if (storedFile.LastWriteTimeUtc >
                safetyCutoffUtc)
            {
                result.RecentUnreferencedFileCount++;

                continue;
            }

            result.EligibleOrphanCount++;

            result.EligibleBytes +=
                storedFile.FileSizeBytes;

            if (dryRun)
            {
                continue;
            }

            result.ProcessedCount++;

            try
            {
                await _mediaStorageService
                    .DeleteAsync(
                        normalizedStorageKey,
                        cancellationToken);

                result.DeletedCount++;

                result.DeletedBytes +=
                    storedFile.FileSizeBytes;

                _logger.LogInformation(
                    "Deleted orphan prompt media file {StorageKey}.",
                    normalizedStorageKey);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                result.FailedCount++;

                result.Errors.Add(
                    new PromptMediaOrphanCleanupError
                    {
                        StorageKey =
                            normalizedStorageKey,

                        Message =
                            exception.Message
                    });

                _logger.LogError(
                    exception,
                    "Failed to delete orphan prompt media file {StorageKey}.",
                    normalizedStorageKey);
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // EN: Platform-independent storage-key normalization
    // RU: Независимая от платформы нормализация ключа хранилища
    // -------------------------------------------------------------------------

    private static string NormalizeStorageKey(
        string storageKey)
    {
        return storageKey
            .Replace(
                '\\',
                '/')
            .TrimStart('/');
    }
}
