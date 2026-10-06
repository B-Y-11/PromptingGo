using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Services.Security;

public sealed class SmartCaptchaOptions
{
    public const string SectionName =
        "SmartCaptcha";

    public bool Enabled { get; set; }
        = false;

    public string ClientKey { get; set; }
        = string.Empty;

    public string ServerKey { get; set; }
        = string.Empty;

    [Required]
    [Url]
    public string ValidationEndpoint { get; set; }
        = "https://smartcaptcha.cloud.yandex.ru/validate";

    [Range(1, 10)]
    public int RequestTimeoutSeconds { get; set; }
        = 2;
}
