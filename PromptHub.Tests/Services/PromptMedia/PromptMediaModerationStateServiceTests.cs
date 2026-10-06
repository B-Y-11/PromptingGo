using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.PromptMedia;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Tests.Services.PromptMedia;

public sealed class PromptMediaModerationStateServiceTests
{
    private const string ModeratorId =
        "moderator-user-id";

    private static readonly DateTime ModeratedAtUtc =
        new(
            2026,
            8,
            4,
            10,
            30,
            0,
            DateTimeKind.Utc);

    private readonly PromptMediaModerationStateService
        _service =
            new();

    // -------------------------------------------------------------------------
    // EN: New media approval tests
    // RU: Тесты одобрения нового медиа
    // -------------------------------------------------------------------------

    [Fact]
    public void ApproveNewMedia_PublishesPendingMedia()
    {
        var media =
            CreatePendingMedia();

        _service.ApproveNewMedia(
            media,
            ModeratorId,
            ModeratedAtUtc);

        Assert.Equal(
            PromptMediaStatus.Published,
            media.Status);

        Assert.Equal(
            ModeratorId,
            media.ModeratedById);

        Assert.Equal(
            ModeratedAtUtc,
            media.ModeratedAtUtc);

        Assert.Equal(
            ModeratedAtUtc,
            media.UpdatedAtUtc);

        Assert.Null(
            media.ModerationComment);
    }

