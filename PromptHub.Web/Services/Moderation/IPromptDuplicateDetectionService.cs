namespace PromptHub.Web.Services.Moderation;

public interface IPromptDuplicateDetectionService
{
    Task<IReadOnlyList<PromptDuplicateMatch>>
        FindSimilarPublishedAsync(
            int promptId,
            CancellationToken cancellationToken = default);
}
