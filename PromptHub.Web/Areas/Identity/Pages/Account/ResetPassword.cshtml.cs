using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class ResetPasswordModel
    : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly ILogger<ResetPasswordModel>
        _logger;

    public ResetPasswordModel(
        UserManager<ApplicationUser> userManager,
        ILogger<ResetPasswordModel> logger)
    {
        _userManager =
            userManager;

        _logger =
            logger;
    }


    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    public bool HasResetToken { get; private set; }


    public void OnGet(
        string? code = null)
    {
        if (string.IsNullOrWhiteSpace(
                code))
        {
            HasResetToken =
                false;

            return;
        }

        Input.Code =
            code;

        HasResetToken =
            true;
    }


    public async Task<IActionResult> OnPostAsync()
    {
        HasResetToken =
            !string.IsNullOrWhiteSpace(
                Input.Code);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        string resetToken;

        try
        {
            resetToken =
                Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(
                        Input.Code));
        }
        catch (FormatException)
        {
            AddInvalidResetRequestError();

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
        // EN: Use the same generic response for an unknown or unavailable
        //     account to avoid exposing account existence.
        // RU: Используем одинаковую ошибку для неизвестного или недоступного
        //     аккаунта, чтобы не раскрывать его существование.
        // ---------------------------------------------------------------------

        if (user is null ||
            !await _userManager
                .IsEmailConfirmedAsync(
                    user))
        {
            AddInvalidResetRequestError();

            return Page();
        }

        var result =
            await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                Input.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "Password was successfully reset for user {UserId}.",
                user.Id);

            return RedirectToPage(
                "./ResetPasswordConfirmation");
        }

        // ---------------------------------------------------------------------
        // EN: An invalid or expired token must not expose internal Identity
        //     details. Password policy errors can be shown to the user.
        // RU: Недействительный или просроченный токен не должен раскрывать
        //     внутренние детали Identity. Ошибки требований к паролю можно
        //     показывать пользователю.
        // ---------------------------------------------------------------------

        if (result.Errors.Any(error =>
                string.Equals(
                    error.Code,
                    "InvalidToken",
                    StringComparison.Ordinal)))
        {
            AddInvalidResetRequestError();

            return Page();
        }

        foreach (var error in
                 result.Errors)
        {
            ModelState.AddModelError(
                nameof(Input.Password),
                GetIdentityErrorMessage(
                    error.Code,
                    error.Description));
        }

        return Page();
    }


    private void AddInvalidResetRequestError()
    {
        ModelState.AddModelError(
            string.Empty,
            "Не удалось изменить пароль. " +
            "Проверьте email и ссылку восстановления " +
            "или запросите новую ссылку.");
    }


    private static string GetIdentityErrorMessage(
        string code,
        string fallbackMessage)
    {
        return code switch
        {
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

            "PasswordRequiresUniqueChars" =>
                "Пароль должен содержать больше различных символов.",

            _ =>
                fallbackMessage
        };
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

        [Required(
            ErrorMessage =
                "Введите новый пароль.")]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage =
                "Пароль должен содержать не менее 6 символов.")]
        [DataType(
            DataType.Password)]
        [Display(
            Name =
                "Новый пароль")]
        public string Password { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Повторите новый пароль.")]
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

        [Required]
        public string Code { get; set; }
            = string.Empty;
    }
}
