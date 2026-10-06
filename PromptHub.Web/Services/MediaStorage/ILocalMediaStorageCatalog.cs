namespace PromptHub.Web.Services.MediaStorage;

public interface ILocalMediaStorageCatalog
{
    // -------------------------------------------------------------------------
    // EN: Enumerate regular files inside the private local storage root.
    // RU: Перечисление обычных файлов приватного локального хранилища.
    // -------------------------------------------------------------------------

    IAsyncEnumerable<LocalStoredMediaFile> EnumerateFilesAsync(
        CancellationToken cancellationToken);
}
