namespace PromptHub.Web.Services.MediaStorage;

public sealed class MediaStorageException : Exception
{
    public MediaStorageException(
        string userMessage)
        : base(userMessage)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
