using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaCleanupOptions
{
    public const string SectionName =
        "PromptMediaCleanup";


    [Range(0, 3650)]
    public int ArchivedRetentionDays { get; set; }
        = 30;

    [Range(0, 3650)]
    public int RejectedRetentionDays { get; set; }
        = 30;


    [Range(1, 1000)]
    public int BatchSize { get; set; }
        = 100;


    // -------------------------------------------------------------------------
    // EN: Orphan-file safety window
    // RU: Защитный период для потерянных файлов
    // -------------------------------------------------------------------------

    [Range(1, 720)]
    public int OrphanSafetyWindowHours { get; set; }
        = 24;
}
