namespace PromptHub.Web.Services.MediaStorage;

public sealed class GeneratedThumbnail
{

    public int SourceWidth { get; set; }

    public int SourceHeight { get; set; }


    public int Width { get; set; }

    public int Height { get; set; }

    public string ContentType { get; set; }
        = "image/webp";

    public long FileSizeBytes { get; set; }
}
