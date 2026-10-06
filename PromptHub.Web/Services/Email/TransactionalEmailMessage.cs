namespace PromptHub.Web.Services.Email;

public sealed record TransactionalEmailMessage(
    string RecipientEmail,
    string Subject,
    string HtmlBody,
    string PlainTextBody,
    IReadOnlyCollection<string>? Tags = null);
