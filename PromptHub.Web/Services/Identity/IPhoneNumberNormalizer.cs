namespace PromptHub.Web.Services.Identity;

public interface IPhoneNumberNormalizer
{
    bool TryNormalize(
        string? phoneNumber,
        out string normalizedPhoneNumber);
}
