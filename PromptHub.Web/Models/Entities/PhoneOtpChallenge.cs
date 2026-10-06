using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PhoneOtpChallenge
{
    public Guid Id { get; set; }
        = Guid.NewGuid();

    public string PhoneNumber { get; set; }
        = string.Empty;

    public PhoneOtpPurpose Purpose { get; set; }

    // -------------------------------------------------------------------------
    // EN: Existing user associated with the challenge, when applicable.
    // RU: Существующий пользователь, связанный с проверкой, если применимо.
    // -------------------------------------------------------------------------

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    // -------------------------------------------------------------------------
    // EN: OTP is never stored in plaintext.
    // RU: OTP никогда не хранится в открытом виде.
    // -------------------------------------------------------------------------

    public string CodeHash { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    // -------------------------------------------------------------------------
    // EN: Failed verification attempts for brute-force protection.
    // RU: Неудачные попытки проверки для защиты от перебора.
    // -------------------------------------------------------------------------

    public int FailedAttempts { get; set; }

    // -------------------------------------------------------------------------
    // EN: Terminal challenge states.
    // RU: Финальные состояния OTP-проверки.
    // -------------------------------------------------------------------------

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? InvalidatedAtUtc { get; set; }
}
