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
public sealed class ResendEmailConfirmationModel
    : PageModel
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IEmailSender<ApplicationUser>
        _emailSender;

    private readonly ILogger<
        ResendEmailConfirmationModel>
        _logger;

    private readonly ISmartCaptchaVerificationService
        _smartCaptchaVerificationService;

    public ResendEmailConfirmationModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender,
        ILogger<
            ResendEmailConfirmationModel> logger,
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

        if (user is not null &&
            !await _userManager
                .IsEmailConfirmedAsync(
                    user))
        {
            var userId =
                await _userManager.GetUserIdAsync(
                    user);

            var confirmationToken =
                await _userManager
                    .GenerateEmailConfirmationTokenAsync(
                        user);

            var encodedToken =
                WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(
                        confirmationToken));

            var confirmationLink =
                Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler:
                        null,
                    values:
                        new
                        {
                            area =
                                "Identity",

                            userId,

                            code =
                                encodedToken
                        },
                    protocol:
                        Request.Scheme);

            if (string.IsNullOrWhiteSpace(
                    confirmationLink))
            {
                throw new InvalidOperationException(
                    "Не удалось создать ссылку подтверждения email.");
            }

            try
            {
                await _emailSender
                    .SendConfirmationLinkAsync(
                        user,
                        email,
                        confirmationLink);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to resend an email confirmation link to user {UserId}.",
                    user.Id);
            }
        }

        // ---------------------------------------------------------------------
        // EN: Always show the same result to prevent account enumeration.
        // RU: Всегда показываем одинаковый результат, чтобы не раскрывать
        //     существование аккаунта.
        // ---------------------------------------------------------------------

        return RedirectToPage(
            "./RegisterConfirmation",
            new
            {
                email
            });
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
