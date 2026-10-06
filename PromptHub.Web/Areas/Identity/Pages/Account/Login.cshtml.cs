using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PromptHub.Web.Models.Entities;
using Microsoft.AspNetCore.RateLimiting;
using PromptHub.Web.Services.Security;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[EnableRateLimiting("IdentityPublicPost")]
public sealed class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly UserManager<ApplicationUser>
    _userManager;

    private readonly ISmartCaptchaVerificationService
        _smartCaptchaVerificationService;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ISmartCaptchaVerificationService smartCaptchaVerificationService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _smartCaptchaVerificationService =
            smartCaptchaVerificationService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(Name = "smart-token")]
    public string? SmartCaptchaToken { get; set; }

    public string ReturnUrl { get; set; } = "/";

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

        if (user is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Неверный email или пароль.");

            return Page();
        }

        var result =
            await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure:
                    true);

        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "Вход временно заблокирован из-за большого количества неудачных попыток.");

            return Page();
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                string.Empty,
                "Перед входом подтвердите email. " +
                "Ссылку можно запросить повторно.");

            return Page();
        }

        ModelState.AddModelError(
            string.Empty,
            "Неверный email или пароль.");

        return Page();
    }

    // -------------------------------------------------------------------------
    // EN: Only local return URLs may be used after authentication.
    // RU: После авторизации разрешены только локальные адреса возврата.
    // -------------------------------------------------------------------------

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
            ErrorMessage = "Введите email.")]
        [EmailAddress(
            ErrorMessage = "Введите корректный email.")]
        [Display(Name = "Email")]
        public string Email { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage = "Введите пароль.")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }
            = string.Empty;

        [Display(Name = "Запомнить меня")]
        public bool RememberMe { get; set; }
    }
}
