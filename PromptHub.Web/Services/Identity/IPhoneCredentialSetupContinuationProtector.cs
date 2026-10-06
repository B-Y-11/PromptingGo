namespace PromptHub.Web.Services.Identity;

public interface IPhoneCredentialSetupContinuationProtector
{
    string Protect(
        Guid challengeId);

    bool TryUnprotect(
        string? protectedValue,
        out Guid challengeId);
}
