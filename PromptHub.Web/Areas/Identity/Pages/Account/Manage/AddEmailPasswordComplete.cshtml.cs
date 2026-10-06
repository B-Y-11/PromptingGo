using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class AddEmailPasswordCompleteModel
    : PageModel
{
    private static readonly TimeSpan
        CompletionLifetime =
            TimeSpan.FromMinutes(
                10);

    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly IEmailSender<ApplicationUser>
        _emailSender;

    private readonly IPhoneCredentialSetupContinuationProtector
        _continuationProtector;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<AddEmailPasswordCompleteModel>
        _logger;

    public AddEmailPasswordCompleteModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender<ApplicationUser> emailSender,
        IPhoneCredentialSetupContinuationProtector continuationProtector,
        TimeProvider timeProvider,
        ILogger<AddEmailPasswordCompleteModel> logger)
    {
        _dbContext =
            dbContext;

        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _emailSender =
            emailSender;

        _continuationProtector =
            continuationProtector;

        _timeProvider =
            timeProvider;

        _logger =
            logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    [BindProperty]
    public string Continuation { get; set; }
        = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        string? continuation,
        CancellationToken cancellationToken)
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

        if (!await CanCompleteAsync(
                user,
                cancellationToken))
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

        if (!await CanCompleteAsync(
                user,
                cancellationToken))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email =
            Input.Email
                .Trim()
                .ToLowerInvariant();

        var existingUser =
            await _userManager.FindByEmailAsync(
                email);

        if (existingUser is not null &&
            existingUser.Id != user.Id)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Email)}",
                "Этот email уже используется другим аккаунтом.");

            return Page();
        }

        if (!_continuationProtector.TryUnprotect(
                Continuation,
                out var challengeId))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var nowUtc =
                _timeProvider
                    .GetUtcNow()
                    .UtcDateTime;

            var minimumConsumedAtUtc =
                nowUtc -
                CompletionLifetime;

            // -----------------------------------------------------------------
            // EN: Claim the verified OTP continuation exactly once.
            //     Rollback restores it if credential setup fails.
            // RU: Одноразово погашаем подтверждение OTP.
            //     При ошибке транзакция вернёт его в исходное состояние.
            // -----------------------------------------------------------------

            var claimedRows =
                await _dbContext
                    .PhoneOtpChallenges
                    .Where(challenge =>
                        challenge.Id ==
                            challengeId &&
                        challenge.UserId ==
                            user.Id &&
                        challenge.Purpose ==
                            PhoneOtpPurpose.AddCredentials &&
                        challenge.ConsumedAtUtc !=
                            null &&
                        challenge.ConsumedAtUtc >=
                            minimumConsumedAtUtc &&
                        challenge.InvalidatedAtUtc ==
                            null)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                challenge =>
                                    challenge.InvalidatedAtUtc,
                                nowUtc),
                        cancellationToken);

            if (claimedRows != 1)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return RedirectToPage(
                    "./SignInMethods");
            }

            var currentEmail =
                await _userManager.GetEmailAsync(
                    user);

            var hasPassword =
                await _userManager.HasPasswordAsync(
                    user);

            if (!string.IsNullOrWhiteSpace(
                    currentEmail) ||
                hasPassword)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return RedirectToPage(
                    "./SignInMethods");
            }

            var emailResult =
                await _userManager.SetEmailAsync(
                    user,
                    email);

            if (!emailResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                AddIdentityErrors(
                    emailResult);

                return Page();
            }

            user.EmailConfirmed =
                false;

            var passwordResult =
                await _userManager.AddPasswordAsync(
                    user,
                    Input.Password);

            if (!passwordResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                AddIdentityErrors(
                    passwordResult);

                return Page();
            }

            user.UpdatedAtUtc =
                nowUtc;

            var updateResult =
                await _userManager.UpdateAsync(
                    user);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                AddIdentityErrors(
                    updateResult);

                return Page();
            }

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Database rejected email credential setup for user {UserId}.",
                user.Id);

            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Email)}",
                "Этот email уже используется другим аккаунтом.");

            return Page();
        }

        await _signInManager.RefreshSignInAsync(
            user);

        // ---------------------------------------------------------------------
        // EN: Email becomes a usable password-login identifier only
        //     after its confirmation.
        // RU: Email станет доступен для входа по паролю только после
        //     подтверждения.
        // ---------------------------------------------------------------------

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

                        userId =
                            user.Id,

                        code =
                            encodedToken,

                        returnUrl =
                            Url.Content(
                                "~/Identity/Account/Manage/SignInMethods")
                    },
                protocol:
                    Request.Scheme);

        var deliveryFailed =
            false;

        try
        {
            if (string.IsNullOrWhiteSpace(
                    confirmationLink))
            {
                throw new InvalidOperationException(
                    "Не удалось создать ссылку подтверждения email.");
            }

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
                "Failed to send confirmation email after credential setup for user {UserId}.",
                user.Id);
        }

        TempData["StatusMessage"] =
            deliveryFailed
                ? "Email и пароль добавлены, но письмо подтверждения отправить не удалось. Запросите ссылку повторно."
                : "Email и пароль добавлены. Подтвердите email по отправленной ссылке.";

        return RedirectToPage(
            "./SignInMethods");
    }

    private async Task<bool> CanCompleteAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        if (!_continuationProtector.TryUnprotect(
                Continuation,
                out var challengeId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                user.PhoneNumber) ||
            !user.PhoneNumberConfirmed)
        {
            return false;
        }

        var email =
            await _userManager.GetEmailAsync(
                user);

        var hasPassword =
            await _userManager.HasPasswordAsync(
                user);

        if (!string.IsNullOrWhiteSpace(
                email) ||
            hasPassword)
        {
            return false;
        }

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var minimumConsumedAtUtc =
            nowUtc -
            CompletionLifetime;

        return await _dbContext
            .PhoneOtpChallenges
            .AsNoTracking()
            .AnyAsync(
                challenge =>
                    challenge.Id ==
                        challengeId &&
                    challenge.UserId ==
                        user.Id &&
                    challenge.PhoneNumber ==
                        user.PhoneNumber &&
                    challenge.Purpose ==
                        PhoneOtpPurpose.AddCredentials &&
                    challenge.ConsumedAtUtc !=
                        null &&
                    challenge.ConsumedAtUtc >=
                        minimumConsumedAtUtc &&
                    challenge.InvalidatedAtUtc ==
                        null,
                cancellationToken);
    }

    private void AddIdentityErrors(
        IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            var message =
                error.Code switch
                {
                    "DuplicateEmail" =>
                        "Этот email уже используется другим аккаунтом.",

                    "InvalidEmail" =>
                        "Введите корректный email.",

                    "PasswordTooShort" =>
                        "Пароль слишком короткий.",

                    "PasswordRequiresDigit" =>
                        "Пароль должен содержать цифру.",

                    "PasswordRequiresLower" =>
                        "Пароль должен содержать строчную букву.",

                    "PasswordRequiresUpper" =>
                        "Пароль должен содержать заглавную букву.",

                    "PasswordRequiresNonAlphanumeric" =>
                        "Пароль должен содержать специальный символ.",

                    "PasswordRequiresUniqueChars" =>
                        "В пароле недостаточно различных символов.",

                    _ =>
                        "Не удалось сохранить данные для входа."
                };

            ModelState.AddModelError(
                string.Empty,
                message);
        }
    }

    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите email.")]
        [EmailAddress(
            ErrorMessage =
                "Введите корректный email.")]
        [StringLength(
            256)]
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
                "Пароль должен содержать от 6 до 100 символов.")]
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
    }
}
