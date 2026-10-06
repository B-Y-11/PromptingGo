using PromptHub.Web.Models.Enums;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaArrangementService
    : IPromptMediaArrangementService
{

    public PromptMediaCoverUpdateResult SetCover(
    IEnumerable<PromptMediaEntity> mediaItems,
    int selectedMediaId,
    DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            mediaItems);

        var mediaArray =
            mediaItems.ToArray();

        var selectedMedia =
            mediaArray.SingleOrDefault(media =>
                media.Id == selectedMediaId);

        if (selectedMedia is null)
        {
            return PromptMediaCoverUpdateResult.MediaNotFound;
        }

        var hasChanges =
            false;

        foreach (var media in mediaArray)
        {
            var shouldBeCover =
                media.Id == selectedMediaId;

            if (media.IsCover ==
                shouldBeCover)
            {
                continue;
            }

            media.IsCover =
                shouldBeCover;

            media.UpdatedAtUtc =
                updatedAtUtc;

            hasChanges =
                true;
        }

        return hasChanges
            ? PromptMediaCoverUpdateResult.Applied
            : PromptMediaCoverUpdateResult.NoChanges;
    }


    public PromptMediaOrderUpdateResult ApplyOrder(
    IEnumerable<PromptMediaEntity> mediaItems,
    IReadOnlyList<int> orderedMediaIds,
    DateTime updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(
            mediaItems);

        ArgumentNullException.ThrowIfNull(
            orderedMediaIds);

        var mediaArray =
    mediaItems.ToArray();

        var activeMedia =
            mediaArray
                .Where(media =>
                    media.Status !=
                        PromptMediaStatus.Archived &&
                    media.Status !=
                        PromptMediaStatus.Rejected)
                .ToArray();

        if (orderedMediaIds.Count < 2 ||
            orderedMediaIds.Any(mediaId =>
                mediaId <= 0))
        {
            return PromptMediaOrderUpdateResult.InvalidOrder;
        }

        var distinctSubmittedIds =
            orderedMediaIds
                .Distinct()
                .ToArray();

        if (distinctSubmittedIds.Length !=
            orderedMediaIds.Count)
        {
            return PromptMediaOrderUpdateResult.InvalidOrder;
        }

        if (activeMedia.Length !=
            orderedMediaIds.Count)
        {
            return PromptMediaOrderUpdateResult.InvalidOrder;
        }

        var activeMediaIds =
            activeMedia
                .Select(media =>
                    media.Id)
                .ToHashSet();

        if (!activeMediaIds.SetEquals(
                orderedMediaIds))
        {
            return PromptMediaOrderUpdateResult.InvalidOrder;
        }

        var mediaById =
            activeMedia.ToDictionary(
                media =>
                    media.Id);

        var hasChanges =
            false;

        for (var index = 0;
             index < orderedMediaIds.Count;
             index++)
        {
            var media =
                mediaById[
                    orderedMediaIds[index]];

            if (media.SortOrder ==
                index)
            {
                continue;
            }

            media.SortOrder =
                index;

            media.UpdatedAtUtc =
                updatedAtUtc;

            hasChanges =
                true;
        }

        return hasChanges
            ? PromptMediaOrderUpdateResult.Applied
            : PromptMediaOrderUpdateResult.NoChanges;
    }
}
