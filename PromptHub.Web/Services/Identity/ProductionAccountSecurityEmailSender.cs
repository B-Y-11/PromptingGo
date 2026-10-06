using System.Net;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Services.Email;

namespace PromptHub.Web.Services.Identity;

public sealed class ProductionAccountSecurityEmailSender
    : IAccountSecurityEmailSender
{
    private readonly ITransactionalEmailClient
        _emailClient;

    public ProductionAccountSecurityEmailSender(
        ITransactionalEmailClient emailClient)
    {
        _emailClient =
            emailClient;
    }

    public Task SendCodeAsync(
        ApplicationUser user,
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var safeCode =
            WebUtility.HtmlEncode(
                code);

        var html =
            ProductionIdentityEmailSender
                .BuildHtmlDocument(
                    title:
                        "Код безопасности",
                    bodyHtml:
                        $"""
                        <p style="margin:0 0 18px;color:#353535;line-height:1.6;">
                            Используйте этот код для подтверждения защищённого действия в PromptingGo:
                        </p>
                        <p style="margin:0 0 22px;font-size:30px;font-weight:700;letter-spacing:6px;color:#242424;">
                            {safeCode}
                        </p>
                        <p style="margin:0;color:#666;line-height:1.6;font-size:14px;">
                            Никому не сообщайте код. Если вы не выполняли это действие, просто проигнорируйте письмо.
                        </p>
                        """);

        var message =
            new TransactionalEmailMessage(
                RecipientEmail:
                    email,
                Subject:
                    "Код безопасности PromptingGo",
                HtmlBody:
                    html,
                PlainTextBody:
                    $"Код безопасности PromptingGo: {code}\n\n" +
                    "Никому не сообщайте код. Если вы не выполняли это действие, просто проигнорируйте письмо.",
                Tags:
                    ["identity", "security-code"]);

        return _emailClient.SendAsync(
            message,
            cancellationToken);
    }
}
