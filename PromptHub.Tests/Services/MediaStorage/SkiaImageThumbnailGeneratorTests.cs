using SkiaSharp;
using PromptHub.Web.Services.MediaStorage;

namespace PromptHub.Tests.Services.MediaStorage;

public sealed class SkiaImageThumbnailGeneratorTests
{
    private readonly SkiaImageThumbnailGenerator
        _generator =
            new();

    // -------------------------------------------------------------------------
    // EN: Source image dimension limits
    // RU: Ограничения размеров исходного изображения
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_Throws_WhenSourceWidthExceedsLimit()
    {
        await using var source =
            CreatePngStream(
                width: 100,
                height: 80);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<MediaStorageException>(
            () =>
                _generator.GenerateAsync(
                    source,
                    destination,
                    maxSourceWidth: 99,
                    maxSourceHeight: 100,
                    maxSourcePixelCount: 20_000,
                    thumbnailMaxWidth: 50,
                    thumbnailMaxHeight: 50,
                    quality: 82,
                    CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenSourceHeightExceedsLimit()
    {
        await using var source =
            CreatePngStream(
                width: 80,
                height: 100);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<MediaStorageException>(
            () =>
                _generator.GenerateAsync(
                    source,
                    destination,
                    maxSourceWidth: 100,
                    maxSourceHeight: 99,
                    maxSourcePixelCount: 20_000,
                    thumbnailMaxWidth: 50,
                    thumbnailMaxHeight: 50,
                    quality: 82,
                    CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenPixelCountExceedsLimit()
    {
        await using var source =
            CreatePngStream(
                width: 100,
                height: 80);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<MediaStorageException>(
            () =>
                _generator.GenerateAsync(
                    source,
                    destination,
                    maxSourceWidth: 200,
                    maxSourceHeight: 200,
                    maxSourcePixelCount: 7_999,
                    thumbnailMaxWidth: 50,
                    thumbnailMaxHeight: 50,
                    quality: 82,
                    CancellationToken.None));
    }

    // -------------------------------------------------------------------------
    // EN: Valid proportional thumbnail generation
    // RU: Корректное пропорциональное создание миниатюры
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GenerateAsync_CreatesProportionalThumbnail()
    {
        await using var source =
            CreatePngStream(
                width: 100,
                height: 80);

        await using var destination =
            new MemoryStream();

        var result =
            await _generator.GenerateAsync(
                source,
                destination,
                maxSourceWidth: 200,
                maxSourceHeight: 200,
                maxSourcePixelCount: 20_000,
                thumbnailMaxWidth: 50,
                thumbnailMaxHeight: 50,
                quality: 82,
                CancellationToken.None);

        Assert.Equal(
            100,
            result.SourceWidth);

        Assert.Equal(
            80,
            result.SourceHeight);

        Assert.Equal(
            50,
            result.Width);

        Assert.Equal(
            40,
            result.Height);

        Assert.Equal(
            "image/webp",
            result.ContentType);

        Assert.True(
            result.FileSizeBytes > 0);

        Assert.True(
            destination.Length > 0);
    }


    private static MemoryStream CreatePngStream(
        int width,
        int height)
    {
        using var bitmap =
            new SKBitmap(
                new SKImageInfo(
                    width,
                    height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul));

        bitmap.Erase(
            SKColors.White);

        using var image =
            SKImage.FromBitmap(
                bitmap);

        using var data =
            image.Encode(
                SKEncodedImageFormat.Png,
                quality: 100);

        if (data is null)
        {
            throw new InvalidOperationException(
                "Не удалось создать тестовое PNG-изображение.");
        }

        var stream =
            new MemoryStream();

        data.SaveTo(
            stream);

        stream.Position =
            0;

        return stream;
    }
}