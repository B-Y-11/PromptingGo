namespace PromptHub.Web.Services.Identity;

public interface IPhoneRegistrationContinuationProtector
{
    string Protect(
        Guid challengeId);

    bool TryUnprotect(
        string? protectedValue,
        out Guid challengeId);
}
