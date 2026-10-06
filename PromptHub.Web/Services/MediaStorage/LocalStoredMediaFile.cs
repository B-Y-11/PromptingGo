namespace PromptHub.Web.Services.MediaStorage;

public sealed class LocalStoredMediaFile
{

    public string StorageKey { get; init; }
        = string.Empty;

    public long FileSizeBytes { get; init; }

    public DateTime LastWriteTimeUtc { get; init; }
}
