using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.MediaStorage;

public sealed class StoredMediaFile
{

    public PromptMediaType MediaType { get; set; }

    public MediaStorageProvider StorageProvider { get; set; }

    public string OriginalFileName { get; set; }
        = string.Empty;

    public string ContentType { get; set; }
        = string.Empty;

    public long FileSizeBytes { get; set; }

    public string StorageKey { get; set; }
        = string.Empty;

    public string? PublicUrl { get; set; }

    public int OriginalWidth { get; set; }

    public int OriginalHeight { get; set; }


    public string? ThumbnailUrl { get; set; }

    public string ThumbnailStorageKey { get; set; }
        = string.Empty;

    public string ThumbnailContentType { get; set; }
        = "image/webp";

    public long ThumbnailFileSizeBytes { get; set; }

    public int ThumbnailWidth { get; set; }

    public int ThumbnailHeight { get; set; }
}
