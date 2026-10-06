using Microsoft.AspNetCore.Http;

namespace PromptHub.Web.Services.MediaStorage;

public interface IMediaStorageService
{

    Task<StoredMediaFile> SavePromptImageAsync(
        IFormFile file,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken);


    Task<StoredProfileAvatarFile> SaveProfileAvatarAsync(
        IFormFile file,
        CancellationToken cancellationToken);

    Task<Stream> OpenProfileAvatarReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task DeleteProfileAvatarAsync(
        string storageKey,
        CancellationToken cancellationToken);
}
