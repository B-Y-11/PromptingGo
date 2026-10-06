namespace PromptHub.Web.Models.ViewModels.Moderation;

public sealed class PromptReportsViewModel
{
    public IReadOnlyList<PromptReportItemViewModel> Reports { get; set; }
        = Array.Empty<PromptReportItemViewModel>();
}

public sealed class PromptReportItemViewModel
{
    public int Id { get; set; }

    public string PromptTitle { get; set; }
        = string.Empty;

    public string PromptSlug { get; set; }
        = string.Empty;

    public string PromptSummary { get; set; }
        = string.Empty;

    public string CategoryName { get; set; }
        = string.Empty;

    public string ReporterName { get; set; }
        = string.Empty;

    public string ReasonName { get; set; }
        = string.Empty;

    public string Details { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
