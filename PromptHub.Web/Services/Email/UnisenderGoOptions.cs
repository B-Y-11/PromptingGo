using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Services.Email;

public sealed class UnisenderGoOptions
{
    public const string SectionName =
        "UnisenderGo";

    [Required]
    public string ApiKey { get; set; }
        = string.Empty;

    [Required]
    [EmailAddress]
    public string FromEmail { get; set; }
        = "noreply@example.com";

    [Required]
    public string FromName { get; set; }
        = "PromptingGo";

    [Required]
    [Url]
    public string BaseUrl { get; set; }
        = "https://goapi.unisender.ru/ru/transactional/api/v1/";

    [Range(1, 60)]
    public int RequestTimeoutSeconds { get; set; }
        = 10;

    // -------------------------------------------------------------------------
    // EN: These switches require corresponding permissions from Unisender Go
    //     support. Keep them disabled until support explicitly enables them.
    // RU: Эти параметры требуют разрешения поддержки Unisender Go. Не
    //     включайте их, пока поддержка явно не активирует такую возможность.
    // -------------------------------------------------------------------------

    public bool DisableTracking { get; set; }
        = false;

    public bool SkipUnsubscribeBlock { get; set; }
        = false;
}
