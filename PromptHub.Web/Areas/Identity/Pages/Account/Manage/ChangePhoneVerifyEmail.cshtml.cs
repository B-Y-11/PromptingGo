using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
[EnableRateLimiting("ChangePhoneVerification")]
public sealed class ChangePhoneVerifyEmailModel
    : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly IPhoneChangeContinuationProtector
        _continuationProtector;

    private readonly ILogger<ChangePhoneVerifyEmailModel>
        _logger;

    public ChangePhoneVerifyEmailModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPhoneChangeContinuationProtector continuationProtector,
        ILogger<ChangePhoneVerifyEmailModel> logger)
    {
        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _continuationProtector =
            continuationProtector;

        _logger =
            logger;
    }


    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    public string MaskedEmail { get; private set; }
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

        if (!await CanUseEmailVerificationAsync(
                user))
        {
            return RedirectToPage(
                "./ChangePhone");
        }

        var email =
            await _userManager.GetEmailAsync(
                user);

        MaskedEmail =
            MaskEmail(
                email!);

        return Page();
    }


    public async Task<IActionResult> OnPostAsync()
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        if (!await CanUseEmailVerificationAsync(
                user))
        {
            return RedirectToPage(
                "./ChangePhone");
        }

        var email =
            await _userManager.GetEmailAsync(
                user);

        MaskedEmail =
            MaskEmail(
                email!);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var valid =
            await _userManager.VerifyUserTokenAsync(
                user,
                TokenOptions.DefaultEmailProvider,
                PhoneChangeTokenPurposes.EmailRecovery,
                Input.Code);

        if (!valid)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Code)}",
                "Неверный или просроченный код.");

            return Page();
        }

        if (string.IsNullOrWhiteSpace(
                user.PhoneNumber) ||
            !user.PhoneNumberConfirmed)
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        // ---------------------------------------------------------------------
        // EN: Rotate the security stamp immediately after successful email
        //     verification. The verified email token becomes unusable again.
        //
        // RU: Сразу после успешной проверки email-кода меняем SecurityStamp.
        //     Старый email-код после этого больше нельзя использовать повторно.
        // ---------------------------------------------------------------------

        var stampResult =
            await _userManager
                .UpdateSecurityStampAsync(
                    user);

        if (!stampResult.Succeeded)
        {
            _logger.LogError(
                "Failed to rotate security stamp after phone-change email verification for user {UserId}. Errors: {Errors}",
                user.Id,
                string.Join(
                    ", ",
                    stampResult.Errors.Select(
                        error =>
                            error.Code)));

            ModelState.AddModelError(
                string.Empty,
                "Не удалось завершить проверку безопасности. Попробуйте ещё раз.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: SecurityStamp changed, therefore refresh the current session.
        //     Other existing sessions will fail stamp validation later.
        //
        // RU: SecurityStamp изменился, поэтому обновляем текущую сессию.
        //     Остальные старые сессии позже не пройдут проверку stamp.
        // ---------------------------------------------------------------------

        await _signInManager.RefreshSignInAsync(
            user);

        var securityStamp =
            await _userManager.GetSecurityStampAsync(
                user);

        if (string.IsNullOrWhiteSpace(
                securityStamp))
        {
            _logger.LogError(
                "Security stamp was unavailable after phone-change email verification for user {UserId}.",
                user.Id);

            return RedirectToPage(
                "./SignInMethods");
        }

        // ---------------------------------------------------------------------
        // EN: Issue a short-lived authorization bound to the NEW security
        //     stamp. Any older phone-change continuation becomes invalid.
        //
        // RU: Создаём краткоживущее разрешение уже с НОВЫМ SecurityStamp.
        //     Старые continuation после изменения stamp становятся невалидными.
        // ---------------------------------------------------------------------

        var authorization =
            new PhoneChangeAuthorization(
                user.Id,
                user.PhoneNumber,
                securityStamp);

        var continuation =
            _continuationProtector.Protect(
                authorization);

        return RedirectToPage(
            "./ChangePhoneNew",
            new
            {
                continuation
            });
    }

    // -------------------------------------------------------------------------
    // EN: Check whether email recovery is available for this account.
    // RU: Проверяем доступность восстановления через email.
    // -------------------------------------------------------------------------

    private async Task<bool>
        CanUseEmailVerificationAsync(
            ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(
                user.PhoneNumber) ||
            !user.PhoneNumberConfirmed)
        {
            return false;
        }

        var email =
            await _userManager.GetEmailAsync(
                user);

        if (string.IsNullOrWhiteSpace(
                email))
        {
            return false;
        }

        var emailConfirmed =
            await _userManager
                .IsEmailConfirmedAsync(
                    user);

        var hasPassword =
            await _userManager
                .HasPasswordAsync(
                    user);

        return
            emailConfirmed &&
            hasPassword;
    }

    private static string MaskEmail(
        string email)
    {
        var atIndex =
            email.IndexOf(
                '@');

        if (atIndex <= 0)
        {
            return "***";
        }

        var localPart =
            email[..atIndex];

        var domain =
            email[atIndex..];

        if (localPart.Length == 1)
        {
            return
                localPart +
                "***" +
                domain;
        }

        return
            localPart[0] +
            new string(
                '*',
                Math.Min(
                    6,
                    localPart.Length - 1)) +
            domain;
    }

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите код.")]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage =
                "Код должен состоять из 6 цифр.")]
        [Display(
            Name =
                "Код подтверждения")]
        public string Code { get; set; }
            = string.Empty;
    }
}
