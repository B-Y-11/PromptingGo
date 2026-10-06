using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.Identity;

public interface IPhoneOtpService
{
    Task<PhoneOtpRequestResult> RequestAsync(
        string? phoneNumber,
        PhoneOtpPurpose purpose,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<PhoneOtpVerificationResult> VerifyAsync(
        Guid challengeId,
        PhoneOtpPurpose purpose,
        string? code,
        CancellationToken cancellationToken = default);
}
