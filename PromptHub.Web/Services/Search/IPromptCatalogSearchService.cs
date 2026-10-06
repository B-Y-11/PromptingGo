using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Services.Search;

public interface IPromptCatalogSearchService
{
    Task<IQueryable<Prompt>> ApplySearchAsync(
        IQueryable<Prompt> source,
        string search,
        CancellationToken cancellationToken = default);
}
