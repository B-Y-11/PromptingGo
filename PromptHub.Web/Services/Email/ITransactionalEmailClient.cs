namespace PromptHub.Web.Services.Email;

public interface ITransactionalEmailClient
{
    Task SendAsync(
        TransactionalEmailMessage message,
        CancellationToken cancellationToken = default);
}
