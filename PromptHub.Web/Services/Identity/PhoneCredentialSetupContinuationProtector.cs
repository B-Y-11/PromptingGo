using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneCredentialSetupContinuationProtector
    : IPhoneCredentialSetupContinuationProtector
{
    private readonly IDataProtector
        _protector;

    public PhoneCredentialSetupContinuationProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector =
            dataProtectionProvider.CreateProtector(
                "PromptHub.Web.PhoneCredentialSetupContinuation.v1");
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
            var value =
                _protector.Unprotect(
                    protectedValue);

            return Guid.TryParseExact(
                value,
                "N",
                out challengeId);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
