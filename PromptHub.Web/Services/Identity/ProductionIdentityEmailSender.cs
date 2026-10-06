using System.Net;
using Microsoft.AspNetCore.Identity;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Services.Email;

namespace PromptHub.Web.Services.Identity;

public sealed class ProductionIdentityEmailSender
    : IEmailSender<ApplicationUser>
{
    private readonly ITransactionalEmailClient
        _emailClient;

    public ProductionIdentityEmailSender(
        ITransactionalEmailClient emailClient)
    {
        _emailClient =
            emailClient;
    }

    public Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink)
    {
        var message =
            CreateActionMessage(
                email,
                subject:
                    "Подтвердите email в PromptingGo",
                title:
                    "Подтвердите адрес электронной почты",
                description:
                    "Нажмите кнопку ниже, чтобы подтвердить email и завершить настройку аккаунта PromptingGo.",
                actionText:
                    "Подтвердить email",
                actionUrl:
                    confirmationLink,
                footer:
                    "Если вы не создавали аккаунт PromptingGo, просто проигнорируйте это письмо.",
                tags:
                    ["identity", "email-confirmation"]);

        return _emailClient.SendAsync(
            message);
    }

    public Task SendPasswordResetLinkAsync(
        ApplicationUser user,
        string email,
        string resetLink)
    {
        var message =
            CreateActionMessage(
                email,
                subject:
                    "Восстановление пароля PromptingGo",
                title:
                    "Создайте новый пароль",
                description:
                    "Мы получили запрос на восстановление доступа к аккаунту PromptingGo.",
                actionText:
                    "Создать новый пароль",
                actionUrl:
                    resetLink,
                footer:
                    "Если вы не запрашивали восстановление пароля, не переходите по ссылке и просто проигнорируйте письмо.",
                tags:
                    ["identity", "password-reset"]);

        return _emailClient.SendAsync(
            message);
    }

    public Task SendPasswordResetCodeAsync(
        ApplicationUser user,
        string email,
        string resetCode)
    {
        var safeCode =
            WebUtility.HtmlEncode(
                resetCode);

        var html =
            BuildHtmlDocument(
                title:
                    "Код восстановления пароля",
                bodyHtml:
                    $"""
                    <p style="margin:0 0 18px;color:#353535;line-height:1.6;">
                        Используйте этот код для восстановления доступа к PromptingGo:
                    </p>
                    <p style="margin:0 0 22px;font-size:30px;font-weight:700;letter-spacing:6px;color:#242424;">
                        {safeCode}
                    </p>
                    <p style="margin:0;color:#666;line-height:1.6;font-size:14px;">
                        Если вы не запрашивали восстановление пароля, просто проигнорируйте это письмо.
                    </p>
                    """);

        var message =
            new TransactionalEmailMessage(
                RecipientEmail:
                    email,
                Subject:
                    "Код восстановления пароля PromptingGo",
                HtmlBody:
                    html,
                PlainTextBody:
                    $"Код восстановления пароля PromptingGo: {resetCode}\n\n" +
                    "Если вы не запрашивали восстановление пароля, просто проигнорируйте это письмо.",
                Tags:
                    ["identity", "password-reset-code"]);

        return _emailClient.SendAsync(
            message);
    }

    private static TransactionalEmailMessage CreateActionMessage(
        string email,
        string subject,
        string title,
        string description,
        string actionText,
        string actionUrl,
        string footer,
        IReadOnlyCollection<string> tags)
    {
        var safeActionUrl =
            WebUtility.HtmlEncode(
                actionUrl);

        var safeActionText =
            WebUtility.HtmlEncode(
                actionText);

        var html =
            BuildHtmlDocument(
                title,
                $"""
                <p style="margin:0 0 20px;color:#353535;line-height:1.6;">
                    {WebUtility.HtmlEncode(description)}
                </p>
                <p style="margin:0 0 24px;">
                    <a href="{safeActionUrl}"
                       style="display:inline-block;padding:12px 20px;border-radius:8px;background:#5b48bb;color:#ffffff;text-decoration:none;font-weight:700;">
                        {safeActionText}
                    </a>
                </p>
                <p style="margin:0 0 8px;color:#666;line-height:1.5;font-size:13px;">
                    Если кнопка не открывается, скопируйте ссылку в браузер:
                </p>
                <p style="margin:0 0 22px;word-break:break-all;color:#4d4d4d;font-size:13px;line-height:1.5;">
                    {safeActionUrl}
                </p>
                <p style="margin:0;color:#666;line-height:1.6;font-size:14px;">
                    {WebUtility.HtmlEncode(footer)}
                </p>
                """);

        var plainText =
            $"{title}\n\n" +
            $"{description}\n\n" +
            $"{actionText}: {actionUrl}\n\n" +
            footer;

        return new TransactionalEmailMessage(
            RecipientEmail:
                email,
            Subject:
                subject,
            HtmlBody:
                html,
            PlainTextBody:
                plainText,
            Tags:
                tags);
    }

    internal static string BuildHtmlDocument(
        string title,
        string bodyHtml)
    {
        return
            $"""
            <!doctype html>
            <html lang="ru">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width,initial-scale=1">
                <title>{WebUtility.HtmlEncode(title)}</title>
            </head>
            <body style="margin:0;padding:0;background:#f5f4ef;font-family:Arial,Helvetica,sans-serif;color:#242424;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#f5f4ef;padding:28px 12px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:620px;background:#ffffff;border:1px solid #dedbd0;border-radius:12px;">
                                <tr>
                                    <td style="padding:30px 32px;">
                                        <div style="margin-bottom:18px;font-size:20px;font-weight:700;color:#5b48bb;">
                                            PromptingGo
                                        </div>
                                        <h1 style="margin:0 0 20px;font-size:24px;line-height:1.3;color:#242424;">
                                            {WebUtility.HtmlEncode(title)}
                                        </h1>
                                        {bodyHtml}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}
