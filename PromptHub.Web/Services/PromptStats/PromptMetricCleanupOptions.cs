using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Services.PromptStats;

public sealed class PromptMetricCleanupOptions
{
    public const string SectionName =
        "PromptMetricCleanup";

    [Range(1, 90)]
    public int RetentionDays { get; set; }
        = 7;

    [Range(1, 24)]
    public int CleanupIntervalHours { get; set; }
        = 6;
}
