using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("PhoneOtpRequest")]
public sealed class PhoneModel
    : PageModel
{
    private readonly IPhoneOtpService
        _phoneOtpService;

    public PhoneModel(
        IPhoneOtpService phoneOtpService)
    {
        _phoneOtpService =
            phoneOtpService;
    }

    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    public string ReturnUrl { get; private set; }
        = "/";

    public void OnGet(
        string? returnUrl = null)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);
    }


    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result =
            await _phoneOtpService.RequestAsync(
                Input.PhoneNumber,
                PhoneOtpPurpose.Authentication,
                cancellationToken:
                    cancellationToken);

        switch (result.Status)
        {
            case PhoneOtpRequestStatus.Sent:
                return RedirectToPage(
                    "./PhoneVerify",
                    new
                    {
                        challengeId =
                            result.ChallengeId,

                        returnUrl =
                            ReturnUrl
                    });

            case PhoneOtpRequestStatus.InvalidPhoneNumber:
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.PhoneNumber)}",
                    "Введите корректный номер телефона в международном формате.");

                break;

            case PhoneOtpRequestStatus.CooldownActive:
                ModelState.AddModelError(
                    string.Empty,
                    $"Новый код можно запросить через " +
                    $"{result.RetryAfterSeconds ?? 1} сек.");

                break;

            case PhoneOtpRequestStatus.RateLimited:
                ModelState.AddModelError(
                    string.Empty,
                    "Слишком много запросов кода. Попробуйте позже.");

                break;

            case PhoneOtpRequestStatus.ConcurrentRequest:
                ModelState.AddModelError(
                    string.Empty,
                    "Код уже был запрошен. Подождите немного и попробуйте снова.");

                break;

            default:
                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось отправить код. Попробуйте позже.");

                break;
        }

        return Page();
    }

    private string ResolveReturnUrl(
        string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(
                returnUrl) &&
            Url.IsLocalUrl(
                returnUrl))
        {
            return returnUrl;
        }

        return Url.Content(
            "~/");
    }

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите номер телефона.")]
        [StringLength(
            30,
            ErrorMessage =
                "Номер телефона слишком длинный.")]
        [Display(
            Name =
                "Номер телефона")]
        public string PhoneNumber { get; set; }
            = string.Empty;
    }
}
