namespace PromptHub.Web.Services.PromptStats;

public interface IPromptStatsService
{
    Task<IReadOnlyDictionary<int, int>> GetVisibleCommentCountsAsync(
        IReadOnlyCollection<int> promptIds,
        CancellationToken cancellationToken);
}
