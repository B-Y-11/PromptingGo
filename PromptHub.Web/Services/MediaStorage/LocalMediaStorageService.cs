using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PromptHub.Web.Models.Enums;
using System.Runtime.CompilerServices;

namespace PromptHub.Web.Services.MediaStorage;

public sealed class LocalMediaStorageService
    : IMediaStorageService,
      ILocalMediaStorageCatalog
{
    private readonly IWebHostEnvironment _environment;

    private readonly MediaStorageOptions _options;

    private readonly IImageThumbnailGenerator
    _thumbnailGenerator;

    public LocalMediaStorageService(
        IWebHostEnvironment environment,
        IOptions<MediaStorageOptions> options,
        IImageThumbnailGenerator thumbnailGenerator)
    {
        _environment = environment;
        _options = options.Value;
        _thumbnailGenerator = thumbnailGenerator;
    }


    public async Task<StoredMediaFile> SavePromptImageAsync(
    IFormFile file,
    CancellationToken cancellationToken)
    {
        ValidateImageFile(
            file,
            _options.MaxImageSizeBytes);

        var extension =
            GetSafeImageExtension(
                file);

        await ValidateImageSignatureAsync(
            file,
            extension,
            cancellationToken);

        var now =
            DateTime.UtcNow;

        var fileIdentifier =
            Guid.NewGuid()
                .ToString("N");

        var originalFileName =
            $"{fileIdentifier}{extension}";

        var thumbnailFileName =
            $"{fileIdentifier}.thumb.webp";

        var relativeDirectory =
            $"{now:yyyy}/{now:MM}";

        var storageKey =
            $"{relativeDirectory}/{originalFileName}";

        var thumbnailStorageKey =
            $"{relativeDirectory}/{thumbnailFileName}";

        var physicalDirectory =
            Path.Combine(
                GetStorageRootPath(),
                now.ToString("yyyy"),
                now.ToString("MM"));

        Directory.CreateDirectory(
            physicalDirectory);

        var originalPhysicalPath =
            Path.Combine(
                physicalDirectory,
                originalFileName);

        var thumbnailPhysicalPath =
            Path.Combine(
                physicalDirectory,
                thumbnailFileName);

        try
        {
            // ---------------------------------------------------------------------
            // EN: Save the untouched private original.
            // RU: Сохраняем неизменённый приватный оригинал.
            // ---------------------------------------------------------------------

            await using (
                var originalStream =
                    new FileStream(
                        originalPhysicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
            {
                await file.CopyToAsync(
                    originalStream,
                    cancellationToken);
            }

            // ---------------------------------------------------------------------
            // EN: Generate a smaller WebP representation for lists and previews.
            // RU: Создаём уменьшенную WebP-версию для списков и предпросмотров.
            // ---------------------------------------------------------------------

            GeneratedThumbnail thumbnail;

            await using (
                var sourceStream =
                    new FileStream(
                        originalPhysicalPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 81920,
                        useAsync: true))
            await using (
                var thumbnailStream =
                    new FileStream(
                        thumbnailPhysicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
            {
                thumbnail =
                    await _thumbnailGenerator.GenerateAsync(
                        sourceStream,
                        thumbnailStream,
                        _options.MaxImageWidth,
                        _options.MaxImageHeight,
                        _options.MaxImagePixelCount,
                        _options.ThumbnailMaxWidth,
                        _options.ThumbnailMaxHeight,
                        _options.ThumbnailWebpQuality,
                        cancellationToken);
            }

            return new StoredMediaFile
            {
                MediaType =
                    PromptMediaType.Image,

                StorageProvider =
                    MediaStorageProvider.Local,

                OriginalFileName =
                    Path.GetFileName(
                        file.FileName),

                ContentType =
                    NormalizeContentType(
                        file.ContentType),

                FileSizeBytes =
                    file.Length,

                StorageKey =
                    storageKey,

                PublicUrl =
                    null,

                OriginalWidth =
                    thumbnail.SourceWidth,

                OriginalHeight =
                    thumbnail.SourceHeight,

                ThumbnailUrl =
                    null,

                ThumbnailStorageKey =
                    thumbnailStorageKey,

                ThumbnailContentType =
                    thumbnail.ContentType,

                ThumbnailFileSizeBytes =
                    thumbnail.FileSizeBytes,

                ThumbnailWidth =
                    thumbnail.Width,

                ThumbnailHeight =
                    thumbnail.Height
            };
        }
        catch (OperationCanceledException)
        {
            DeleteFileIfExists(
                thumbnailPhysicalPath);

            DeleteFileIfExists(
                originalPhysicalPath);

            throw;
        }
        catch (MediaStorageException)
        {
            DeleteFileIfExists(
                thumbnailPhysicalPath);

            DeleteFileIfExists(
                originalPhysicalPath);

            throw;
        }
        catch (Exception exception)
        {
            DeleteFileIfExists(
                thumbnailPhysicalPath);

            DeleteFileIfExists(
                originalPhysicalPath);

            throw new MediaStorageException(
                $"Не удалось сохранить изображение: {exception.Message}");
        }
    }



    public async Task<StoredProfileAvatarFile>
        SaveProfileAvatarAsync(
            IFormFile file,
            CancellationToken cancellationToken)
    {
        ValidateImageFile(
            file,
            _options.MaxAvatarImageSizeBytes);

        var extension =
            GetSafeImageExtension(
                file);

        await ValidateImageSignatureAsync(
            file,
            extension,
            cancellationToken);

        var now =
            DateTime.UtcNow;

        var avatarIdentifier =
            Guid.NewGuid()
                .ToString("N");

        var fileName =
            $"{avatarIdentifier}.webp";

        var relativeDirectory =
            $"{now:yyyy}/{now:MM}";

        var storageKey =
            $"{relativeDirectory}/{fileName}";

        var physicalDirectory =
            Path.Combine(
                GetAvatarStorageRootPath(),
                now.ToString("yyyy"),
                now.ToString("MM"));

        Directory.CreateDirectory(
            physicalDirectory);

        var physicalPath =
            Path.Combine(
                physicalDirectory,
                fileName);

        try
        {
            GeneratedThumbnail generatedAvatar;

            await using (
                var sourceStream =
                    file.OpenReadStream())
            await using (
                var destinationStream =
                    new FileStream(
                        physicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
            {
                generatedAvatar =
                    await _thumbnailGenerator.GenerateAsync(
                        sourceStream,
                        destinationStream,
                        _options.MaxImageWidth,
                        _options.MaxImageHeight,
                        _options.MaxImagePixelCount,
                        _options.AvatarMaxWidth,
                        _options.AvatarMaxHeight,
                        _options.AvatarWebpQuality,
                        cancellationToken);
            }

            return new StoredProfileAvatarFile
            {
                StorageKey =
                    storageKey,

                ContentType =
                    generatedAvatar.ContentType,

                FileSizeBytes =
                    generatedAvatar.FileSizeBytes,

                Width =
                    generatedAvatar.Width,

                Height =
                    generatedAvatar.Height
            };
        }
        catch (OperationCanceledException)
        {
            DeleteFileIfExists(
                physicalPath);

            throw;
        }
        catch (MediaStorageException)
        {
            DeleteFileIfExists(
                physicalPath);

            throw;
        }
        catch (Exception exception)
        {
            DeleteFileIfExists(
                physicalPath);

            throw new MediaStorageException(
                $"Не удалось сохранить аватар: " +
                $"{exception.Message}");
        }
    }



    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var physicalPath =
            ResolvePhysicalPath(storageKey);

        if (!File.Exists(physicalPath))
        {
            throw new FileNotFoundException(
                "Медиафайл не найден.",
                physicalPath);
        }

        Stream stream =
            new FileStream(
                physicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

        return Task.FromResult(stream);
    }




    public Task<Stream> OpenProfileAvatarReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var physicalPath =
            ResolveAvatarPhysicalPath(
                storageKey);

        if (!File.Exists(
                physicalPath))
        {
            throw new FileNotFoundException(
                "Файл аватара не найден.",
                physicalPath);
        }

        Stream stream =
            new FileStream(
                physicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

        return Task.FromResult(
            stream);
    }


    // -----------------------------------------------------------------------------
    // EN: Idempotent local file deletion
    // RU: Повторяемое удаление локального файла
    // -----------------------------------------------------------------------------

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var physicalPath =
            ResolvePhysicalPath(
                storageKey);

        if (File.Exists(
                physicalPath))
        {
            File.Delete(
                physicalPath);
        }

        return Task.CompletedTask;
    }



    // -----------------------------------------------------------------------------
    // EN: Idempotent profile avatar deletion
    // RU: Повторяемое удаление аватара профиля
    // -----------------------------------------------------------------------------

    public Task DeleteProfileAvatarAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        var physicalPath =
            ResolveAvatarPhysicalPath(
                storageKey);

        if (File.Exists(
                physicalPath))
        {
            File.Delete(
                physicalPath);
        }

        return Task.CompletedTask;
    }



    public async IAsyncEnumerable<LocalStoredMediaFile>
        EnumerateFilesAsync(
            [EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        var storageRoot =
            GetStorageRootPath();

        if (!Directory.Exists(
                storageRoot))
        {
            yield break;
        }

        var enumerationOptions =
            new EnumerationOptions
            {
                RecurseSubdirectories =
                    true,

                IgnoreInaccessible =
                    true,

                ReturnSpecialDirectories =
                    false,

                AttributesToSkip =
                    FileAttributes.ReparsePoint
            };

        var enumeratedFileCount =
            0;

        foreach (var physicalPath in
                 Directory.EnumerateFiles(
                     storageRoot,
                     "*",
                     enumerationOptions))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            FileInfo fileInfo;

            try
            {
                fileInfo =
                    new FileInfo(
                        physicalPath);

                if (!fileInfo.Exists)
                {
                    continue;
                }
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var storageKey =
                Path.GetRelativePath(
                        storageRoot,
                        physicalPath)
                    .Replace(
                        '\\',
                        '/');

            yield return new LocalStoredMediaFile
            {
                StorageKey =
                    storageKey,

                FileSizeBytes =
                    fileInfo.Length,

                LastWriteTimeUtc =
                    fileInfo.LastWriteTimeUtc
            };

            enumeratedFileCount++;

            // ---------------------------------------------------------------------
            // EN: Periodically release the executing thread during large scans.
            // RU: Периодически освобождаем поток при сканировании большого объёма.
            // ---------------------------------------------------------------------

            if (enumeratedFileCount % 128 == 0)
            {
                await Task.Yield();
            }
        }
    }




    private void ValidateImageFile(
    IFormFile file,
    long maxFileSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(
            file);

        if (file.Length <= 0)
        {
            throw new MediaStorageException(
                "Файл изображения пустой.");
        }

        if (maxFileSizeBytes <= 0)
        {
            throw new MediaStorageException(
                "Некорректно настроен максимальный размер изображения.");
        }

        if (file.Length >
            maxFileSizeBytes)
        {
            throw new MediaStorageException(
                $"Размер изображения не должен превышать " +
                $"{FormatFileSize(maxFileSizeBytes)}.");
        }

        var contentType =
            NormalizeContentType(
                file.ContentType);

        if (!_options.AllowedImageContentTypes.Contains(
                contentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new MediaStorageException(
                "Недопустимый тип изображения. " +
                "Разрешены JPG, PNG и WEBP.");
        }

        var extension =
            Path.GetExtension(
                file.FileName);

        if (string.IsNullOrWhiteSpace(
                extension) ||
            !_options.AllowedImageExtensions.Contains(
                extension,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new MediaStorageException(
                "Недопустимое расширение файла. " +
                "Разрешены JPG, PNG и WEBP.");
        }
    }

    private static async Task ValidateImageSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var buffer =
            new byte[12];

        await using var stream =
            file.OpenReadStream();

        var bytesRead =
            await stream.ReadAsync(
                buffer,
                cancellationToken);

        var isValid =
            IsJpeg(
                buffer,
                bytesRead,
                extension) ||
            IsPng(
                buffer,
                bytesRead,
                extension) ||
            IsWebp(
                buffer,
                bytesRead,
                extension);

        if (!isValid)
        {
            throw new MediaStorageException(
                "Файл не похож на настоящее изображение JPG, PNG или WEBP.");
        }
    }

    private static bool IsJpeg(
        byte[] buffer,
        int bytesRead,
        string extension)
    {
        return
            (extension == ".jpg" ||
             extension == ".jpeg") &&
            bytesRead >= 3 &&
            buffer[0] == 0xFF &&
            buffer[1] == 0xD8 &&
            buffer[2] == 0xFF;
    }

    private static bool IsPng(
        byte[] buffer,
        int bytesRead,
        string extension)
    {
        return
            extension == ".png" &&
            bytesRead >= 8 &&
            buffer[0] == 0x89 &&
            buffer[1] == 0x50 &&
            buffer[2] == 0x4E &&
            buffer[3] == 0x47 &&
            buffer[4] == 0x0D &&
            buffer[5] == 0x0A &&
            buffer[6] == 0x1A &&
            buffer[7] == 0x0A;
    }

    private static bool IsWebp(
        byte[] buffer,
        int bytesRead,
        string extension)
    {
        return
            extension == ".webp" &&
            bytesRead >= 12 &&
            buffer[0] == 0x52 &&
            buffer[1] == 0x49 &&
            buffer[2] == 0x46 &&
            buffer[3] == 0x46 &&
            buffer[8] == 0x57 &&
            buffer[9] == 0x45 &&
            buffer[10] == 0x42 &&
            buffer[11] == 0x50;
    }


    private string GetStorageRootPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                NormalizePath(_options.RootFolder)));
    }

    private string GetAvatarStorageRootPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                _environment.ContentRootPath,
                NormalizePath(
                    _options.AvatarRootFolder)));
    }

    private string ResolvePhysicalPath(
    string storageKey)
    {
        return ResolvePhysicalPath(
            GetStorageRootPath(),
            storageKey);
    }

    private string ResolveAvatarPhysicalPath(
        string storageKey)
    {
        return ResolvePhysicalPath(
            GetAvatarStorageRootPath(),
            storageKey);
    }

    private static string ResolvePhysicalPath(
        string storageRoot,
        string storageKey)
    {
        if (string.IsNullOrWhiteSpace(
                storageKey))
        {
            throw new MediaStorageException(
                "Не указан ключ медиафайла.");
        }

        var normalizedStorageKey =
            storageKey
                .Replace(
                    '\\',
                    '/')
                .TrimStart('/');

        if (normalizedStorageKey.Contains(
                "..",
                StringComparison.Ordinal))
        {
            throw new MediaStorageException(
                "Некорректный путь медиафайла.");
        }

        var normalizedStorageRoot =
            Path.GetFullPath(
                storageRoot);

        var physicalPath =
            Path.GetFullPath(
                Path.Combine(
                    normalizedStorageRoot,
                    NormalizePath(
                        normalizedStorageKey)));

        var storageRootWithSeparator =
            normalizedStorageRoot.EndsWith(
                Path.DirectorySeparatorChar)
                ? normalizedStorageRoot
                : normalizedStorageRoot +
                  Path.DirectorySeparatorChar;

        if (!physicalPath.StartsWith(
                storageRootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new MediaStorageException(
                "Некорректный путь медиафайла.");
        }

        return physicalPath;
    }

    private static string GetSafeImageExtension(
        IFormFile file)
    {
        return Path
            .GetExtension(file.FileName)
            .Trim()
            .ToLowerInvariant();
    }

    private static string NormalizeContentType(
        string? contentType)
    {
        return contentType?
                   .Trim()
                   .ToLowerInvariant()
               ?? string.Empty;
    }

    private static string NormalizePath(
        string value)
    {
        return value
            .Replace(
                '\\',
                Path.DirectorySeparatorChar)
            .Replace(
                '/',
                Path.DirectorySeparatorChar)
            .TrimStart(
                Path.DirectorySeparatorChar);
    }

    private static string FormatFileSize(
        long bytes)
    {
        var megabytes =
            bytes / 1024d / 1024d;

        return $"{megabytes:0.#} МБ";
    }

    private static void DeleteFileIfExists(
    string physicalPath)
    {
        if (File.Exists(
                physicalPath))
        {
            File.Delete(
                physicalPath);
        }
    }
}

