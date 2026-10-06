using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
[EnableRateLimiting("PhoneOtpRequest")]
public sealed class AddEmailPasswordModel
    : PageModel
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IPhoneOtpService
        _phoneOtpService;

    public AddEmailPasswordModel(
        UserManager<ApplicationUser> userManager,
        IPhoneOtpService phoneOtpService)
    {
        _userManager =
            userManager;

        _phoneOtpService =
            phoneOtpService;
    }

    public string MaskedPhoneNumber { get; private set; }
        = string.Empty;

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

        if (!await CanConfigureAsync(
                user))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        MaskedPhoneNumber =
            MaskPhoneNumber(
                user.PhoneNumber!);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        if (!await CanConfigureAsync(
                user))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        MaskedPhoneNumber =
            MaskPhoneNumber(
                user.PhoneNumber!);

        var result =
            await _phoneOtpService.RequestAsync(
                user.PhoneNumber,
                PhoneOtpPurpose.AddCredentials,
                user.Id,
                cancellationToken);

        switch (result.Status)
        {
            case PhoneOtpRequestStatus.Sent:
                return RedirectToPage(
                    "./AddEmailPasswordVerify",
                    new
                    {
                        challengeId =
                            result.ChallengeId
                    });

            case PhoneOtpRequestStatus.CooldownActive:
                ModelState.AddModelError(
                    string.Empty,
                    $"Новый код можно запросить через " +
                    $"{result.RetryAfterSeconds ?? 1} сек.");

                break;

            case PhoneOtpRequestStatus.RateLimited:
                ModelState.AddModelError(
                    string.Empty,
                    "Слишком много запросов. Попробуйте позже.");

                break;

            case PhoneOtpRequestStatus.ConcurrentRequest:
                ModelState.AddModelError(
                    string.Empty,
                    "Код уже был запрошен. Подождите немного.");

                break;

            default:
                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось отправить код. Попробуйте позже.");

                break;
        }

        return Page();
    }

    private async Task<bool> CanConfigureAsync(
        ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(
                user.PhoneNumber) ||
            !user.PhoneNumberConfirmed)
        {
            return false;
        }

        var hasEmail =
            !string.IsNullOrWhiteSpace(
                await _userManager.GetEmailAsync(
                    user));

        var hasPassword =
            await _userManager.HasPasswordAsync(
                user);

        return !hasEmail &&
               !hasPassword;
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
