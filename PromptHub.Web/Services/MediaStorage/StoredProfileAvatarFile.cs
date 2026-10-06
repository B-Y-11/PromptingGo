namespace PromptHub.Web.Services.MediaStorage;

public sealed class StoredProfileAvatarFile
{

    public string StorageKey { get; set; }
        = string.Empty;

    public string ContentType { get; set; }
        = "image/webp";

    public long FileSizeBytes { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}
