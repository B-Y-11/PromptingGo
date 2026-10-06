using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class SignInMethodsModel
    : PageModel
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly bool
        _phoneAuthenticationEnabled;

    public SignInMethodsModel(
        UserManager<ApplicationUser> userManager,
        IOptions<PhoneAuthenticationFeatureOptions> phoneAuthenticationOptions)
    {
        _userManager =
            userManager;

        _phoneAuthenticationEnabled =
            phoneAuthenticationOptions.Value.Enabled;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public string Email { get; private set; }
        = string.Empty;

    public bool EmailConfirmed { get; private set; }

    public string MaskedPhoneNumber { get; private set; }
        = string.Empty;

    public bool HasPhoneNumber { get; private set; }

    public bool HasPassword { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        Email =
            await _userManager.GetEmailAsync(
                user)
            ?? string.Empty;

        EmailConfirmed =
            !string.IsNullOrWhiteSpace(
                Email) &&
            await _userManager
                .IsEmailConfirmedAsync(
                    user);

        if (_phoneAuthenticationEnabled)
        {
            var phoneNumber =
                await _userManager
                    .GetPhoneNumberAsync(
                        user);

            HasPhoneNumber =
                !string.IsNullOrWhiteSpace(
                    phoneNumber);

            MaskedPhoneNumber =
                HasPhoneNumber
                    ? MaskPhoneNumber(
                        phoneNumber!)
                    : string.Empty;
        }

        HasPassword =
            await _userManager
                .HasPasswordAsync(
                    user);

        return Page();
    }

    private static string MaskPhoneNumber(
        string phoneNumber)
    {
        if (phoneNumber.Length <= 5)
        {
            return phoneNumber;
        }

        return
            phoneNumber[..3] +
            new string(
                '*',
                phoneNumber.Length - 5) +
            phoneNumber[^2..];
    }
}
