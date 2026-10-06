using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Services.Identity;

public sealed class DevelopmentAccountSecurityEmailSender
    : IAccountSecurityEmailSender
{
    private readonly IHostEnvironment
        _environment;

    private readonly ILogger<
        DevelopmentAccountSecurityEmailSender>
        _logger;

    public DevelopmentAccountSecurityEmailSender(
        IHostEnvironment environment,
        ILogger<
            DevelopmentAccountSecurityEmailSender> logger)
    {
        _environment =
            environment;

        _logger =
            logger;
    }

    public Task SendCodeAsync(
        ApplicationUser user,
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Production security email sender is not configured.");
        }

        _logger.LogWarning(
            """
            DEVELOPMENT ACCOUNT SECURITY EMAIL

            Recipient: {Email}
            Code: {Code}
            """,
            email,
            code);

        return Task.CompletedTask;
    }
}
