using Microsoft.AspNetCore.Identity;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Services.Identity;

public sealed class DevelopmentIdentityEmailSender
    : IEmailSender<ApplicationUser>
{
    private readonly IHostEnvironment
        _environment;

    private readonly ILogger<
        DevelopmentIdentityEmailSender>
        _logger;

    public DevelopmentIdentityEmailSender(
        IHostEnvironment environment,
        ILogger<
            DevelopmentIdentityEmailSender> logger)
    {
        _environment =
            environment;

        _logger =
            logger;
    }


    public Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink)
    {
        EnsureDevelopmentEnvironment();

        _logger.LogWarning(
            """
            DEVELOPMENT IDENTITY EMAIL

            Type: Email confirmation
            Recipient: {Email}
            UserId: {UserId}
            Link: {ConfirmationLink}
            """,
            email,
            user.Id,
            confirmationLink);

        return Task.CompletedTask;
    }


    public Task SendPasswordResetLinkAsync(
        ApplicationUser user,
        string email,
        string resetLink)
    {
        EnsureDevelopmentEnvironment();

        _logger.LogWarning(
            """
            DEVELOPMENT IDENTITY EMAIL

            Type: Password reset
            Recipient: {Email}
            UserId: {UserId}
            Link: {ResetLink}
            """,
            email,
            user.Id,
            resetLink);

        return Task.CompletedTask;
    }


    public Task SendPasswordResetCodeAsync(
        ApplicationUser user,
        string email,
        string resetCode)
    {
        EnsureDevelopmentEnvironment();

        _logger.LogWarning(
            """
            DEVELOPMENT IDENTITY EMAIL

            Type: Password reset code
            Recipient: {Email}
            UserId: {UserId}
            Code: {ResetCode}
            """,
            email,
            user.Id,
            resetCode);

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // EN: Prevent accidental use of the development sender in production.
    // RU: Запрещаем случайное использование development-отправителя
    //     в production.
    // -------------------------------------------------------------------------

    private void EnsureDevelopmentEnvironment()
    {
        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Production Identity email sender is not configured.");
        }
    }
}
