using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.PromptStats;

public sealed class PromptStatsService : IPromptStatsService
{
    // -----------------------------------------------------------------------------
    // EN: Dependencies and construction
    // RU: Зависимости и создание сервиса
    // -----------------------------------------------------------------------------

    private readonly ApplicationDbContext _dbContext;

    public PromptStatsService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }


    public async Task<IReadOnlyDictionary<int, int>> GetVisibleCommentCountsAsync(
        IReadOnlyCollection<int> promptIds,
        CancellationToken cancellationToken)
    {
        if (promptIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var distinctPromptIds = promptIds
            .Distinct()
            .ToArray();

        if (distinctPromptIds.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _dbContext.PromptComments
            .AsNoTracking()
            .Where(comment =>
                distinctPromptIds.Contains(comment.PromptId) &&
                comment.Status == PromptCommentStatus.Visible)
            .GroupBy(comment =>
                comment.PromptId)
            .Select(group =>
                new
                {
                    PromptId = group.Key,
                    Count = group.Count()
                })
            .ToDictionaryAsync(
                item => item.PromptId,
                item => item.Count,
                cancellationToken);
    }
}
