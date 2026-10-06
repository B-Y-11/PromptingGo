using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneRegistrationContinuationProtector
    : IPhoneRegistrationContinuationProtector
{
    private readonly IDataProtector
        _protector;

    public PhoneRegistrationContinuationProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector =
            dataProtectionProvider.CreateProtector(
                "PromptHub.Web.PhoneRegistrationContinuation.v1");
    }

    public string Protect(
        Guid challengeId)
    {
        return _protector.Protect(
            challengeId.ToString("N"));
    }

    public bool TryUnprotect(
        string? protectedValue,
        out Guid challengeId)
    {
        challengeId =
            Guid.Empty;

        if (string.IsNullOrWhiteSpace(
                protectedValue))
        {
            return false;
        }

        try
        {
            var rawValue =
                _protector.Unprotect(
                    protectedValue);

            return Guid.TryParseExact(
                rawValue,
                "N",
                out challengeId);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
