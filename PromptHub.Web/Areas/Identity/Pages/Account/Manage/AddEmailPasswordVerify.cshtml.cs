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
public sealed class AddEmailPasswordVerifyModel
    : PageModel
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IPhoneOtpService
        _phoneOtpService;

    private readonly IPhoneCredentialSetupContinuationProtector
        _continuationProtector;

    private readonly TimeProvider
        _timeProvider;

    public AddEmailPasswordVerifyModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IPhoneOtpService phoneOtpService,
        IPhoneCredentialSetupContinuationProtector continuationProtector,
        TimeProvider timeProvider)
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
                "./AddEmailPassword");
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
                "./AddEmailPassword");
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
                PhoneOtpPurpose.AddCredentials,
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
                StringComparison.Ordinal))
        {
            return RedirectToPage(
                "./SignInMethods");
        }

        var continuation =
            _continuationProtector.Protect(
                ChallengeId);

        return RedirectToPage(
            "./AddEmailPasswordComplete",
            new
            {
                continuation
            });
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
                        PhoneOtpPurpose.AddCredentials &&
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
