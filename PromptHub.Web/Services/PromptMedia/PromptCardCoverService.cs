using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Prompts;

namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptCardCoverService
    : IPromptCardCoverService
{
    private readonly ApplicationDbContext _dbContext;

    public PromptCardCoverService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }


    public async Task<
        IReadOnlyDictionary<int, PromptCardCoverViewModel>>
        GetPublishedCoversAsync(
            IReadOnlyCollection<int> promptIds,
            CancellationToken cancellationToken)
    {
        var distinctPromptIds =
            promptIds
                .Where(promptId =>
                    promptId > 0)
                .Distinct()
                .ToArray();

        if (distinctPromptIds.Length == 0)
        {
            return new Dictionary<
                int,
                PromptCardCoverViewModel>();
        }

        var candidates =
            await _dbContext.PromptMediaItems
                .AsNoTracking()
                .Where(media =>
                    distinctPromptIds.Contains(
                        media.PromptId) &&
                    media.Prompt.Status ==
                        PromptStatus.Published &&
                        media.Prompt.Category.IsActive &&
                    media.Status ==
                        PromptMediaStatus.Published &&
                    media.MediaType ==
                        PromptMediaType.Image)
                .OrderBy(media =>
                    media.PromptId)
                .ThenByDescending(media =>
                    media.IsCover)
                .ThenBy(media =>
                    media.SortOrder)
                .ThenBy(media =>
                    media.CreatedAtUtc)
                .ThenBy(media =>
                    media.Id)
                .Select(media =>
                    new
                    {
                        media.PromptId,
                        media.Id,
                        media.AltText
                    })
                .ToListAsync(
                    cancellationToken);

        return candidates
            .GroupBy(media =>
                media.PromptId)
            .ToDictionary(
                group =>
                    group.Key,

                group =>
                {
                    var cover =
                        group.First();

                    return new PromptCardCoverViewModel
                    {
                        MediaId =
                            cover.Id,

                        AltText =
                            cover.AltText
                    };
                });
    }
}
