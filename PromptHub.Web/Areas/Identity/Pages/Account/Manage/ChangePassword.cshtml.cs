using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    public ChangePasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user =
            await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        var hasPassword =
            await _userManager.HasPasswordAsync(user);

        if (!hasPassword)
        {
            StatusMessage =
                "Для этого аккаунта локальный пароль не установлен.";

            return RedirectToPage("./Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user =
            await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        var result =
            await _userManager.ChangePasswordAsync(
                user,
                Input.CurrentPassword,
                Input.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    GetErrorMessage(error.Code));
            }

            return Page();
        }

        user.UpdatedAtUtc =
            DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(
            user);

        StatusMessage =
            "Пароль успешно изменён.";

        return RedirectToPage();
    }

    private static string GetErrorMessage(
        string errorCode)
    {
        return errorCode switch
        {
            "PasswordMismatch" =>
                "Текущий пароль указан неверно.",

            "PasswordTooShort" =>
                "Новый пароль слишком короткий.",

            "PasswordRequiresDigit" =>
                "Новый пароль должен содержать цифру.",

            "PasswordRequiresLower" =>
                "Новый пароль должен содержать строчную букву.",

            "PasswordRequiresUpper" =>
                "Новый пароль должен содержать заглавную букву.",

            "PasswordRequiresNonAlphanumeric" =>
                "Новый пароль должен содержать специальный символ.",

            "PasswordRequiresUniqueChars" =>
                "В новом пароле недостаточно различных символов.",

            _ =>
                "Не удалось изменить пароль."
        };
    }

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите текущий пароль.")]
        [DataType(DataType.Password)]
        [Display(
            Name = "Текущий пароль")]
        public string CurrentPassword { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Введите новый пароль.")]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage =
                "Пароль должен содержать от 6 до 100 символов.")]
        [DataType(DataType.Password)]
        [Display(
            Name = "Новый пароль")]
        public string NewPassword { get; set; }
            = string.Empty;

        [Required(
            ErrorMessage =
                "Повторите новый пароль.")]
        [DataType(DataType.Password)]
        [Compare(
            nameof(NewPassword),
            ErrorMessage =
                "Новый пароль и подтверждение не совпадают.")]
        [Display(
            Name = "Повторите новый пароль")]
        public string ConfirmPassword { get; set; }
            = string.Empty;
    }
}
