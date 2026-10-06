using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class ConfirmEmailModel
    : PageModel
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    public ConfirmEmailModel(
        UserManager<ApplicationUser> userManager)
    {
        _userManager =
            userManager;
    }

    public bool Succeeded { get; private set; }

    public string StatusMessage { get; private set; }
        = string.Empty;

    public string ReturnUrl { get; private set; }
        = "/";


    public async Task OnGetAsync(
        string? userId,
        string? code,
        string? returnUrl = null)
    {
        ReturnUrl =
            !string.IsNullOrWhiteSpace(
                returnUrl) &&
            Url.IsLocalUrl(
                returnUrl)
                ? returnUrl
                : Url.Content(
                    "~/");

        if (string.IsNullOrWhiteSpace(
                userId) ||
            string.IsNullOrWhiteSpace(
                code))
        {
            SetInvalidLinkResult();

            return;
        }

        var user =
            await _userManager.FindByIdAsync(
                userId);

        if (user is null)
        {
            SetInvalidLinkResult();

            return;
        }

        if (await _userManager
                .IsEmailConfirmedAsync(
                    user))
        {
            Succeeded =
                true;

            StatusMessage =
                "Email уже подтверждён.";

            return;
        }

        string confirmationToken;

        try
        {
            confirmationToken =
                Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(
                        code));
        }
        catch (FormatException)
        {
            SetInvalidLinkResult();

            return;
        }

        var result =
            await _userManager
                .ConfirmEmailAsync(
                    user,
                    confirmationToken);

        if (result.Succeeded)
        {
            Succeeded =
                true;

            StatusMessage =
                "Email успешно подтверждён.";

            return;
        }

        SetInvalidLinkResult();
    }

    private void SetInvalidLinkResult()
    {
        Succeeded =
            false;

        StatusMessage =
            "Ссылка подтверждения недействительна или уже устарела.";
    }
}
