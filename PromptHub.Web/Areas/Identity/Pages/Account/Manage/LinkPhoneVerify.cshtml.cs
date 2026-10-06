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
using Npgsql;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class LinkPhoneVerifyModel
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

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<LinkPhoneVerifyModel>
        _logger;

    public LinkPhoneVerifyModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IPhoneOtpService phoneOtpService,
        TimeProvider timeProvider,
        ILogger<LinkPhoneVerifyModel> logger)
    {
        _dbContext =
            dbContext;

        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _phoneOtpService =
            phoneOtpService;

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

    public string MaskedPhoneNumber { get; private set; }
        = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        ChallengeId =
            challengeId;

        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        var challenge =
            await LoadChallengeAsync(
                user.Id,
                cancellationToken);

        if (challenge is null)
        {
            return RedirectToPage(
                "./LinkPhone");
        }

        MaskedPhoneNumber =
            MaskPhoneNumber(
                challenge.PhoneNumber);

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

        var challenge =
            await LoadChallengeAsync(
                user.Id,
                cancellationToken);

        if (challenge is null)
        {
            return RedirectToPage(
                "./LinkPhone");
        }

        MaskedPhoneNumber =
            MaskPhoneNumber(
                challenge.PhoneNumber);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var verification =
            await _phoneOtpService
                .VerifyAsync(
                    ChallengeId,
                    PhoneOtpPurpose.LinkPhone,
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

        // ---------------------------------------------------------------------
        // EN: Challenge belongs only to the authenticated account that created it.
        // RU: Challenge принадлежит только аккаунту, который его создал.
        // ---------------------------------------------------------------------

        if (!string.Equals(
                verification.UserId,
                user.Id,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(
                verification.PhoneNumber))
        {
            _logger.LogWarning(
                "Phone linking challenge {ChallengeId} has invalid ownership.",
                ChallengeId);

            return RedirectToPage(
                "./SignInMethods");
        }

        var phoneNumber =
            verification.PhoneNumber;

        // ---------------------------------------------------------------------
        // EN: We currently support initial linking only, not phone replacement.
        // RU: Сейчас поддерживается только первичная привязка, не замена номера.
        // ---------------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
                user.PhoneNumber))
        {
            TempData["StatusMessage"] =
                "К аккаунту уже привязан номер телефона.";

            return RedirectToPage(
                "./SignInMethods");
        }

        var phoneAlreadyUsed =
            await _userManager
                .Users
                .AsNoTracking()
                .AnyAsync(
                    otherUser =>
                        otherUser.Id !=
                            user.Id &&
                        otherUser.PhoneNumber ==
                            phoneNumber,
                    cancellationToken);

        if (phoneAlreadyUsed)
        {
            ModelState.AddModelError(
                string.Empty,
                "Этот номер нельзя привязать к текущему аккаунту.");

            return Page();
        }



        // -------------------------------------------------------------------------
        // EN: Phone linking and security-stamp rotation must succeed atomically.
        // RU: Привязка телефона и смена SecurityStamp должны пройти атомарно.
        // -------------------------------------------------------------------------

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
                phoneNumber;

            user.PhoneNumberConfirmed =
                true;

            user.UpdatedAtUtc =
                nowUtc;

            var updateResult =
                await _userManager
                    .UpdateAsync(
                        user);

            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    "Failed to link phone to user {UserId}. Errors: {Errors}",
                    user.Id,
                    string.Join(
                        ", ",
                        updateResult.Errors.Select(
                            error =>
                                error.Code)));

                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось привязать номер.");

                return Page();
            }

            // ---------------------------------------------------------------------
            // EN: Linking a new authentication method invalidates older sessions.
            // RU: Добавление нового способа входа инвалидирует старые сессии.
            // ---------------------------------------------------------------------

            var securityStampResult =
                await _userManager
                    .UpdateSecurityStampAsync(
                        user);

            if (!securityStampResult.Succeeded)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    "Failed to update security stamp while linking phone for user {UserId}. Errors: {Errors}",
                    user.Id,
                    string.Join(
                        ", ",
                        securityStampResult.Errors.Select(
                            error =>
                                error.Code)));

                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось завершить привязку номера.");

                return Page();
            }

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(
                exception))
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _logger.LogWarning(
                exception,
                "Phone {PhoneNumber} was claimed concurrently while linking it to user {UserId}.",
                phoneNumber,
                user.Id);

            ModelState.AddModelError(
                string.Empty,
                "Этот номер нельзя привязать к текущему аккаунту.");

            return Page();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            _logger.LogError(
                exception,
                "Unexpected phone linking failure for user {UserId}.",
                user.Id);

            ModelState.AddModelError(
                string.Empty,
                "Не удалось завершить привязку номера.");

            return Page();
        }

        // -------------------------------------------------------------------------
        // EN: SecurityStamp has changed, refresh only the current session.
        // RU: SecurityStamp изменён — обновляем только текущую сессию.
        // -------------------------------------------------------------------------

        await _signInManager
            .RefreshSignInAsync(
                user);

        TempData["StatusMessage"] =
            "Номер телефона успешно привязан.";

        return RedirectToPage(
            "./SignInMethods");
    }

    private async Task<PhoneOtpChallenge?>
        LoadChallengeAsync(
            string userId,
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
                        userId &&
                    challenge.Purpose ==
                        PhoneOtpPurpose.LinkPhone &&
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

    private static bool IsUniqueViolation(
    DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException postgresException &&
            postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation;
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
