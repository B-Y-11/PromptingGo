using PromptHub.Web.Models.ViewModels.Prompts;

namespace PromptHub.Web.Services.PromptMedia;

public interface IPromptCardCoverService
{
    Task<IReadOnlyDictionary<int, PromptCardCoverViewModel>>
        GetPublishedCoversAsync(
            IReadOnlyCollection<int> promptIds,
            CancellationToken cancellationToken);
}
