namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaCleanupResult
{

    public bool IsDryRun { get; set; }


    public int EligibleCount { get; set; }

    public int EligibleArchivedCount { get; set; }

    public int EligibleRejectedCount { get; set; }

    public long EligibleBytes { get; set; }


    public int ProcessedCount { get; set; }

    public int PurgedCount { get; set; }

    public int FailedCount { get; set; }

    public int RemainingEligibleCount { get; set; }

    public long PurgedBytes { get; set; }

    public List<PromptMediaCleanupError> Errors
    {
        get;
    } = [];
}

public sealed class PromptMediaCleanupError
{
    public int MediaId { get; set; }

    public string Message { get; set; }
        = string.Empty;
}
