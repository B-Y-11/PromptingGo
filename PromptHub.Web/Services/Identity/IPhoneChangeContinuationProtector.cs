namespace PromptHub.Web.Services.Identity;

public interface IPhoneChangeContinuationProtector
{
    string Protect(
        PhoneChangeAuthorization authorization);

    bool TryUnprotect(
        string? protectedValue,
        out PhoneChangeAuthorization? authorization);
}

public sealed record PhoneChangeAuthorization(
    string UserId,
    string CurrentPhoneNumber,
    string SecurityStamp);
