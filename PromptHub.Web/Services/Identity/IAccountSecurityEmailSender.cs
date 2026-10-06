using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Services.Identity;

public interface IAccountSecurityEmailSender
{
    Task SendCodeAsync(
        ApplicationUser user,
        string email,
        string code,
        CancellationToken cancellationToken = default);
}
