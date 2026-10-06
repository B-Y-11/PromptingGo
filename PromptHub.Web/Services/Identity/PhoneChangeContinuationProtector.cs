using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneChangeContinuationProtector
    : IPhoneChangeContinuationProtector
{
    private static readonly TimeSpan Lifetime =
        TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector
        _protector;

    public PhoneChangeContinuationProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector =
            dataProtectionProvider
                .CreateProtector(
                    "PromptHub.Web.PhoneChangeAuthorization.v1")
                .ToTimeLimitedDataProtector();
    }

    public string Protect(
        PhoneChangeAuthorization authorization)
    {
        var json =
            JsonSerializer.Serialize(
                authorization);

        return _protector.Protect(
            json,
            Lifetime);
    }

    public bool TryUnprotect(
        string? protectedValue,
        out PhoneChangeAuthorization? authorization)
    {
        authorization =
            null;

        if (string.IsNullOrWhiteSpace(
                protectedValue))
        {
            return false;
        }

        try
        {
            var json =
                _protector.Unprotect(
                    protectedValue,
                    out _);

            authorization =
                JsonSerializer.Deserialize<
                    PhoneChangeAuthorization>(
                    json);

            return authorization is not null &&
                   !string.IsNullOrWhiteSpace(
                       authorization.UserId) &&
                   !string.IsNullOrWhiteSpace(
                       authorization.CurrentPhoneNumber) &&
                   !string.IsNullOrWhiteSpace(
                       authorization.SecurityStamp);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
