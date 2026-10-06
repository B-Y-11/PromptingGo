namespace PromptHub.Web.Services.PromptMedia;

public interface IPromptMediaCleanupService
{
    Task<PromptMediaCleanupResult> CleanupAsync(
        bool dryRun,
        CancellationToken cancellationToken);
}
