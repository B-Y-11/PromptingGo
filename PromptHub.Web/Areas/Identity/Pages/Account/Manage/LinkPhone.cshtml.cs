using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
[EnableRateLimiting("PhoneOtpRequest")]
public sealed class LinkPhoneModel
    : PageModel
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly IPhoneNumberNormalizer
        _phoneNumberNormalizer;

    private readonly IPhoneOtpService
        _phoneOtpService;

    public LinkPhoneModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPhoneNumberNormalizer phoneNumberNormalizer,
        IPhoneOtpService phoneOtpService)
    {
        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _phoneNumberNormalizer =
            phoneNumberNormalizer;

        _phoneOtpService =
            phoneOtpService;
    }

    [BindProperty]
    public InputModel Input { get; set; }
        = new();

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

        if (!string.IsNullOrWhiteSpace(
                user.PhoneNumber))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        if (!await _userManager
                .HasPasswordAsync(
                    user))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

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

        if (!string.IsNullOrWhiteSpace(
                user.PhoneNumber))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: Sensitive account change requires password re-authentication.
        // RU: Для чувствительного изменения повторно проверяем пароль.
        // ---------------------------------------------------------------------

        var passwordResult =
            await _signInManager
                .CheckPasswordSignInAsync(
                    user,
                    Input.CurrentPassword,
                    lockoutOnFailure:
                        true);

        if (passwordResult.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "Аккаунт временно заблокирован. Попробуйте позже.");

            return Page();
        }

        if (!passwordResult.Succeeded)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.CurrentPassword)}",
                "Неверный пароль.");

            return Page();
        }

        if (!_phoneNumberNormalizer
                .TryNormalize(
                    Input.PhoneNumber,
                    out var normalizedPhoneNumber))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.PhoneNumber)}",
                "Введите корректный номер телефона в международном формате.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: Never send an OTP for a phone already owned by another account.
        // RU: Не отправляем OTP на номер, уже принадлежащий другому аккаунту.
        // ---------------------------------------------------------------------

        var phoneAlreadyUsed =
            await _userManager
                .Users
                .AsNoTracking()
                .AnyAsync(
                    otherUser =>
                        otherUser.Id !=
                            user.Id &&
                        otherUser.PhoneNumber ==
                            normalizedPhoneNumber,
                    cancellationToken);

        if (phoneAlreadyUsed)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.PhoneNumber)}",
                "Этот номер нельзя привязать к текущему аккаунту.");

            return Page();
        }

        var result =
            await _phoneOtpService
                .RequestAsync(
                    normalizedPhoneNumber,
                    PhoneOtpPurpose.LinkPhone,
                    user.Id,
                    cancellationToken);

        switch (result.Status)
        {
            case PhoneOtpRequestStatus.Sent:
                return RedirectToPage(
                    "./LinkPhoneVerify",
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

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите текущий пароль.")]
        [DataType(
            DataType.Password)]
        [Display(
            Name =
                "Текущий пароль")]
        public string CurrentPassword { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Введите номер телефона.")]
        [StringLength(
            30)]
        [Display(
            Name =
                "Номер телефона")]
        public string PhoneNumber { get; set; }
            = string.Empty;
    }
}
