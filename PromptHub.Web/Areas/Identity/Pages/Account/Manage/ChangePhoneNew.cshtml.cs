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
[EnableRateLimiting("ChangePhoneVerification")]
public sealed class ChangePhoneNewModel
    : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IPhoneNumberNormalizer
        _phoneNumberNormalizer;

    private readonly IPhoneOtpService
        _phoneOtpService;

    private readonly IPhoneChangeContinuationProtector
        _continuationProtector;

    public ChangePhoneNewModel(
        UserManager<ApplicationUser> userManager,
        IPhoneNumberNormalizer phoneNumberNormalizer,
        IPhoneOtpService phoneOtpService,
        IPhoneChangeContinuationProtector continuationProtector)
    {
        _userManager =
            userManager;

        _phoneNumberNormalizer =
            phoneNumberNormalizer;

        _phoneOtpService =
            phoneOtpService;

        _continuationProtector =
            continuationProtector;
    }


    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    [BindProperty]
    public string Continuation { get; set; }
        = string.Empty;

    public string MaskedCurrentPhoneNumber { get; private set; }
        = string.Empty;


    public async Task<IActionResult> OnGetAsync(
        string? continuation)
    {
        Continuation =
            continuation ??
            string.Empty;

        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        var authorization =
            await ValidateAuthorizationAsync(
                user);

        if (authorization is null)
        {
            return RedirectToPage(
                "./ChangePhone");
        }

        MaskedCurrentPhoneNumber =
            MaskPhoneNumber(
                authorization.CurrentPhoneNumber);

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

        var authorization =
            await ValidateAuthorizationAsync(
                user);

        if (authorization is null)
        {
            return RedirectToPage(
                "./ChangePhone");
        }

        MaskedCurrentPhoneNumber =
            MaskPhoneNumber(
                authorization.CurrentPhoneNumber);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!_phoneNumberNormalizer.TryNormalize(
                Input.PhoneNumber,
                out var normalizedPhoneNumber))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.PhoneNumber)}",
                "Введите корректный номер телефона в международном формате.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: The replacement phone must actually differ from the current one.
        // RU: Новый номер действительно должен отличаться от текущего.
        // ---------------------------------------------------------------------

        if (string.Equals(
                normalizedPhoneNumber,
                authorization.CurrentPhoneNumber,
                StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.PhoneNumber)}",
                "Этот номер уже привязан к вашему аккаунту.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: Do not send OTP when the phone is already owned by another user.
        // RU: Не отправляем OTP, если номер уже принадлежит другому аккаунту.
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
                "Этот номер уже используется другим аккаунтом.");

            return Page();
        }

        var result =
            await _phoneOtpService.RequestAsync(
                normalizedPhoneNumber,
                PhoneOtpPurpose.ChangePhoneNew,
                user.Id,
                cancellationToken);

        switch (result.Status)
        {
            case PhoneOtpRequestStatus.Sent:
                return RedirectToPage(
                    "./ChangePhoneVerifyNew",
                    new
                    {
                        challengeId =
                            result.ChallengeId,

                        continuation =
                            Continuation
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

            case PhoneOtpRequestStatus.InvalidPhoneNumber:
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.PhoneNumber)}",
                    "Введите корректный номер телефона.");

                break;

            default:
                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось отправить код. Попробуйте позже.");

                break;
        }

        return Page();
    }

    // -------------------------------------------------------------------------
    // EN: Validate short-lived authorization issued after old-account proof.
    // RU: Проверяем краткоживущее разрешение, полученное после
    //     подтверждения владельца аккаунта.
    // -------------------------------------------------------------------------

    private async Task<PhoneChangeAuthorization?>
        ValidateAuthorizationAsync(
            ApplicationUser user)
    {
        if (!_continuationProtector.TryUnprotect(
                Continuation,
                out var authorization) ||
            authorization is null)
        {
            return null;
        }

        if (!string.Equals(
                authorization.UserId,
                user.Id,
                StringComparison.Ordinal))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(
                user.PhoneNumber) ||
            !user.PhoneNumberConfirmed)
        {
            return null;
        }

        if (!string.Equals(
                authorization.CurrentPhoneNumber,
                user.PhoneNumber,
                StringComparison.Ordinal))
        {
            return null;
        }

        var currentSecurityStamp =
            await _userManager.GetSecurityStampAsync(
                user);

        if (string.IsNullOrWhiteSpace(
                currentSecurityStamp) ||
            !string.Equals(
                authorization.SecurityStamp,
                currentSecurityStamp,
                StringComparison.Ordinal))
        {
            return null;
        }

        return authorization;
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

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите новый номер телефона.")]
        [StringLength(
            30)]
        [Display(
            Name =
                "Новый номер телефона")]
        public string PhoneNumber { get; set; }
            = string.Empty;
    }
}
