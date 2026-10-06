using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.PromptMedia;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Tests.Services.PromptMedia;

public sealed class PromptMediaArrangementServiceTests
{
    private static readonly DateTime UpdatedAtUtc =
        new(
            2026,
            8,
            4,
            15,
            0,
            0,
            DateTimeKind.Utc);

    private readonly PromptMediaArrangementService
        _service =
            new();

    // -------------------------------------------------------------------------
    // EN: Cover selection tests
    // RU: Тесты выбора обложки
    // -------------------------------------------------------------------------

    [Fact]
    public void SetCover_MarksOnlySelectedMediaAsCover()
    {
        var first =
            CreateMedia(
                id: 1,
                sortOrder: 0,
                isCover: true);

        var second =
            CreateMedia(
                id: 2,
                sortOrder: 1);

        var third =
            CreateMedia(
                id: 3,
                sortOrder: 2,
                isCover: true);

        var mediaItems =
            new[]
            {
                first,
                second,
                third
            };

        var result =
            _service.SetCover(
                mediaItems,
                selectedMediaId: 2,
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaCoverUpdateResult.Applied,
            result);

        Assert.False(
            first.IsCover);

        Assert.True(
            second.IsCover);

        Assert.False(
            third.IsCover);

        Assert.Single(
            mediaItems,
            media =>
                media.IsCover);
    }

    [Fact]
    public void SetCover_UpdatesChangedMediaTimestamps()
    {
        var first =
            CreateMedia(
                id: 1,
                sortOrder: 0,
                isCover: true);

        var second =
            CreateMedia(
                id: 2,
                sortOrder: 1);

        _service.SetCover(
            new[]
            {
                first,
                second
            },
            selectedMediaId: 2,
            UpdatedAtUtc);

        Assert.Equal(
            UpdatedAtUtc,
            first.UpdatedAtUtc);

        Assert.Equal(
            UpdatedAtUtc,
            second.UpdatedAtUtc);
    }

    [Fact]
    public void SetCover_ReturnsNoChanges_WhenSelectedMediaIsOnlyCover()
    {
        var first =
            CreateMedia(
                id: 1,
                sortOrder: 0,
                isCover: true);

        var second =
            CreateMedia(
                id: 2,
                sortOrder: 1);

        var result =
            _service.SetCover(
                new[]
                {
                    first,
                    second
                },
                selectedMediaId: 1,
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaCoverUpdateResult.NoChanges,
            result);

        Assert.Null(
            first.UpdatedAtUtc);

        Assert.Null(
            second.UpdatedAtUtc);
    }

    [Fact]
    public void SetCover_ReturnsMediaNotFound_ForUnknownMedia()
    {
        var result =
            _service.SetCover(
                new[]
                {
                    CreateMedia(
                        id: 1,
                        sortOrder: 0)
                },
                selectedMediaId: 999,
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaCoverUpdateResult.MediaNotFound,
            result);
    }

    // -------------------------------------------------------------------------
    // EN: Complete ordering tests
    // RU: Тесты полной сортировки
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyOrder_AppliesSubmittedActiveMediaOrder()
    {
        var first =
            CreateMedia(
                id: 1,
                sortOrder: 0);

        var second =
            CreateMedia(
                id: 2,
                sortOrder: 1);

        var third =
            CreateMedia(
                id: 3,
                sortOrder: 2,
                status:
                    PromptMediaStatus.PendingModeration);

        var result =
            _service.ApplyOrder(
                new[]
                {
                    first,
                    second,
                    third
                },
                new[]
                {
                    3,
                    1,
                    2
                },
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaOrderUpdateResult.Applied,
            result);

        Assert.Equal(
            1,
            first.SortOrder);

        Assert.Equal(
            2,
            second.SortOrder);

        Assert.Equal(
            0,
            third.SortOrder);
    }

    [Fact]
    public void ApplyOrder_ReturnsNoChanges_WhenOrderIsAlreadyNormalized()
    {
        var first =
            CreateMedia(
                id: 1,
                sortOrder: 0);

        var second =
            CreateMedia(
                id: 2,
                sortOrder: 1);

        var result =
            _service.ApplyOrder(
                new[]
                {
                    first,
                    second
                },
                new[]
                {
                    1,
                    2
                },
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaOrderUpdateResult.NoChanges,
            result);

        Assert.Null(
            first.UpdatedAtUtc);

        Assert.Null(
            second.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyOrder_RejectsDuplicateIds()
    {
        var mediaItems =
            new[]
            {
                CreateMedia(
                    id: 1,
                    sortOrder: 0),

                CreateMedia(
                    id: 2,
                    sortOrder: 1)
            };

        var result =
            _service.ApplyOrder(
                mediaItems,
                new[]
                {
                    1,
                    1
                },
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaOrderUpdateResult.InvalidOrder,
            result);
    }

    [Fact]
    public void ApplyOrder_RejectsMissingActiveMedia()
    {
        var mediaItems =
            new[]
            {
                CreateMedia(
                    id: 1,
                    sortOrder: 0),

                CreateMedia(
                    id: 2,
                    sortOrder: 1),

                CreateMedia(
                    id: 3,
                    sortOrder: 2)
            };

        var result =
            _service.ApplyOrder(
                mediaItems,
                new[]
                {
                    1,
                    2
                },
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaOrderUpdateResult.InvalidOrder,
            result);
    }

    [Fact]
    public void ApplyOrder_RejectsForeignMediaId()
    {
        var mediaItems =
            new[]
            {
                CreateMedia(
                    id: 1,
                    sortOrder: 0),

                CreateMedia(
                    id: 2,
                    sortOrder: 1)
            };

        var result =
            _service.ApplyOrder(
                mediaItems,
                new[]
                {
                    1,
                    999
                },
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaOrderUpdateResult.InvalidOrder,
            result);
    }

    [Fact]
    public void ApplyOrder_ExcludesArchivedAndRejectedMedia()
    {
        var first =
            CreateMedia(
                id: 1,
                sortOrder: 0);

        var second =
            CreateMedia(
                id: 2,
                sortOrder: 1);

        var archived =
            CreateMedia(
                id: 3,
                sortOrder: 50,
                status:
                    PromptMediaStatus.Archived);

        var rejected =
            CreateMedia(
                id: 4,
                sortOrder: 60,
                status:
                    PromptMediaStatus.Rejected);

        var result =
            _service.ApplyOrder(
                new[]
                {
                    first,
                    second,
                    archived,
                    rejected
                },
                new[]
                {
                    2,
                    1
                },
                UpdatedAtUtc);

        Assert.Equal(
            PromptMediaOrderUpdateResult.Applied,
            result);

        Assert.Equal(
            0,
            second.SortOrder);

        Assert.Equal(
            1,
            first.SortOrder);

        Assert.Equal(
            50,
            archived.SortOrder);

        Assert.Equal(
            60,
            rejected.SortOrder);

        Assert.Null(
            archived.UpdatedAtUtc);

        Assert.Null(
            rejected.UpdatedAtUtc);
    }


    private static PromptMediaEntity CreateMedia(
        int id,
        int sortOrder,
        bool isCover = false,
        PromptMediaStatus status =
            PromptMediaStatus.Published)
    {
        return new PromptMediaEntity
        {
            Id =
                id,

            PromptId =
                10,

            UploadedById =
                "author-user-id",

            MediaType =
                PromptMediaType.Image,

            Status =
                status,

            StorageProvider =
                MediaStorageProvider.Local,

            StorageKey =
                $"2026/08/image-{id}.jpg",

            SortOrder =
                sortOrder,

            IsCover =
                isCover
        };
    }
}