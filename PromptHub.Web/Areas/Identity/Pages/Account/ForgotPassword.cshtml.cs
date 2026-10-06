using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using PromptHub.Web.Models.Entities;
using Microsoft.AspNetCore.RateLimiting;
using PromptHub.Web.Services.Security;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("IdentityPublicPost")]
public sealed class ForgotPasswordModel
    : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IEmailSender<ApplicationUser>
        _emailSender;

    private readonly ILogger<ForgotPasswordModel>
        _logger;

    private readonly ISmartCaptchaVerificationService
        _smartCaptchaVerificationService;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender,
        ILogger<ForgotPasswordModel> logger,
        ISmartCaptchaVerificationService smartCaptchaVerificationService)
    {
        _userManager =
            userManager;

        _emailSender =
            emailSender;

        _logger =
            logger;

        _smartCaptchaVerificationService =
            smartCaptchaVerificationService;
    }


    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    [BindProperty(Name = "smart-token")]
    public string? SmartCaptchaToken { get; set; }

    public void OnGet()
    {
    }


    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!await VerifySmartCaptchaAsync(
                HttpContext.RequestAborted))
        {
            return Page();
        }

        var email =
            Input.Email
                .Trim()
                .ToLowerInvariant();

        var user =
            await _userManager.FindByEmailAsync(
                email);

        // ---------------------------------------------------------------------
        // EN: Do not reveal whether the account exists or whether its email
        //     address has been confirmed.
        // RU: Не раскрываем существование аккаунта и состояние подтверждения
        //     его электронной почты.
        // ---------------------------------------------------------------------

        if (user is null ||
            !await _userManager
                .IsEmailConfirmedAsync(
                    user))
        {
            return RedirectToPage(
                "./ForgotPasswordConfirmation");
        }

        var resetToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(
                    user);

        var encodedToken =
            WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(
                    resetToken));

        var resetLink =
            Url.Page(
                "/Account/ResetPassword",
                pageHandler:
                    null,
                values:
                    new
                    {
                        area =
                            "Identity",

                        code =
                            encodedToken
                    },
                protocol:
                    Request.Scheme);

        if (string.IsNullOrWhiteSpace(
                resetLink))
        {
            throw new InvalidOperationException(
                "Не удалось создать ссылку восстановления пароля.");
        }

        try
        {
            await _emailSender
                .SendPasswordResetLinkAsync(
                    user,
                    email,
                    resetLink);
        }
        catch (Exception exception)
        {
            // -----------------------------------------------------------------
            // EN: Do not expose mail delivery failures to anonymous callers.
            // RU: Не раскрываем анонимному пользователю ошибки доставки почты.
            // -----------------------------------------------------------------

            _logger.LogError(
                exception,
                "Failed to send a password reset link to user {UserId}.",
                user.Id);
        }

        return RedirectToPage(
            "./ForgotPasswordConfirmation");
    }


    private async Task<bool> VerifySmartCaptchaAsync(
        CancellationToken cancellationToken)
    {
        var isValid =
            await _smartCaptchaVerificationService
                .VerifyAsync(
                    SmartCaptchaToken,
                    HttpContext.Connection.RemoteIpAddress,
                    cancellationToken);

        if (isValid)
        {
            return true;
        }

        ModelState.AddModelError(
            string.Empty,
            "Не удалось подтвердить, что запрос отправил человек. Повторите проверку и попробуйте ещё раз.");

        return false;
    }

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите email.")]
        [EmailAddress(
            ErrorMessage =
                "Введите корректный email.")]
        [Display(
            Name =
                "Email")]
        public string Email { get; set; }
            = string.Empty;
    }
}
