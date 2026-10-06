using SkiaSharp;

namespace PromptHub.Web.Services.MediaStorage;

public sealed class SkiaImageThumbnailGenerator
    : IImageThumbnailGenerator
{

    public async Task<GeneratedThumbnail> GenerateAsync(
     Stream source,
     Stream destination,
     int maxSourceWidth,
     int maxSourceHeight,
     long maxSourcePixelCount,
     int thumbnailMaxWidth,
     int thumbnailMaxHeight,
     int quality,
     CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (maxSourceWidth <= 0 ||
            maxSourceHeight <= 0 ||
            maxSourcePixelCount <= 0)
        {
            throw new MediaStorageException(
                "Некорректно настроены ограничения исходного изображения.");
        }

        if (thumbnailMaxWidth <= 0 ||
            thumbnailMaxHeight <= 0)
        {
            throw new MediaStorageException(
                "Некорректно настроен размер миниатюры.");
        }

        var normalizedQuality =
            Math.Clamp(
                quality,
                1,
                100);

        try
        {
            using var codec =
                SKCodec.Create(source);

            if (codec is null)
            {
                throw new MediaStorageException(
                    "Не удалось прочитать изображение.");
            }

            var sourceInfo =
    codec.Info;

            if (sourceInfo.Width <= 0 ||
                sourceInfo.Height <= 0)
            {
                throw new MediaStorageException(
                    "Изображение имеет некорректный размер.");
            }

            // -------------------------------------------------------------------------
            // EN: Account for EXIF rotation before applying width and height limits.
            // RU: Учитываем EXIF-поворот перед проверкой ширины и высоты.
            // -------------------------------------------------------------------------

            var swapsDimensions =
                codec.EncodedOrigin is
                    SKEncodedOrigin.LeftTop or
                    SKEncodedOrigin.RightTop or
                    SKEncodedOrigin.RightBottom or
                    SKEncodedOrigin.LeftBottom;

            var orientedSourceWidth =
                swapsDimensions
                    ? sourceInfo.Height
                    : sourceInfo.Width;

            var orientedSourceHeight =
                swapsDimensions
                    ? sourceInfo.Width
                    : sourceInfo.Height;

            // -------------------------------------------------------------------------
            // EN: Validate dimensions before allocating a full decoded pixel buffer.
            // RU: Проверяем размеры до выделения полного пиксельного буфера.
            // -------------------------------------------------------------------------

            ValidateSourceImageDimensions(
                orientedSourceWidth,
                orientedSourceHeight,
                maxSourceWidth,
                maxSourceHeight,
                maxSourcePixelCount);

            var decodeInfo =
                new SKImageInfo(sourceInfo.Width,
                    sourceInfo.Height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul);

            using var decodedBitmap =
                new SKBitmap(
                    decodeInfo);

            var decodeResult =
                codec.GetPixels(
                    decodeInfo,
                    decodedBitmap.GetPixels());

            if (decodeResult is not
                (SKCodecResult.Success or
                 SKCodecResult.IncompleteInput))
            {
                throw new MediaStorageException(
                    "Не удалось декодировать изображение.");
            }

            using var orientedBitmap =
                CreateOrientedBitmap(
                    decodedBitmap,
                    codec.EncodedOrigin);

            var thumbnailSize =
                CalculateThumbnailSize(
                    orientedBitmap.Width,
                    orientedBitmap.Height,
                    thumbnailMaxWidth,
                    thumbnailMaxHeight);

            var thumbnailInfo =
                new SKImageInfo(
                    thumbnailSize.Width,
                    thumbnailSize.Height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul);

            using var thumbnailBitmap =
                new SKBitmap(
                    thumbnailInfo);

            var sampling =
                new SKSamplingOptions(
                    SKCubicResampler.Mitchell);

            var resized =
                orientedBitmap.ScalePixels(
                    thumbnailBitmap,
                    sampling);

            if (!resized)
            {
                throw new MediaStorageException(
                    "Не удалось уменьшить изображение.");
            }

            using var thumbnailImage =
                SKImage.FromBitmap(
                    thumbnailBitmap);

            using var encodedData =
                thumbnailImage.Encode(
                    SKEncodedImageFormat.Webp,
                    normalizedQuality);

            if (encodedData is null ||
                encodedData.Size <= 0)
            {
                throw new MediaStorageException(
                    "Не удалось сохранить миниатюру.");
            }

            encodedData.SaveTo(
                destination);

            await destination.FlushAsync(
                cancellationToken);

            return new GeneratedThumbnail
            {
                SourceWidth =
                    orientedBitmap.Width,

                SourceHeight =
                    orientedBitmap.Height,

                Width =
                    thumbnailSize.Width,

                Height =
                    thumbnailSize.Height,

                ContentType =
                    "image/webp",

                FileSizeBytes =
                    checked((long)encodedData.Size)
            };
        }
        catch (MediaStorageException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new MediaStorageException(
                $"Не удалось создать миниатюру изображения: {exception.Message}");
        }
    }


    private static SKBitmap CreateOrientedBitmap(
        SKBitmap source,
        SKEncodedOrigin orientation)
    {
        var swapsDimensions =
            orientation is
                SKEncodedOrigin.LeftTop or
                SKEncodedOrigin.RightTop or
                SKEncodedOrigin.RightBottom or
                SKEncodedOrigin.LeftBottom;

        var destinationWidth =
            swapsDimensions
                ? source.Height
                : source.Width;

        var destinationHeight =
            swapsDimensions
                ? source.Width
                : source.Height;

        var destination =
            new SKBitmap(
                new SKImageInfo(
                    destinationWidth,
                    destinationHeight,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul));

        using var canvas =
            new SKCanvas(
                destination);

        canvas.Clear(
            SKColors.Transparent);

        var width =
            source.Width;

        var height =
            source.Height;

        var matrix =
            orientation switch
            {
                SKEncodedOrigin.TopRight =>
                    new SKMatrix(
                        -1, 0, width,
                         0, 1, 0,
                         0, 0, 1),

                SKEncodedOrigin.BottomRight =>
                    new SKMatrix(
                        -1, 0, width,
                         0, -1, height,
                         0, 0, 1),

                SKEncodedOrigin.BottomLeft =>
                    new SKMatrix(
                         1, 0, 0,
                         0, -1, height,
                         0, 0, 1),

                SKEncodedOrigin.LeftTop =>
                    new SKMatrix(
                         0, 1, 0,
                         1, 0, 0,
                         0, 0, 1),

                SKEncodedOrigin.RightTop =>
                    new SKMatrix(
                         0, -1, height,
                         1, 0, 0,
                         0, 0, 1),

                SKEncodedOrigin.RightBottom =>
                    new SKMatrix(
                         0, -1, height,
                        -1, 0, width,
                         0, 0, 1),

                SKEncodedOrigin.LeftBottom =>
                    new SKMatrix(
                         0, 1, 0,
                        -1, 0, width,
                         0, 0, 1),

                _ =>
                    SKMatrix.Identity
            };

        canvas.SetMatrix(
            matrix);

        canvas.DrawBitmap(
            source,
            0,
            0,
            SKSamplingOptions.Default);

        canvas.Flush();

        return destination;
    }


    // -----------------------------------------------------------------------------
    // EN: Source image dimension and decoded pixel-count validation
    // RU: Проверка размеров и количества декодируемых пикселей
    // -----------------------------------------------------------------------------

    private static void ValidateSourceImageDimensions(
        int sourceWidth,
        int sourceHeight,
        int maxSourceWidth,
        int maxSourceHeight,
        long maxSourcePixelCount)
    {
        if (sourceWidth > maxSourceWidth)
        {
            throw new MediaStorageException(
                $"Ширина изображения не должна превышать " +
                $"{maxSourceWidth:N0} пикселей.");
        }

        if (sourceHeight > maxSourceHeight)
        {
            throw new MediaStorageException(
                $"Высота изображения не должна превышать " +
                $"{maxSourceHeight:N0} пикселей.");
        }

        var sourcePixelCount =
            checked(
                (long)sourceWidth *
                sourceHeight);

        if (sourcePixelCount >
            maxSourcePixelCount)
        {
            throw new MediaStorageException(
                $"Изображение не должно содержать больше " +
                $"{maxSourcePixelCount:N0} пикселей.");
        }
    }





    private static ThumbnailSize CalculateThumbnailSize(
        int sourceWidth,
        int sourceHeight,
        int maxWidth,
        int maxHeight)
    {
        var scale =
            Math.Min(
                1d,
                Math.Min(
                    maxWidth /
                    (double)sourceWidth,

                    maxHeight /
                    (double)sourceHeight));

        return new ThumbnailSize(
            Math.Max(
                1,
                (int)Math.Round(
                    sourceWidth * scale)),

            Math.Max(
                1,
                (int)Math.Round(
                    sourceHeight * scale)));
    }

    private readonly record struct ThumbnailSize(
        int Width,
        int Height);
}
