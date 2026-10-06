using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class ChangePhoneVerifyNewModel
    : PageModel
{

    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly IPhoneOtpService
        _phoneOtpService;

    private readonly IPhoneChangeContinuationProtector
        _continuationProtector;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<ChangePhoneVerifyNewModel>
        _logger;

    public ChangePhoneVerifyNewModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPhoneOtpService phoneOtpService,
        IPhoneChangeContinuationProtector continuationProtector,
        TimeProvider timeProvider,
        ILogger<ChangePhoneVerifyNewModel> logger)
    {
        _dbContext =
            dbContext;

        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _phoneOtpService =
            phoneOtpService;

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
    public Guid ChallengeId { get; set; }

    [BindProperty]
    public string Continuation { get; set; }
        = string.Empty;

    public string MaskedNewPhoneNumber { get; private set; }
        = string.Empty;


    public async Task<IActionResult> OnGetAsync(
        Guid challengeId,
        string? continuation,
        CancellationToken cancellationToken)
    {
        ChallengeId =
            challengeId;

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

        var challenge =
            await LoadChallengeAsync(
                user,
                cancellationToken);

        if (challenge is null)
        {
            return RedirectToPage(
                "./ChangePhoneNew",
                new
                {
                    continuation =
                        Continuation
                });
        }

        MaskedNewPhoneNumber =
            MaskPhoneNumber(
                challenge.PhoneNumber);

        return Page();
    }

    // -------------------------------------------------------------------------
    // EN: Verify new phone and atomically replace the authentication phone.
    // RU: Подтверждаем новый номер и заменяем номер аутентификации.
    // -------------------------------------------------------------------------

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

        var challenge =
            await LoadChallengeAsync(
                user,
                cancellationToken);

        if (challenge is null)
        {
            return RedirectToPage(
                "./ChangePhoneNew",
                new
                {
                    continuation =
                        Continuation
                });
        }

        MaskedNewPhoneNumber =
            MaskPhoneNumber(
                challenge.PhoneNumber);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var verification =
            await _phoneOtpService.VerifyAsync(
                ChallengeId,
                PhoneOtpPurpose.ChangePhoneNew,
                Input.Code,
                cancellationToken);

        switch (verification.Status)
        {
            case PhoneOtpVerificationStatus.InvalidCode:
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.Code)}",
                    "Неверный код.");

                return Page();

            case PhoneOtpVerificationStatus.Expired:
                ModelState.AddModelError(
                    string.Empty,
                    "Срок действия кода истёк. Запросите новый.");

                return Page();

            case PhoneOtpVerificationStatus.AttemptsExceeded:
                ModelState.AddModelError(
                    string.Empty,
                    "Превышено количество попыток. Запросите новый код.");

                return Page();

            case PhoneOtpVerificationStatus.Unavailable:
                ModelState.AddModelError(
                    string.Empty,
                    "Этот код больше недоступен.");

                return Page();

            case PhoneOtpVerificationStatus.Succeeded:
                break;

            default:
                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось проверить код.");

                return Page();
        }

        if (!string.Equals(
                verification.UserId,
                user.Id,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(
                verification.PhoneNumber))
        {
            _logger.LogWarning(
                "New phone challenge {ChallengeId} failed ownership validation for user {UserId}.",
                ChallengeId,
                user.Id);

            return RedirectToPage(
                "./SignInMethods");
        }

        var newPhoneNumber =
            verification.PhoneNumber;

        // ---------------------------------------------------------------------
        // EN: Re-check authorization after OTP verification because the
        //     account could have changed while the user entered the code.
        // RU: Повторно проверяем authorization после OTP — состояние аккаунта
        //     могло измениться, пока пользователь вводил код.
        // ---------------------------------------------------------------------

        authorization =
            await ValidateAuthorizationAsync(
                user);

        if (authorization is null)
        {
            return RedirectToPage(
                "./ChangePhone");
        }

        if (string.Equals(
                newPhoneNumber,
                authorization.CurrentPhoneNumber,
                StringComparison.Ordinal))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        // ---------------------------------------------------------------------
        // EN: Friendly pre-check. The database unique index remains the
        //     authoritative protection against races.
        // RU: Предварительная проверка для нормального UX.
        //     Источником истины всё равно остаётся UNIQUE-индекс БД.
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
                            newPhoneNumber,
                    cancellationToken);

        if (phoneAlreadyUsed)
        {
            ModelState.AddModelError(
                string.Empty,
                "Этот номер уже используется другим аккаунтом.");

            return Page();
        }

        await using var transaction =
            await _dbContext
                .Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var nowUtc =
                _timeProvider
                    .GetUtcNow()
                    .UtcDateTime;


            user.PhoneNumber =
                newPhoneNumber;

            user.PhoneNumberConfirmed =
                true;

            user.UpdatedAtUtc =
                nowUtc;

            var updateResult =
                await _userManager.UpdateAsync(
                    user);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    "Failed to replace phone for user {UserId}. Errors: {Errors}",
                    user.Id,
                    string.Join(
                        ", ",
                        updateResult.Errors.Select(
                            error =>
                                error.Code)));

                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось изменить номер телефона.");

                return Page();
            }

            // -----------------------------------------------------------------
            // EN: Invalidate older authenticated sessions after changing
            //     a sensitive authentication method.
            // RU: После изменения способа входа инвалидируем старые сессии.
            // -----------------------------------------------------------------

            var stampResult =
                await _userManager
                    .UpdateSecurityStampAsync(
                        user);

            if (!stampResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    "Failed to update security stamp after phone replacement for user {UserId}.",
                    user.Id);

                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось завершить смену номера телефона.");

                return Page();
            }

            // -----------------------------------------------------------------
            // EN: Invalidate any remaining active OTP challenges owned by
            //     this account.
            // RU: Инвалидируем оставшиеся активные OTP этого аккаунта.
            // -----------------------------------------------------------------

            await _dbContext
                .PhoneOtpChallenges
                .Where(otp =>
                    otp.UserId ==
                        user.Id &&
                    otp.ConsumedAtUtc ==
                        null &&
                    otp.InvalidatedAtUtc ==
                        null)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            otp =>
                                otp.InvalidatedAtUtc,
                            nowUtc),
                    cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Database rejected phone replacement for user {UserId}.",
                user.Id);

            ModelState.AddModelError(
                string.Empty,
                "Этот номер уже используется другим аккаунтом.");

            return Page();
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _logger.LogError(
                exception,
                "Unexpected phone replacement failure for user {UserId}.",
                user.Id);

            ModelState.AddModelError(
                string.Empty,
                "Не удалось завершить смену номера телефона.");

            return Page();
        }

        // ---------------------------------------------------------------------
        // EN: SecurityStamp changed, therefore refresh the current session.
        // RU: SecurityStamp изменился — обновляем текущую сессию.
        // ---------------------------------------------------------------------

        await _signInManager.RefreshSignInAsync(
            user);

        TempData["StatusMessage"] =
            "Номер телефона успешно изменён.";

        return RedirectToPage(
            "./SignInMethods");
    }

    // -------------------------------------------------------------------------
    // EN: Verify the protected authorization issued after old-account proof.
    // RU: Проверяем защищённое разрешение, полученное после подтверждения
    //     владельца старого аккаунта.
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

    private async Task<PhoneOtpChallenge?>
        LoadChallengeAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
    {
        if (ChallengeId ==
            Guid.Empty)
        {
            return null;
        }

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        return await _dbContext
            .PhoneOtpChallenges
            .AsNoTracking()
            .SingleOrDefaultAsync(
                challenge =>
                    challenge.Id ==
                        ChallengeId &&
                    challenge.UserId ==
                        user.Id &&
                    challenge.Purpose ==
                        PhoneOtpPurpose.ChangePhoneNew &&
                    challenge.PhoneNumber !=
                        user.PhoneNumber &&
                    challenge.ConsumedAtUtc ==
                        null &&
                    challenge.InvalidatedAtUtc ==
                        null &&
                    challenge.ExpiresAtUtc >
                        nowUtc,
                cancellationToken);
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
