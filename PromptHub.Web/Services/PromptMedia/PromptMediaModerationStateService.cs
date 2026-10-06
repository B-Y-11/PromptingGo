using PromptHub.Web.Models.Enums;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaModerationStateService
    : IPromptMediaModerationStateService
{

    public void ApproveNewMedia(
        PromptMediaEntity media,
        string moderatorId,
        DateTime moderatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            media);

        ValidateModeratorId(
            moderatorId);

        if (media.Status !=
            PromptMediaStatus.PendingModeration)
        {
            throw new InvalidOperationException(
                "Одобрить можно только медиа, ожидающее модерации.");
        }

        media.Status =
            PromptMediaStatus.Published;

        media.ModeratedAtUtc =
            moderatedAtUtc;

        media.ModeratedById =
            moderatorId;

        media.ModerationComment =
            null;

        media.UpdatedAtUtc =
            moderatedAtUtc;
    }


    public void RejectNewMedia(
        PromptMediaEntity media,
        string moderatorId,
        string moderationComment,
        DateTime moderatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            media);

        ValidateModeratorId(
            moderatorId);

        var normalizedComment =
            ValidateModerationComment(
                moderationComment);

        if (media.Status !=
            PromptMediaStatus.PendingModeration)
        {
            throw new InvalidOperationException(
                "Отклонить можно только медиа, ожидающее модерации.");
        }

        media.Status =
            PromptMediaStatus.Rejected;

        media.IsCover =
            false;

        media.ModeratedAtUtc =
            moderatedAtUtc;

        media.ModeratedById =
            moderatorId;

        media.ModerationComment =
            normalizedComment;

        media.UpdatedAtUtc =
            moderatedAtUtc;
    }


    public void ApproveMetadata(
        PromptMediaEntity media,
        string moderatorId,
        DateTime moderatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            media);

        ValidateModeratorId(
            moderatorId);

        EnsurePendingMetadataReview(
            media);

        media.Caption =
            media.PendingCaption
            ?? string.Empty;

        media.AltText =
            media.PendingAltText
            ?? string.Empty;

        media.PendingCaption =
            null;

        media.PendingAltText =
            null;

        media.MetadataSubmittedAtUtc =
            null;

        media.MetadataModeratedAtUtc =
            moderatedAtUtc;

        media.MetadataModeratedById =
            moderatorId;

        media.MetadataModerationComment =
            null;

        media.UpdatedAtUtc =
            moderatedAtUtc;
    }


    public void RejectMetadata(
        PromptMediaEntity media,
        string moderatorId,
        string moderationComment,
        DateTime moderatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            media);

        ValidateModeratorId(
            moderatorId);

        var normalizedComment =
            ValidateModerationComment(
                moderationComment);

        EnsurePendingMetadataReview(
            media);

        // ---------------------------------------------------------------------
        // EN: Keep current public metadata and retain the rejected proposal so
        //     the author can correct and resubmit it.
        // RU: Сохраняем текущие публичные данные и отклонённое предложение,
        //     чтобы автор мог исправить и повторно отправить его.
        // ---------------------------------------------------------------------

        media.MetadataSubmittedAtUtc =
            null;

        media.MetadataModeratedAtUtc =
            moderatedAtUtc;

        media.MetadataModeratedById =
            moderatorId;

        media.MetadataModerationComment =
            normalizedComment;

        media.UpdatedAtUtc =
            moderatedAtUtc;
    }


    private static void EnsurePendingMetadataReview(
        PromptMediaEntity media)
    {
        if (media.Status !=
                PromptMediaStatus.Published ||
            !media.MetadataSubmittedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "У медиа нет изменений метаданных, ожидающих модерации.");
        }
    }

    private static void ValidateModeratorId(
        string moderatorId)
    {
        if (string.IsNullOrWhiteSpace(
                moderatorId))
        {
            throw new ArgumentException(
                "Не указан идентификатор модератора.",
                nameof(moderatorId));
        }
    }

    private static string ValidateModerationComment(
        string moderationComment)
    {
        var normalizedComment =
            moderationComment?.Trim()
            ?? string.Empty;

        if (normalizedComment.Length < 5)
        {
            throw new ArgumentException(
                "Причина отклонения должна содержать минимум 5 символов.",
                nameof(moderationComment));
        }

        if (normalizedComment.Length > 1000)
        {
            throw new ArgumentException(
                "Причина отклонения не должна превышать 1000 символов.",
                nameof(moderationComment));
        }

        return normalizedComment;
    }
}
