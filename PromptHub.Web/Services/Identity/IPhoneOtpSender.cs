namespace PromptHub.Web.Services.Identity;

public interface IPhoneOtpSender
{
    Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default);
}
