using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.MediaStorage;
using PromptHub.Web.Services.PromptMedia;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Tests.Services.PromptMedia;

public sealed class PromptMediaCleanupServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(
            2026,
            8,
            5,
            12,
            0,
            0,
            TimeSpan.Zero);

    // -------------------------------------------------------------------------
    // EN: Dry-run behavior
    // RU: Проверка режима без удаления
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_DryRun_ReportsEligibleMediaWithoutChanges()
    {
        await using var dbContext =
            CreateDbContext();

        var archived =
            CreateMedia(
                id: 1,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-31));

        var rejected =
            CreateMedia(
                id: 2,
                status:
                    PromptMediaStatus.Rejected,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-31));

        var freshArchived =
            CreateMedia(
                id: 3,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-29));

        dbContext.PromptMediaItems.AddRange(
            archived,
            rejected,
            freshArchived);

        await dbContext.SaveChangesAsync();

        var expectedEligibleBytes =
            (archived.FileSizeBytes ?? 0L) +
            (archived.ThumbnailFileSizeBytes ?? 0L) +
            (rejected.FileSizeBytes ?? 0L) +
            (rejected.ThumbnailFileSizeBytes ?? 0L);

        var storage =
            new FakeMediaStorageService();

        var service =
            CreateService(
                dbContext,
                storage);

        var result =
            await service.CleanupAsync(
                dryRun: true,
                CancellationToken.None);

        Assert.True(
            result.IsDryRun);

        Assert.Equal(
            2,
            result.EligibleCount);

        Assert.Equal(
            1,
            result.EligibleArchivedCount);

        Assert.Equal(
            1,
            result.EligibleRejectedCount);

        Assert.Equal(
            expectedEligibleBytes,
            result.EligibleBytes);

        Assert.Equal(
            0,
            result.ProcessedCount);

        Assert.Equal(
            0,
            result.PurgedCount);

        Assert.Equal(
            0,
            result.FailedCount);

        Assert.Equal(
            2,
            result.RemainingEligibleCount);

        Assert.Empty(
            storage.DeleteAttempts);

        dbContext.ChangeTracker.Clear();

        var storedMedia =
            await dbContext.PromptMediaItems
                .OrderBy(media =>
                    media.Id)
                .ToListAsync();

        Assert.All(
            storedMedia,
            media =>
            {
                Assert.NotNull(
                    media.StorageKey);

                Assert.NotNull(
                    media.ThumbnailStorageKey);

                Assert.Null(
                    media.StoragePurgedAtUtc);

                Assert.Null(
                    media.StoragePurgeReason);
            });
    }

    // -------------------------------------------------------------------------
    // EN: Complete batch processing
    // RU: Полная обработка одного пакета
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_PurgesEveryEligibleItemFromSameBatch()
    {
        await using var dbContext =
            CreateDbContext();

        dbContext.PromptMediaItems.AddRange(
            CreateMedia(
                id: 1,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-31),
                isCover: true),

            CreateMedia(
                id: 2,
                status:
                    PromptMediaStatus.Rejected,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-45)),

            CreateMedia(
                id: 3,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-60)));

        await dbContext.SaveChangesAsync();

        var storage =
            new FakeMediaStorageService();

        var service =
            CreateService(
                dbContext,
                storage,
                batchSize: 3);

        var result =
            await service.CleanupAsync(
                dryRun: false,
                CancellationToken.None);

        Assert.False(
            result.IsDryRun);

        Assert.Equal(
            3,
            result.EligibleCount);

        Assert.Equal(
            3,
            result.ProcessedCount);

        Assert.Equal(
            3,
            result.PurgedCount);

        Assert.Equal(
            0,
            result.FailedCount);

        Assert.Equal(
            0,
            result.RemainingEligibleCount);

        Assert.Equal(
            6,
            storage.DeleteAttempts.Count);

        Assert.Equal(
            new[]
            {
                "thumbnail-1.webp",
                "original-1.jpg",
                "thumbnail-2.webp",
                "original-2.jpg",
                "thumbnail-3.webp",
                "original-3.jpg"
            },
            storage.DeleteAttempts);

        dbContext.ChangeTracker.Clear();

        var storedMedia =
            await dbContext.PromptMediaItems
                .OrderBy(media =>
                    media.Id)
                .ToListAsync();

        Assert.Equal(
            3,
            storedMedia.Count);

        Assert.All(
            storedMedia,
            media =>
            {
                Assert.Null(
                    media.StorageKey);

                Assert.Null(
                    media.ThumbnailStorageKey);

                Assert.Null(
                    media.PublicUrl);

                Assert.Null(
                    media.ThumbnailUrl);

                Assert.False(
                    media.IsCover);

                Assert.NotNull(
                    media.StoragePurgedAtUtc);
            });

        Assert.Equal(
            PromptMediaStoragePurgeReason
                .ArchivedRetentionExpired,
            storedMedia[0].StoragePurgeReason);

        Assert.Equal(
            PromptMediaStoragePurgeReason
                .RejectedRetentionExpired,
            storedMedia[1].StoragePurgeReason);

        Assert.Equal(
            PromptMediaStoragePurgeReason
                .ArchivedRetentionExpired,
            storedMedia[2].StoragePurgeReason);
    }

    // -------------------------------------------------------------------------
    // EN: Ineligible states and retention dates
    // RU: Неподходящие состояния и сроки хранения
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_DoesNotPurgeFreshOrPublicMedia()
    {
        await using var dbContext =
            CreateDbContext();

        var freshArchived =
            CreateMedia(
                id: 1,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-29));

        var freshRejected =
            CreateMedia(
                id: 2,
                status:
                    PromptMediaStatus.Rejected,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-29));

        var published =
            CreateMedia(
                id: 3,
                status:
                    PromptMediaStatus.Published,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-100));

        var pending =
            CreateMedia(
                id: 4,
                status:
                    PromptMediaStatus.PendingModeration,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-100));

        var remoteArchived =
            CreateMedia(
                id: 5,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-100),
                storageProvider:
                    MediaStorageProvider.S3);

        var alreadyPurged =
            CreateMedia(
                id: 6,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-100));

        alreadyPurged.StoragePurgedAtUtc =
            FixedUtcNow.AddDays(-10)
                .UtcDateTime;

        dbContext.PromptMediaItems.AddRange(
            freshArchived,
            freshRejected,
            published,
            pending,
            remoteArchived,
            alreadyPurged);

        await dbContext.SaveChangesAsync();

        var storage =
            new FakeMediaStorageService();

        var service =
            CreateService(
                dbContext,
                storage);

        var result =
            await service.CleanupAsync(
                dryRun: false,
                CancellationToken.None);

        Assert.Equal(
            0,
            result.EligibleCount);

        Assert.Equal(
            0,
            result.ProcessedCount);

        Assert.Equal(
            0,
            result.PurgedCount);

        Assert.Equal(
            0,
            result.RemainingEligibleCount);

        Assert.Empty(
            storage.DeleteAttempts);
    }

    // -------------------------------------------------------------------------
    // EN: Idempotent repeated execution
    // RU: Безопасный повторный запуск
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_IsIdempotent()
    {
        await using var dbContext =
            CreateDbContext();

        dbContext.PromptMediaItems.Add(
            CreateMedia(
                id: 1,
                status:
                    PromptMediaStatus.Archived,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-31)));

        await dbContext.SaveChangesAsync();

        var storage =
            new FakeMediaStorageService();

        var service =
            CreateService(
                dbContext,
                storage);

        var firstResult =
            await service.CleanupAsync(
                dryRun: false,
                CancellationToken.None);

        var deleteAttemptCountAfterFirstRun =
            storage.DeleteAttempts.Count;

        var secondResult =
            await service.CleanupAsync(
                dryRun: false,
                CancellationToken.None);

        Assert.Equal(
            1,
            firstResult.PurgedCount);

        Assert.Equal(
            0,
            firstResult.RemainingEligibleCount);

        Assert.Equal(
            0,
            secondResult.EligibleCount);

        Assert.Equal(
            0,
            secondResult.ProcessedCount);

        Assert.Equal(
            0,
            secondResult.PurgedCount);

        Assert.Equal(
            deleteAttemptCountAfterFirstRun,
            storage.DeleteAttempts.Count);

        dbContext.ChangeTracker.Clear();

        var storedMedia =
            await dbContext.PromptMediaItems
                .SingleAsync();

        Assert.Null(
            storedMedia.StorageKey);

        Assert.Null(
            storedMedia.ThumbnailStorageKey);

        Assert.NotNull(
            storedMedia.StoragePurgedAtUtc);
    }

    // -------------------------------------------------------------------------
    // EN: Retry after a storage failure
    // RU: Повторная очистка после ошибки хранилища
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CleanupAsync_CanRetryAfterStorageDeletionFailure()
    {
        await using var dbContext =
            CreateDbContext();

        dbContext.PromptMediaItems.Add(
            CreateMedia(
                id: 1,
                status:
                    PromptMediaStatus.Rejected,
                lifecycleAtUtc:
                    FixedUtcNow.AddDays(-31)));

        await dbContext.SaveChangesAsync();

        var storage =
            new FakeMediaStorageService();

        storage.FailingStorageKeys.Add(
            "original-1.jpg");

        var service =
            CreateService(
                dbContext,
                storage);

        var failedResult =
            await service.CleanupAsync(
                dryRun: false,
                CancellationToken.None);

        Assert.Equal(
            1,
            failedResult.ProcessedCount);

        Assert.Equal(
            0,
            failedResult.PurgedCount);

        Assert.Equal(
            1,
            failedResult.FailedCount);

        Assert.Equal(
            1,
            failedResult.RemainingEligibleCount);

        Assert.Single(
            failedResult.Errors);

        dbContext.ChangeTracker.Clear();

        var mediaAfterFailure =
            await dbContext.PromptMediaItems
                .SingleAsync();

        Assert.NotNull(
            mediaAfterFailure.StorageKey);

        Assert.NotNull(
            mediaAfterFailure.ThumbnailStorageKey);

        Assert.Null(
            mediaAfterFailure.StoragePurgedAtUtc);

        storage.FailingStorageKeys.Clear();

        var retryResult =
            await service.CleanupAsync(
                dryRun: false,
                CancellationToken.None);

        Assert.Equal(
            1,
            retryResult.PurgedCount);

        Assert.Equal(
            0,
            retryResult.FailedCount);

        Assert.Equal(
            0,
            retryResult.RemainingEligibleCount);

        dbContext.ChangeTracker.Clear();

        var mediaAfterRetry =
            await dbContext.PromptMediaItems
                .SingleAsync();

        Assert.Null(
            mediaAfterRetry.StorageKey);

        Assert.Null(
            mediaAfterRetry.ThumbnailStorageKey);

        Assert.Equal(
            PromptMediaStoragePurgeReason
                .RejectedRetentionExpired,
            mediaAfterRetry.StoragePurgeReason);
    }


    private static PromptMediaCleanupService CreateService(
        ApplicationDbContext dbContext,
        FakeMediaStorageService storage,
        int batchSize = 100)
    {
        var options =
            Options.Create(
                new PromptMediaCleanupOptions
                {
                    ArchivedRetentionDays =
                        30,

                    RejectedRetentionDays =
                        30,

                    BatchSize =
                        batchSize
                });

        return new PromptMediaCleanupService(
            dbContext,
            storage,
            options,
            NullLogger<
                PromptMediaCleanupService>
                .Instance,
            new FixedTimeProvider(
                FixedUtcNow));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<
                    ApplicationDbContext>()
                .UseInMemoryDatabase(
                    $"prompt-media-cleanup-" +
                    $"{Guid.NewGuid():N}")
                .Options;

        return new ApplicationDbContext(
            options);
    }


    private static PromptMediaEntity CreateMedia(
        int id,
        PromptMediaStatus status,
        DateTimeOffset lifecycleAtUtc,
        bool isCover = false,
        MediaStorageProvider storageProvider =
            MediaStorageProvider.Local)
    {
        return new PromptMediaEntity
        {
            Id =
                id,

            PromptId =
                100,

            UploadedById =
                "author-user-id",

            MediaType =
                PromptMediaType.Image,

            Status =
                status,

            StorageProvider =
                storageProvider,

            OriginalFileName =
                $"image-{id}.jpg",

            ContentType =
                "image/jpeg",

            FileSizeBytes =
                100L + id,

            StorageKey =
                $"original-{id}.jpg",

            PublicUrl =
                $"/media/prompt/{id}",

            ThumbnailStorageKey =
                $"thumbnail-{id}.webp",

            ThumbnailContentType =
                "image/webp",

            ThumbnailFileSizeBytes =
                10L + id,

            ThumbnailUrl =
                $"/media/prompt/{id}/thumbnail",

            IsCover =
                isCover,

            ArchivedAtUtc =
                status ==
                    PromptMediaStatus.Archived
                    ? lifecycleAtUtc.UtcDateTime
                    : null,

            ModeratedAtUtc =
                status ==
                    PromptMediaStatus.Rejected
                    ? lifecycleAtUtc.UtcDateTime
                    : null
        };
    }

    // -------------------------------------------------------------------------
    // EN: Fixed application clock
    // RU: Фиксированные системные часы
    // -------------------------------------------------------------------------

    private sealed class FixedTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset
            _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow =
                utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    // -------------------------------------------------------------------------
    // EN: In-memory media storage test double
    // RU: Тестовая замена хранилища медиа в памяти
    // -------------------------------------------------------------------------

    private sealed class FakeMediaStorageService
        : IMediaStorageService
    {
        public List<string> DeleteAttempts
        {
            get;
        } = [];

        public HashSet<string> FailingStorageKeys
        {
            get;
        } = new(
            StringComparer.Ordinal);

        public Task<StoredMediaFile>
            SavePromptImageAsync(
                IFormFile file,
                CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "Загрузка файлов не используется в тестах cleanup.");
        }

        // -------------------------------------------------------------------------
        // EN: Profile avatar operations are not used by cleanup tests.
        // RU: Операции с аватарами не используются в тестах очистки.
        // -------------------------------------------------------------------------

        public Task<StoredProfileAvatarFile>
            SaveProfileAvatarAsync(
                IFormFile file,
                CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "Сохранение аватаров не используется в тестах cleanup.");
        }

        public Task<Stream>
            OpenProfileAvatarReadAsync(
                string storageKey,
                CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "Чтение аватаров не используется в тестах cleanup.");
        }

        public Task DeleteProfileAvatarAsync(
            string storageKey,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "Удаление аватаров не используется в тестах cleanup.");
        }

        public Task<Stream> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException(
                "Чтение файлов не используется в тестах cleanup.");
        }

        public Task DeleteAsync(
            string storageKey,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            DeleteAttempts.Add(
                storageKey);

            if (FailingStorageKeys.Contains(
                    storageKey))
            {
                throw new IOException(
                    $"Тестовая ошибка удаления файла: {storageKey}");
            }

            return Task.CompletedTask;
        }
    }
}