namespace PromptHub.Web.Services.Identity;


public enum PhoneOtpRequestStatus
{
    Sent = 0,

    InvalidPhoneNumber = 1,

    CooldownActive = 2,

    RateLimited = 3,

    DeliveryFailed = 4,

    ConcurrentRequest = 5
}

public sealed record PhoneOtpRequestResult(
    PhoneOtpRequestStatus Status,
    Guid? ChallengeId = null,
    string? PhoneNumber = null,
    int? RetryAfterSeconds = null);



public enum PhoneOtpVerificationStatus
{
    Succeeded = 0,

    InvalidCode = 1,

    Expired = 2,

    AttemptsExceeded = 3,

    Unavailable = 4
}

public sealed record PhoneOtpVerificationResult(
    PhoneOtpVerificationStatus Status,
    string? PhoneNumber = null,
    string? UserId = null);
