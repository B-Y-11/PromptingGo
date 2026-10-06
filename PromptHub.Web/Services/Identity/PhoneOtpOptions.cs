using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneOtpOptions
{
    public const string SectionName =
        "PhoneOtp";


    [Range(1, 30)]
    public int LifetimeMinutes { get; set; }
        = 5;


    [Range(10, 600)]
    public int ResendCooldownSeconds { get; set; }
        = 60;


    [Range(1, 100)]
    public int MaxRequestsPerHour { get; set; }
        = 5;


    [Range(1, 10)]
    public int MaxVerificationAttempts { get; set; }
        = 5;

    [Range(1, 100)]
    public int MaxRequestsPerUserPerHour { get; set; }
    = 10;


    [Range(1, 168)]
    public int RetentionHours { get; set; }
        = 24;


    [Range(5, 1440)]
    public int CleanupIntervalMinutes { get; set; }
        = 60;

    // -------------------------------------------------------------------------
    // EN: Server-side secret used for OTP hashing.
    //     Must never be committed to source control.
    // RU: Серверный секрет для хеширования OTP.
    //     Никогда не должен попадать в репозиторий.
    // -------------------------------------------------------------------------

    public string HashingKey { get; set; }
        = string.Empty;
}
