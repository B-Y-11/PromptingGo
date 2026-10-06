namespace PromptHub.Web.Services.MediaStorage;

public interface IImageThumbnailGenerator
{
    // -------------------------------------------------------------------------
    // EN: Safe image decoding and thumbnail generation
    // RU: Безопасное декодирование и создание миниатюры
    // -------------------------------------------------------------------------

    Task<GeneratedThumbnail> GenerateAsync(
        Stream source,
        Stream destination,
        int maxSourceWidth,
        int maxSourceHeight,
        long maxSourcePixelCount,
        int thumbnailMaxWidth,
        int thumbnailMaxHeight,
        int quality,
        CancellationToken cancellationToken);
}
