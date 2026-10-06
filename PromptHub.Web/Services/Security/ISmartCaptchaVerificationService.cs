using System.Net;

namespace PromptHub.Web.Services.Security;

public interface ISmartCaptchaVerificationService
{
    Task<bool> VerifyAsync(
        string? token,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken = default);
}
