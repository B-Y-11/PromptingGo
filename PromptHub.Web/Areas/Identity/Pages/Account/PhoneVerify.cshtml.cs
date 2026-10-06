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

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class PhoneVerifyModel
    : PageModel
{

    private readonly IPhoneOtpService
        _phoneOtpService;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly ApplicationDbContext
        _dbContext;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<PhoneVerifyModel>
        _logger;

    private readonly IPhoneRegistrationContinuationProtector
    _continuationProtector;

    public PhoneVerifyModel(
        IPhoneOtpService phoneOtpService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<PhoneVerifyModel> logger,
        IPhoneRegistrationContinuationProtector continuationProtector)
    {
        _phoneOtpService =
            phoneOtpService;

        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _dbContext =
            dbContext;

        _timeProvider =
            timeProvider;

        _logger =
            logger;
        _continuationProtector = continuationProtector;
    }


    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    [BindProperty]
    public Guid ChallengeId { get; set; }

    public string ReturnUrl { get; private set; }
        = "/";

    public string MaskedPhoneNumber { get; private set; }
        = string.Empty;


    public async Task<IActionResult> OnGetAsync(
        Guid challengeId,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);

        ChallengeId =
            challengeId;

        if (ChallengeId ==
            Guid.Empty)
        {
            return RedirectToPage(
                "./Phone",
                new
                {
                    returnUrl =
                        ReturnUrl
                });
        }

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.Id ==
                            ChallengeId &&
                        item.Purpose ==
                            PhoneOtpPurpose.Authentication,
                    cancellationToken);

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        if (challenge is null ||
            challenge.ConsumedAtUtc.HasValue ||
            challenge.InvalidatedAtUtc.HasValue ||
            challenge.ExpiresAtUtc <=
                nowUtc)
        {
            return RedirectToPage(
                "./Phone",
                new
                {
                    returnUrl =
                        ReturnUrl
                });
        }

        MaskedPhoneNumber =
            MaskPhoneNumber(
                challenge.PhoneNumber);

        return Page();
    }


    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);

        if (ChallengeId ==
            Guid.Empty)
        {
            return RedirectToPage(
                "./Phone",
                new
                {
                    returnUrl =
                        ReturnUrl
                });
        }

        await LoadMaskedPhoneNumberAsync(
            cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var verificationResult =
            await _phoneOtpService
                .VerifyAsync(
                    ChallengeId,
                    PhoneOtpPurpose.Authentication,
                    Input.Code,
                    cancellationToken);

        switch (verificationResult.Status)
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
                    "Этот код больше недоступен. Запросите новый.");

                return Page();

            case PhoneOtpVerificationStatus.Succeeded:
                break;

            default:
                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось проверить код.");

                return Page();
        }

        var phoneNumber =
            verificationResult.PhoneNumber;

        if (string.IsNullOrWhiteSpace(
                phoneNumber))
        {
            _logger.LogError(
                "Successful phone OTP challenge {ChallengeId} returned no phone number.",
                ChallengeId);

            ModelState.AddModelError(
                string.Empty,
                "Не удалось завершить вход.");

            return Page();
        }


        var existingUser =
            await _userManager
                .Users
                .SingleOrDefaultAsync(
                    user =>
                        user.PhoneNumber ==
                            phoneNumber,
                    cancellationToken);

        if (existingUser is not null)
        {
            if (await _userManager
                    .IsLockedOutAsync(
                        existingUser))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Вход в аккаунт временно заблокирован. Попробуйте позже.");

                return Page();
            }

            if (!existingUser
                    .PhoneNumberConfirmed)
            {
                existingUser.PhoneNumberConfirmed =
                    true;

                existingUser.UpdatedAtUtc =
                    _timeProvider
                        .GetUtcNow()
                        .UtcDateTime;

                var updateResult =
                    await _userManager
                        .UpdateAsync(
                            existingUser);

                if (!updateResult.Succeeded)
                {
                    _logger.LogError(
                        "Failed to confirm phone number for user {UserId}. Errors: {Errors}",
                        existingUser.Id,
                        string.Join(
                            ", ",
                            updateResult.Errors.Select(
                                error =>
                                    error.Code)));

                    ModelState.AddModelError(
                        string.Empty,
                        "Не удалось завершить вход.");

                    return Page();
                }
            }

            // -----------------------------------------------------------------
            // EN: Associate the successful challenge with the authenticated user.
            // RU: Связываем успешный challenge с найденным пользователем.
            // -----------------------------------------------------------------

            await _dbContext
                .PhoneOtpChallenges
                .Where(challenge =>
                    challenge.Id ==
                        ChallengeId)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            challenge =>
                                challenge.UserId,
                            existingUser.Id),
                    cancellationToken);

            await _signInManager
                .SignInAsync(
                    existingUser,
                    isPersistent:
                        false);

            return LocalRedirect(
                ReturnUrl);
        }

        // ---------------------------------------------------------------------
        // EN: New phone -> continue to account creation.
        //     Only the protected TempData cookie carries the proof forward.
        // RU: Новый телефон -> переходим к созданию аккаунта.
        //     Подтверждение передаём через защищённый TempData.
        // ---------------------------------------------------------------------

        var continuation =
    _continuationProtector.Protect(
        ChallengeId);

        return RedirectToPage(
            "./PhoneCompleteRegistration",
            new
            {
                continuation,
                returnUrl =
                    ReturnUrl
            });
    }


    private async Task LoadMaskedPhoneNumberAsync(
        CancellationToken cancellationToken)
    {
        var phoneNumber =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .Where(challenge =>
                    challenge.Id ==
                        ChallengeId)
                .Select(challenge =>
                    challenge.PhoneNumber)
                .SingleOrDefaultAsync(
                    cancellationToken);

        MaskedPhoneNumber =
            string.IsNullOrWhiteSpace(
                phoneNumber)
                ? string.Empty
                : MaskPhoneNumber(
                    phoneNumber);
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

    private static string MaskPhoneNumber(
        string phoneNumber)
    {
        if (phoneNumber.Length <=
            5)
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
