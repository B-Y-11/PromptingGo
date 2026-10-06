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
[EnableRateLimiting("ChangePhoneVerification")]
public sealed class ChangePhoneModel
    : PageModel
{


    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly IPhoneOtpService
        _phoneOtpService;

    private readonly IAccountSecurityEmailSender
        _securityEmailSender;

    public ChangePhoneModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPhoneOtpService phoneOtpService,
        IAccountSecurityEmailSender securityEmailSender)
    {
        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _phoneOtpService =
            phoneOtpService;

        _securityEmailSender =
            securityEmailSender;
    }


    [BindProperty]
    public string EmailRecoveryPassword { get; set; }
        = string.Empty;

    public string MaskedPhoneNumber { get; private set; }
        = string.Empty;

    public string MaskedEmail { get; private set; }
        = string.Empty;

    public bool CanUseEmailRecovery { get; private set; }


    public async Task<IActionResult> OnGetAsync()
    {
        var user =
            await LoadUserAsync();

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        if (!HasConfirmedPhone(
                user))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        await LoadPageStateAsync(
            user);

        return Page();
    }


    public async Task<IActionResult> OnPostSmsAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await LoadUserAsync();

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        if (!HasConfirmedPhone(
                user))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        await LoadPageStateAsync(
            user);

        var result =
            await _phoneOtpService.RequestAsync(
                user.PhoneNumber,
                PhoneOtpPurpose.ChangePhoneCurrent,
                user.Id,
                cancellationToken);

        switch (result.Status)
        {
            case PhoneOtpRequestStatus.Sent:
                return RedirectToPage(
                    "./ChangePhoneVerifyCurrent",
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

            case PhoneOtpRequestStatus.InvalidPhoneNumber:
                ModelState.AddModelError(
                    string.Empty,
                    "Текущий номер телефона имеет некорректный формат.");

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
    // EN: Recovery when access to the current phone has been lost.
    //     Requires the account password and an already confirmed email.
    //
    // RU: Резервный способ при потере доступа к текущему телефону.
    //     Требуются пароль аккаунта и уже подтверждённый email.
    // -------------------------------------------------------------------------

    public async Task<IActionResult> OnPostEmailAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await LoadUserAsync();

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        if (!HasConfirmedPhone(
                user))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        await LoadPageStateAsync(
            user);

        if (!CanUseEmailRecovery)
        {
            ModelState.AddModelError(
                string.Empty,
                "Восстановление через email для этого аккаунта недоступно.");

            return Page();
        }

        if (string.IsNullOrWhiteSpace(
                EmailRecoveryPassword))
        {
            ModelState.AddModelError(
                nameof(EmailRecoveryPassword),
                "Введите текущий пароль.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: Password re-authentication protects this sensitive operation.
        // RU: Перед чувствительной операцией повторно проверяем пароль.
        // ---------------------------------------------------------------------

        var passwordResult =
            await _signInManager
                .CheckPasswordSignInAsync(
                    user,
                    EmailRecoveryPassword,
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
                nameof(EmailRecoveryPassword),
                "Неверный пароль.");

            return Page();
        }

        var email =
            await _userManager
                .GetEmailAsync(
                    user);

        if (string.IsNullOrWhiteSpace(
                email) ||
            !await _userManager
                .IsEmailConfirmedAsync(
                    user))
        {
            ModelState.AddModelError(
                string.Empty,
                "Подтверждённый email не найден.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: Generate a short-lived Identity email token specifically for
        //     changing the phone number.
        //
        // RU: Генерируем Identity-токен специально для подтверждения
        //     смены номера телефона.
        // ---------------------------------------------------------------------

        var code =
    await _userManager
        .GenerateUserTokenAsync(
            user,
            TokenOptions.DefaultEmailProvider,
            PhoneChangeTokenPurposes.EmailRecovery);

        try
        {
            await _securityEmailSender
                .SendCodeAsync(
                    user,
                    email,
                    code,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            ModelState.AddModelError(
                string.Empty,
                "Не удалось отправить код на email. Попробуйте позже.");

            return Page();
        }

        return RedirectToPage(
            "./ChangePhoneVerifyEmail");
    }


    private async Task<ApplicationUser?>
        LoadUserAsync()
    {
        return await _userManager
            .GetUserAsync(
                User);
    }

    private async Task LoadPageStateAsync(
        ApplicationUser user)
    {
        MaskedPhoneNumber =
            MaskPhoneNumber(
                user.PhoneNumber!);

        var email =
            await _userManager
                .GetEmailAsync(
                    user);

        var hasPassword =
            await _userManager
                .HasPasswordAsync(
                    user);

        var emailConfirmed =
            !string.IsNullOrWhiteSpace(
                email) &&
            await _userManager
                .IsEmailConfirmedAsync(
                    user);

        CanUseEmailRecovery =
            hasPassword &&
            emailConfirmed;

        MaskedEmail =
            CanUseEmailRecovery
                ? MaskEmail(
                    email!)
                : string.Empty;
    }

    private static bool HasConfirmedPhone(
        ApplicationUser user)
    {
        return
            !string.IsNullOrWhiteSpace(
                user.PhoneNumber) &&
            user.PhoneNumberConfirmed;
    }

    private static string MaskPhoneNumber(
        string phoneNumber)
    {
        if (phoneNumber.Length <=
            5)
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
}
