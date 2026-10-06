using System.ComponentModel.DataAnnotations;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class AuthorSupportMethod
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }
        = string.Empty;

    public AuthorSupportProvider Provider { get; set; }

    [Required]
    [MaxLength(100)]
    public string ExternalReference { get; set; }
        = string.Empty;

    public bool IsEnabled { get; set; }
        = true;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public ApplicationUser User { get; set; }
        = null!;
}
