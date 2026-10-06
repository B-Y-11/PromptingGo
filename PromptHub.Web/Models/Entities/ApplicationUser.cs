using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Models.Entities;

public sealed class ApplicationUser : IdentityUser
{
    [PersonalData]
    [MaxLength(80)]
    public string DisplayName { get; set; } = string.Empty;

    [PersonalData]
    [MaxLength(500)]
    public string Bio { get; set; } = string.Empty;



    [PersonalData]
    [MaxLength(500)]
    public string? AvatarStorageKey { get; set; }

    [PersonalData]
    [MaxLength(100)]
    public string? AvatarContentType { get; set; }

    [PersonalData]
    public long? AvatarFileSizeBytes { get; set; }

    [PersonalData]
    public int? AvatarWidth { get; set; }

    [PersonalData]
    public int? AvatarHeight { get; set; }

    [PersonalData]
    public DateTime? AvatarUpdatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // -------------------------------------------------------------------------
    // EN: Legal document acceptance metadata.
    // RU: Метаданные принятия юридических документов.
    // -------------------------------------------------------------------------

    [PersonalData]
    public DateTime? TermsAcceptedAtUtc { get; set; }

    [PersonalData]
    [MaxLength(32)]
    public string? TermsVersion { get; set; }

    [PersonalData]
    public DateTime? ContentRulesAcceptedAtUtc { get; set; }

    [PersonalData]
    [MaxLength(32)]
    public string? ContentRulesVersion { get; set; }

    [PersonalData]
    public DateTime? PersonalDataConsentAcceptedAtUtc { get; set; }

    [PersonalData]
    [MaxLength(32)]
    public string? PersonalDataConsentVersion { get; set; }

    public ICollection<Prompt> AuthoredPrompts { get; set; }
    = new List<Prompt>();

    public ICollection<PromptFavorite> FavoritePrompts { get; set; }
    = new List<PromptFavorite>();

    public ICollection<PromptRating> PromptRatings { get; set; }
    = new List<PromptRating>();

    public ICollection<PromptReport> PromptReports { get; set; }
    = new List<PromptReport>();

    public ICollection<UserNotification> Notifications { get; set; }
    = new List<UserNotification>();

    public ICollection<AuthorSupportMethod> SupportMethods { get; set; }
    = new List<AuthorSupportMethod>();
}
