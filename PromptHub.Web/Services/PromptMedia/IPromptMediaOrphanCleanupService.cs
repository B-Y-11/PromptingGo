namespace PromptHub.Web.Services.PromptMedia;

public interface IPromptMediaOrphanCleanupService
{
    Task<PromptMediaOrphanCleanupResult> CleanupAsync(
        bool dryRun,
        CancellationToken cancellationToken);
}
