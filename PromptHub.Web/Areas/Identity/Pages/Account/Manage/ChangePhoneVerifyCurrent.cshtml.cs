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
public sealed class ChangePhoneVerifyCurrentModel
    : PageModel
{

    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IPhoneOtpService
        _phoneOtpService;

    private readonly IPhoneChangeContinuationProtector
        _continuationProtector;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<ChangePhoneVerifyCurrentModel>
        _logger;

    public ChangePhoneVerifyCurrentModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IPhoneOtpService phoneOtpService,
        IPhoneChangeContinuationProtector continuationProtector,
        TimeProvider timeProvider,
        ILogger<ChangePhoneVerifyCurrentModel> logger)
    {
        _dbContext =
            dbContext;

        _userManager =
            userManager;

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
                user,
                cancellationToken);

        if (challenge is null)
        {
            return RedirectToPage(
                "./ChangePhone");
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
                user,
                cancellationToken);

        if (challenge is null)
        {
            return RedirectToPage(
                "./ChangePhone");
        }

        MaskedPhoneNumber =
            MaskPhoneNumber(
                challenge.PhoneNumber);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var verification =
            await _phoneOtpService.VerifyAsync(
                ChallengeId,
                PhoneOtpPurpose.ChangePhoneCurrent,
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
        // EN: The verified OTP must belong to this user and their current phone.
        // RU: Подтверждённый OTP должен принадлежать этому пользователю
        //     и его текущему номеру.
        // ---------------------------------------------------------------------

        if (!string.Equals(
                verification.UserId,
                user.Id,
                StringComparison.Ordinal) ||
            !string.Equals(
                verification.PhoneNumber,
                user.PhoneNumber,
                StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Current phone verification challenge {ChallengeId} failed ownership validation for user {UserId}.",
                ChallengeId,
                user.Id);

            return RedirectToPage(
                "./SignInMethods");
        }

        var securityStamp =
            await _userManager.GetSecurityStampAsync(
                user);

        if (string.IsNullOrWhiteSpace(
                securityStamp) ||
            string.IsNullOrWhiteSpace(
                user.PhoneNumber))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        // ---------------------------------------------------------------------
        // EN: Issue short-lived authorization to proceed with phone replacement.
        // RU: Выдаём краткоживущее разрешение на продолжение смены номера.
        // ---------------------------------------------------------------------

        var authorization =
            new PhoneChangeAuthorization(
                user.Id,
                user.PhoneNumber,
                securityStamp);

        var continuation =
            _continuationProtector.Protect(
                authorization);

        return RedirectToPage(
            "./ChangePhoneNew",
            new
            {
                continuation
            });
    }

    private async Task<PhoneOtpChallenge?>
        LoadChallengeAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
    {
        if (ChallengeId ==
            Guid.Empty ||
            string.IsNullOrWhiteSpace(
                user.PhoneNumber))
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
                    challenge.PhoneNumber ==
                        user.PhoneNumber &&
                    challenge.Purpose ==
                        PhoneOtpPurpose.ChangePhoneCurrent &&
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
