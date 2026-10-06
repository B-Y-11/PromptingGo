using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class RegisterConfirmationModel
    : PageModel
{
    public string Email { get; private set; }
        = string.Empty;

    public string ReturnUrl { get; private set; }
        = "/";

    public bool DeliveryFailed { get; private set; }

    public void OnGet(
        string? email,
        string? returnUrl = null,
        bool deliveryFailed = false)
    {
        Email =
            email?.Trim()
            ?? string.Empty;

        ReturnUrl =
            !string.IsNullOrWhiteSpace(
                returnUrl) &&
            Url.IsLocalUrl(
                returnUrl)
                ? returnUrl
                : Url.Content(
                    "~/");

        DeliveryFailed =
            deliveryFailed;
    }
}
