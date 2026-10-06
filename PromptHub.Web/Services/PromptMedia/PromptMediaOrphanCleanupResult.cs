namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaOrphanCleanupResult
{

    public bool IsDryRun { get; set; }


    public int ScannedFileCount { get; set; }

    public int ReferencedFileCount { get; set; }

    public int RecentUnreferencedFileCount { get; set; }

    public int EligibleOrphanCount { get; set; }

    public long EligibleBytes { get; set; }


    public int ProcessedCount { get; set; }

    public int DeletedCount { get; set; }

    public int FailedCount { get; set; }

    public long DeletedBytes { get; set; }

    public List<PromptMediaOrphanCleanupError> Errors
    {
        get;
    } = [];
}

public sealed class PromptMediaOrphanCleanupError
{
    public string StorageKey { get; set; }
        = string.Empty;

    public string Message { get; set; }
        = string.Empty;
}
