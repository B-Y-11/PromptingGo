using PromptHub.Web.Models.ViewModels.AuthorSupport;

namespace PromptHub.Web.Services.AuthorSupport;

public interface IAuthorSupportService
{
    Task<AuthorSupportViewModel?> GetPublicAsync(
        string userId,
        string authorName,
        CancellationToken cancellationToken = default);
}