    [Fact]
    public void ApproveNewMedia_Throws_WhenMediaIsAlreadyPublished()
    {
        var media =
            CreatePendingMedia();

        media.Status =
            PromptMediaStatus.Published;

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.ApproveNewMedia(
                    media,
                    ModeratorId,
                    ModeratedAtUtc));
    }

    // -------------------------------------------------------------------------
    // EN: New media rejection tests
    // RU: Тесты отклонения нового медиа
    // -------------------------------------------------------------------------

    [Fact]
    public void RejectNewMedia_RejectsPendingMediaAndRemovesCover()
    {
        var media =
            CreatePendingMedia();

        media.IsCover =
            true;

        _service.RejectNewMedia(
            media,
            ModeratorId,
            "  Изображение не соответствует промпту.  ",
            ModeratedAtUtc);

        Assert.Equal(
            PromptMediaStatus.Rejected,
            media.Status);

        Assert.False(
            media.IsCover);

        Assert.Equal(
            "Изображение не соответствует промпту.",
            media.ModerationComment);

        Assert.Equal(
            ModeratorId,
            media.ModeratedById);

        Assert.Equal(
            ModeratedAtUtc,
            media.ModeratedAtUtc);
    }

    [Fact]
    public void RejectNewMedia_Throws_WhenCommentIsTooShort()
    {
        var media =
            CreatePendingMedia();

        Assert.Throws<ArgumentException>(
            () =>
                _service.RejectNewMedia(
                    media,
                    ModeratorId,
                    "Нет",
                    ModeratedAtUtc));
    }

    // -------------------------------------------------------------------------
    // EN: Metadata approval tests
    // RU: Тесты одобрения метаданных
    // -------------------------------------------------------------------------

    [Fact]
    public void ApproveMetadata_PublishesPendingCaptionAndAltText()
    {
        var media =
            CreatePublishedMediaWithPendingMetadata();

        _service.ApproveMetadata(
            media,
            ModeratorId,
            ModeratedAtUtc);

        Assert.Equal(
            "Новая подпись",
            media.Caption);

        Assert.Equal(
            "Новый alt-текст",
            media.AltText);

        Assert.Null(
            media.PendingCaption);

        Assert.Null(
            media.PendingAltText);

        Assert.Null(
            media.MetadataSubmittedAtUtc);

        Assert.Equal(
            ModeratorId,
            media.MetadataModeratedById);

        Assert.Equal(
            ModeratedAtUtc,
            media.MetadataModeratedAtUtc);

        Assert.Null(
            media.MetadataModerationComment);
    }

    [Fact]
    public void ApproveMetadata_AllowsRemovingCaptionAndAltText()
    {
        var media =
            CreatePublishedMediaWithPendingMetadata();

        media.PendingCaption =
            string.Empty;

        media.PendingAltText =
            string.Empty;

        _service.ApproveMetadata(
            media,
            ModeratorId,
            ModeratedAtUtc);

        Assert.Equal(
            string.Empty,
            media.Caption);

        Assert.Equal(
            string.Empty,
            media.AltText);
    }

    [Fact]
    public void ApproveMetadata_Throws_WhenNoReviewIsPending()
    {
        var media =
            CreatePublishedMediaWithPendingMetadata();

        media.MetadataSubmittedAtUtc =
            null;

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.ApproveMetadata(
                    media,
                    ModeratorId,
                    ModeratedAtUtc));
    }

    // -------------------------------------------------------------------------
    // EN: Metadata rejection tests
    // RU: Тесты отклонения метаданных
    // -------------------------------------------------------------------------

    [Fact]
    public void RejectMetadata_KeepsCurrentPublicMetadata()
    {
        var media =
            CreatePublishedMediaWithPendingMetadata();

        _service.RejectMetadata(
            media,
            ModeratorId,
            "Описание содержит рекламный текст.",
            ModeratedAtUtc);

        Assert.Equal(
            "Старая подпись",
            media.Caption);

        Assert.Equal(
            "Старый alt-текст",
            media.AltText);
    }

    [Fact]
    public void RejectMetadata_RetainsProposalForAuthorCorrection()
    {
        var media =
            CreatePublishedMediaWithPendingMetadata();

        _service.RejectMetadata(
            media,
            ModeratorId,
            "Описание содержит рекламный текст.",
            ModeratedAtUtc);

        Assert.Equal(
            "Новая подпись",
            media.PendingCaption);

        Assert.Equal(
            "Новый alt-текст",
            media.PendingAltText);

        Assert.Null(
            media.MetadataSubmittedAtUtc);

        Assert.Equal(
            "Описание содержит рекламный текст.",
            media.MetadataModerationComment);

        Assert.Equal(
            ModeratorId,
            media.MetadataModeratedById);

        Assert.Equal(
            ModeratedAtUtc,
            media.MetadataModeratedAtUtc);
    }

    [Fact]
    public void RejectMetadata_Throws_WhenMediaIsNotPublished()
    {
        var media =
            CreatePublishedMediaWithPendingMetadata();

        media.Status =
            PromptMediaStatus.PendingModeration;

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.RejectMetadata(
                    media,
                    ModeratorId,
                    "Описание необходимо исправить.",
                    ModeratedAtUtc));
    }


    private static PromptMediaEntity CreatePendingMedia()
    {
        return new PromptMediaEntity
        {
            Id =
                10,

            PromptId =
                20,

            UploadedById =
                "author-user-id",

            Status =
                PromptMediaStatus.PendingModeration,

            MediaType =
                PromptMediaType.Image,

            StorageProvider =
                MediaStorageProvider.Local,

            StorageKey =
                "2026/08/test-image.jpg",

            ModerationComment =
                "Старый комментарий"
        };
    }

    private static PromptMediaEntity
        CreatePublishedMediaWithPendingMetadata()
    {
        return new PromptMediaEntity
        {
            Id =
                10,

            PromptId =
                20,

            UploadedById =
                "author-user-id",

            Status =
                PromptMediaStatus.Published,

            MediaType =
                PromptMediaType.Image,

            StorageProvider =
                MediaStorageProvider.Local,

            StorageKey =
                "2026/08/test-image.jpg",

            Caption =
                "Старая подпись",

            AltText =
                "Старый alt-текст",

            PendingCaption =
                "Новая подпись",

            PendingAltText =
                "Новый alt-текст",

            MetadataSubmittedAtUtc =
                new DateTime(
                    2026,
                    8,
                    3,
                    12,
                    0,
                    0,
                    DateTimeKind.Utc)
        };
    }
}