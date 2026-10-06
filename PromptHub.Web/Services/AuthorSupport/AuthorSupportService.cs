using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.AuthorSupport;

namespace PromptHub.Web.Services.AuthorSupport;

public sealed class AuthorSupportService
    : IAuthorSupportService
{
    private readonly ApplicationDbContext
        _dbContext;

    public AuthorSupportService(
        ApplicationDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<AuthorSupportViewModel?> GetPublicAsync(
        string userId,
        string authorName,
        CancellationToken cancellationToken = default)
    {
        var methods =
            await _dbContext
                .AuthorSupportMethods
                .AsNoTracking()
                .Where(method =>
                    method.UserId == userId &&
                    method.IsEnabled)
                .Select(method =>
                    new
                    {
                        method.Provider,
                        method.ExternalReference
                    })
                .ToListAsync(
                    cancellationToken);

        var cloudTips =
            methods.SingleOrDefault(method =>
                method.Provider ==
                AuthorSupportProvider.CloudTips);

        var yooMoney =
            methods.SingleOrDefault(method =>
                method.Provider ==
                AuthorSupportProvider.YooMoney);

        if (cloudTips is null &&
            yooMoney is null)
        {
            return null;
        }

        return new AuthorSupportViewModel
        {
            AuthorName =
                authorName,

            CloudTipsUrl =
                cloudTips is null
                    ? null
                    : AuthorSupportReferenceParser
                        .BuildCloudTipsUrl(
                            cloudTips.ExternalReference),

            YooMoneyWallet =
                yooMoney?.ExternalReference
        };
    }
}
