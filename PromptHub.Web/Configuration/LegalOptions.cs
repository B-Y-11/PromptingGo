using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Configuration;

public sealed class LegalOptions
{
    public const string SectionName =
        "Legal";

    public string OperatorName { get; set; } =
        string.Empty;

    public string OperatorAddress { get; set; } =
        string.Empty;

    [Required]
    [EmailAddress]
    public string ContactEmail { get; set; } =
        string.Empty;
}
