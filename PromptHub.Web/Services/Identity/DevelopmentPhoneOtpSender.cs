namespace PromptHub.Web.Services.Identity;

public sealed class DevelopmentPhoneOtpSender
    : IPhoneOtpSender
{
    private readonly IHostEnvironment
        _environment;

    private readonly ILogger<
        DevelopmentPhoneOtpSender>
        _logger;

    public DevelopmentPhoneOtpSender(
        IHostEnvironment environment,
        ILogger<
            DevelopmentPhoneOtpSender> logger)
    {
        _environment =
            environment;

        _logger =
            logger;
    }

    public Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentEnvironment();

        _logger.LogWarning(
            """
            DEVELOPMENT PHONE OTP

            Recipient: {PhoneNumber}
            Code: {Code}
            """,
            phoneNumber,
            code);

        return Task.CompletedTask;
    }

    private void EnsureDevelopmentEnvironment()
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Production SMS sender is not configured.");
        }
    }
}
