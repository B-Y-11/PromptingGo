namespace PromptHub.Web.Services.MediaStorage;

public sealed class MediaStorageOptions
{
    public const string SectionName =
        "MediaStorage";


    public string RootFolder { get; set; }
        = "App_Data/prompt-media";

    public long MaxImageSizeBytes { get; set; }
        = 5 * 1024 * 1024;

    public int MaxImagesPerPrompt { get; set; }
        = 6;


    public string AvatarRootFolder { get; set; }
        = "App_Data/profile-avatars";

    public long MaxAvatarImageSizeBytes { get; set; }
        = 2 * 1024 * 1024;

    public int AvatarMaxWidth { get; set; }
        = 512;

    public int AvatarMaxHeight { get; set; }
        = 512;

    public int AvatarWebpQuality { get; set; }
        = 85;


    public int MaxImageWidth { get; set; }
        = 12_000;

    public int MaxImageHeight { get; set; }
        = 12_000;

    public long MaxImagePixelCount { get; set; }
        = 40_000_000;


    public int ThumbnailMaxWidth { get; set; }
        = 960;

    public int ThumbnailMaxHeight { get; set; }
        = 720;

    public int ThumbnailWebpQuality { get; set; }
        = 82;


    public string[] AllowedImageContentTypes { get; set; }
        =
        [
            "image/jpeg",
            "image/png",
            "image/webp"
        ];

    public string[] AllowedImageExtensions { get; set; }
        =
        [
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        ];
}
