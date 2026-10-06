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
using PromptHub.Web.Legal;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("IdentityPublicPost")]
public sealed class RegisterModel
    : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IEmailSender<ApplicationUser>
        _emailSender;

    private readonly ILogger<RegisterModel>
        _logger;

    private readonly ISmartCaptchaVerificationService
        _smartCaptchaVerificationService;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender,
        ILogger<RegisterModel> logger,
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
        string? returnUrl = null)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);

        // -------------------------------------------------------------------------
        // EN: Legal acceptance is validated explicitly on the server.
        // RU: Подтверждение документов обязательно проверяется на сервере.
        // -------------------------------------------------------------------------

        if (!Input.AcceptTerms)
        {
            ModelState.AddModelError(
                "Input.AcceptTerms",
                "Необходимо принять Пользовательское соглашение и Правила публикации.");
        }

        if (!Input.AcceptPersonalData)
        {
            ModelState.AddModelError(
                "Input.AcceptPersonalData",
                "Необходимо дать согласие на обработку персональных данных.");
        }

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

        var now =
            DateTime.UtcNow;

        var user =
    new ApplicationUser
    {
        UserName =
            email,

        Email =
            email,

        DisplayName =
            Input.DisplayName.Trim(),

        CreatedAtUtc =
            now,

        UpdatedAtUtc =
            now,

        TermsAcceptedAtUtc =
            now,

        TermsVersion =
            LegalDocumentVersions.Terms,

        ContentRulesAcceptedAtUtc =
            now,

        ContentRulesVersion =
            LegalDocumentVersions.ContentRules,

        PersonalDataConsentAcceptedAtUtc =
            now,

        PersonalDataConsentVersion =
            LegalDocumentVersions.PersonalDataConsent
    };

        var result =
            await _userManager.CreateAsync(
                user,
                Input.Password);

        if (!result.Succeeded)
        {
            AddIdentityErrors(
                result);

            return Page();
        }


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
                            encodedToken,

                        returnUrl =
                            ReturnUrl
                    },
                protocol:
                    Request.Scheme);

        if (string.IsNullOrWhiteSpace(
                confirmationLink))
        {
            throw new InvalidOperationException(
                "Не удалось создать ссылку подтверждения email.");
        }

        var deliveryFailed =
            false;

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
            deliveryFailed =
                true;

            _logger.LogError(
                exception,
                "Failed to send an email confirmation link to user {UserId}.",
                user.Id);
        }

        // ---------------------------------------------------------------------
        // EN: Do not sign in before email confirmation.
        // RU: Не выполняем вход до подтверждения электронной почты.
        // ---------------------------------------------------------------------

        return RedirectToPage(
            "./RegisterConfirmation",
            new
            {
                email,

                returnUrl =
                    ReturnUrl,

                deliveryFailed
            });
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


    private void AddIdentityErrors(
        IdentityResult result)
    {
        foreach (var error in
                 result.Errors)
        {
            ModelState.AddModelError(
                string.Empty,
                GetIdentityErrorMessage(
                    error.Code,
                    error.Description));
        }
    }

    private static string GetIdentityErrorMessage(
        string code,
        string fallbackMessage)
    {
        return code switch
        {
            "DuplicateEmail" or
            "DuplicateUserName" =>
                "Аккаунт с таким email уже существует.",

            "PasswordTooShort" =>
                "Пароль слишком короткий.",

            "PasswordRequiresDigit" =>
                "Пароль должен содержать хотя бы одну цифру.",

            "PasswordRequiresLower" =>
                "Пароль должен содержать строчную букву.",

            "PasswordRequiresUpper" =>
                "Пароль должен содержать заглавную букву.",

            "PasswordRequiresNonAlphanumeric" =>
                "Пароль должен содержать специальный символ.",

            _ =>
                fallbackMessage
        };
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
                "Введите имя на сайте или псевдоним.")]
        [StringLength(
            80,
            MinimumLength = 2,
            ErrorMessage =
                "Имя должно содержать от 2 до 80 символов.")]
        [Display(
            Name =
                "Имя на сайте / псевдоним")]
        public string DisplayName { get; set; }
            = string.Empty;

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

        [Required(
            ErrorMessage =
                "Введите пароль.")]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage =
                "Пароль должен содержать не менее 6 символов.")]
        [DataType(
            DataType.Password)]
        [Display(
            Name =
                "Пароль")]
        public string Password { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Повторите пароль.")]
        [DataType(
            DataType.Password)]
        [Compare(
            nameof(Password),
            ErrorMessage =
                "Пароли не совпадают.")]
        [Display(
            Name =
                "Повторите пароль")]
        public string ConfirmPassword { get; set; }
            = string.Empty;

        [Display(
    Name =
        "Пользовательское соглашение")]
        public bool AcceptTerms { get; set; }

        [Display(
            Name =
                "Согласие на обработку персональных данных")]
        public bool AcceptPersonalData { get; set; }
    }
}
