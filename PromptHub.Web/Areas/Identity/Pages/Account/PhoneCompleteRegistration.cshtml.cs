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
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using PromptHub.Web.Legal;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("IdentityPublicPost")]
public sealed class PhoneCompleteRegistrationModel
    : PageModel
{
    private static readonly TimeSpan
        RegistrationCompletionLifetime =
            TimeSpan.FromMinutes(
                10);


    private readonly ApplicationDbContext
        _dbContext;

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<
        PhoneCompleteRegistrationModel>
        _logger;

    private readonly IPhoneRegistrationContinuationProtector
    _continuationProtector;

    public PhoneCompleteRegistrationModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TimeProvider timeProvider,
        ILogger<
            PhoneCompleteRegistrationModel> logger,
        IPhoneRegistrationContinuationProtector continuationProtector)
    {
        _dbContext =
            dbContext;

        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _timeProvider =
            timeProvider;

        _logger =
            logger;
        _continuationProtector = continuationProtector;
    }


    [BindProperty]
    public string Continuation { get; set; }
    = string.Empty;


    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    public string ReturnUrl { get; private set; }
        = "/";

    public string MaskedPhoneNumber { get; private set; }
        = string.Empty;


    public async Task<IActionResult> OnGetAsync(
    string? continuation = null,
    string? returnUrl = null,
    CancellationToken cancellationToken = default)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);

        Continuation =
            continuation ??
            string.Empty;

        var challenge =
            await GetRegistrationChallengeAsync(
                cancellationToken);

        if (challenge is null)
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

    // -------------------------------------------------------------------------
    // EN: Create a passwordless phone-only Identity account.
    // RU: Создаём Identity-аккаунт только с телефоном, без пароля.
    // -------------------------------------------------------------------------

    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ReturnUrl =
            ResolveReturnUrl(
                returnUrl);

        var challenge =
            await GetRegistrationChallengeAsync(
                cancellationToken);

        if (challenge is null)
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

        await using var transaction =
            await _dbContext
                .Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            // -----------------------------------------------------------------
            // EN: Re-read the challenge inside the transaction.
            // RU: Повторно проверяем challenge внутри транзакции.
            // -----------------------------------------------------------------

            var nowUtc =
                _timeProvider
                    .GetUtcNow()
                    .UtcDateTime;

            var minimumConsumedAtUtc =
                nowUtc -
                RegistrationCompletionLifetime;

            var currentChallenge =
                await _dbContext
                    .PhoneOtpChallenges
                    .SingleOrDefaultAsync(
                        item =>
                            item.Id ==
                                challenge.Id &&
                            item.Purpose ==
                                PhoneOtpPurpose.Authentication &&
                            item.ConsumedAtUtc !=
                                null &&
                            item.ConsumedAtUtc >=
                                minimumConsumedAtUtc &&
                            item.InvalidatedAtUtc ==
                                null &&
                            item.UserId ==
                                null,
                        cancellationToken);

            if (currentChallenge is null)
            {
                await transaction
                    .RollbackAsync(
                        cancellationToken);

                return RedirectToPage(
                    "./Phone",
                    new
                    {
                        returnUrl =
                            ReturnUrl
                    });
            }

            // -----------------------------------------------------------------
            // EN: A concurrent request may already have created the account.
            // RU: Параллельный запрос мог уже создать этот аккаунт.
            // -----------------------------------------------------------------

            var existingUser =
                await _userManager
                    .Users
                    .SingleOrDefaultAsync(
                        user =>
                            user.PhoneNumber ==
                                currentChallenge.PhoneNumber,
                        cancellationToken);

            if (existingUser is not null)
            {

                if (await _userManager
        .IsLockedOutAsync(
            existingUser))
                {
                    await transaction
                        .RollbackAsync(
                            cancellationToken);

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
                        nowUtc;

                    var updateResult =
                        await _userManager
                            .UpdateAsync(
                                existingUser);

                    if (!updateResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Unable to confirm the phone number of an existing user.");
                    }
                }

                currentChallenge.UserId =
                    existingUser.Id;

                await _dbContext
                    .SaveChangesAsync(
                        cancellationToken);

                await transaction
                    .CommitAsync(
                        cancellationToken);


                await _signInManager
                    .SignInAsync(
                        existingUser,
                        isPersistent:
                            false);

                return LocalRedirect(
                    ReturnUrl);
            }

            // -----------------------------------------------------------------
            // EN: UserName is an internal non-PII identifier for phone accounts.
            // RU: UserName для телефонного аккаунта — внутренний идентификатор
            //     без номера телефона и другой персональной информации.
            // -----------------------------------------------------------------

            var user =
    new ApplicationUser
    {
        UserName =
            $"phone_{currentChallenge.Id:N}",

        PhoneNumber =
            currentChallenge.PhoneNumber,

        PhoneNumberConfirmed =
            true,

        DisplayName =
            Input.DisplayName.Trim(),

        CreatedAtUtc =
            nowUtc,

        UpdatedAtUtc =
            nowUtc,

        TermsAcceptedAtUtc =
            nowUtc,

        TermsVersion =
            LegalDocumentVersions.Terms,

        ContentRulesAcceptedAtUtc =
            nowUtc,

        ContentRulesVersion =
            LegalDocumentVersions.ContentRules,

        PersonalDataConsentAcceptedAtUtc =
            nowUtc,

        PersonalDataConsentVersion =
            LegalDocumentVersions.PersonalDataConsent
    };

            var createResult =
                await _userManager
                    .CreateAsync(
                        user);

            if (!createResult.Succeeded)
            {
                await transaction
                    .RollbackAsync(
                        cancellationToken);

                await transaction
                    .DisposeAsync();

                _dbContext
                    .ChangeTracker
                    .Clear();

                // ---------------------------------------------------------------------
                // EN: Another request may have completed registration after our
                //     previous existence check but before CreateAsync.
                //
                // RU: После нашей проверки другой параллельный запрос мог успеть
                //     создать аккаунт раньше CreateAsync.
                // ---------------------------------------------------------------------

                var concurrentResult =
                    await TryCompleteConcurrentRegistrationAsync(
                        currentChallenge.Id,
                        currentChallenge.PhoneNumber,
                        cancellationToken);

                if (concurrentResult is not null)
                {
                    return concurrentResult;
                }

                _logger.LogError(
                    "Failed to create phone account. Errors: {Errors}",
                    string.Join(
                        ", ",
                        createResult.Errors.Select(
                            error =>
                                error.Code)));

                ModelState.AddModelError(
                    string.Empty,
                    "Не удалось создать аккаунт. Попробуйте ещё раз.");

                return Page();
            }

            currentChallenge.UserId =
                user.Id;

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            await transaction
                .CommitAsync(
                    cancellationToken);


            await _signInManager
                .SignInAsync(
                    user,
                    isPersistent:
                        false);

            return LocalRedirect(
                ReturnUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException exception)
    when (IsUniqueViolation(
        exception))
        {
            await transaction
                .RollbackAsync(
                    CancellationToken.None);

            await transaction
                .DisposeAsync();

            _dbContext
                .ChangeTracker
                .Clear();

            // ---------------------------------------------------------------------
            // EN: The unique phone/user-name constraint is the final protection
            //     against two concurrent account creations.
            //
            // RU: UNIQUE-индекс телефона/UserName остаётся последней защитой
            //     от конкурентного создания двух аккаунтов.
            // ---------------------------------------------------------------------

            var concurrentResult =
                await TryCompleteConcurrentRegistrationAsync(
                    challenge.Id,
                    challenge.PhoneNumber,
                    cancellationToken);

            if (concurrentResult is not null)
            {
                return concurrentResult;
            }

            _logger.LogWarning(
                exception,
                "Unique constraint rejected phone registration for challenge {ChallengeId}.",
                challenge.Id);

            ModelState.AddModelError(
                string.Empty,
                "Не удалось создать аккаунт. Попробуйте ещё раз.");

            return Page();
        }
        catch (Exception exception)
        {
            await transaction
                .RollbackAsync(
                    CancellationToken.None);

            _logger.LogError(
                exception,
                "Failed to complete phone registration for challenge {ChallengeId}.",
                challenge.Id);

            ModelState.AddModelError(
                string.Empty,
                "Не удалось создать аккаунт. Попробуйте ещё раз.");

            return Page();
        }
    }

    // -------------------------------------------------------------------------
    // EN: Verified registration challenge from protected TempData.
    // RU: Подтверждённый challenge регистрации из защищённого TempData.
    // -------------------------------------------------------------------------

    private async Task<PhoneOtpChallenge?>
        GetRegistrationChallengeAsync(
            CancellationToken cancellationToken)
    {
        if (!_continuationProtector.TryUnprotect(
        Continuation,
        out var challengeId))
        {
            return null;
        }

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var minimumConsumedAtUtc =
            nowUtc -
            RegistrationCompletionLifetime;

        return await _dbContext
            .PhoneOtpChallenges
            .AsNoTracking()
            .SingleOrDefaultAsync(
                challenge =>
                    challenge.Id ==
                        challengeId &&
                    challenge.Purpose ==
                        PhoneOtpPurpose.Authentication &&
                    challenge.ConsumedAtUtc !=
                        null &&
                    challenge.ConsumedAtUtc >=
                        minimumConsumedAtUtc &&
                    challenge.InvalidatedAtUtc ==
                        null &&
                    challenge.UserId ==
                        null,
                cancellationToken);
    }

    // -------------------------------------------------------------------------
    // EN: Resolve a race where another request created the phone account first.
    // RU: Обрабатываем гонку, когда другой запрос первым создал аккаунт.
    // -------------------------------------------------------------------------

    private async Task<IActionResult?>
        TryCompleteConcurrentRegistrationAsync(
            Guid challengeId,
            string phoneNumber,
            CancellationToken cancellationToken)
    {
        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var minimumConsumedAtUtc =
            nowUtc -
            RegistrationCompletionLifetime;

        // ---------------------------------------------------------------------
        // EN: The database unique phone index guarantees at most one result.
        // RU: UNIQUE-индекс телефона гарантирует максимум одного пользователя.
        // ---------------------------------------------------------------------

        var existingUser =
            await _userManager
                .Users
                .SingleOrDefaultAsync(
                    user =>
                        user.PhoneNumber ==
                            phoneNumber,
                    cancellationToken);

        if (existingUser is null)
        {
            return null;
        }

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
                nowUtc;

            var updateResult =
                await _userManager
                    .UpdateAsync(
                        existingUser);

            if (!updateResult.Succeeded)
            {
                _logger.LogError(
                    "Failed to confirm phone for concurrent registration user {UserId}. Errors: {Errors}",
                    existingUser.Id,
                    string.Join(
                        ", ",
                        updateResult.Errors.Select(
                            error =>
                                error.Code)));

                return null;
            }
        }

        // ---------------------------------------------------------------------
        // EN: Do not trust only the phone collision. The original OTP challenge
        //     must still be a recent successfully consumed registration proof.
        //
        // RU: Одного совпадения телефона недостаточно. Исходный OTP challenge
        //     должен оставаться свежим успешно подтверждённым доказательством.
        // ---------------------------------------------------------------------

        var associatedRows =
            await _dbContext
                .PhoneOtpChallenges
                .Where(challenge =>
                    challenge.Id ==
                        challengeId &&
                    challenge.PhoneNumber ==
                        phoneNumber &&
                    challenge.Purpose ==
                        PhoneOtpPurpose.Authentication &&
                    challenge.ConsumedAtUtc !=
                        null &&
                    challenge.ConsumedAtUtc >=
                        minimumConsumedAtUtc &&
                    challenge.InvalidatedAtUtc ==
                        null &&
                    (
                        challenge.UserId ==
                            null ||
                        challenge.UserId ==
                            existingUser.Id
                    ))
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            challenge =>
                                challenge.UserId,
                            existingUser.Id),
                    cancellationToken);

        if (associatedRows != 1)
        {
            _logger.LogWarning(
                "Concurrent phone registration challenge {ChallengeId} could not be associated with user {UserId}.",
                challengeId,
                existingUser.Id);

            return null;
        }

        await _signInManager
            .SignInAsync(
                existingUser,
                isPersistent:
                    false);

        return LocalRedirect(
            ReturnUrl);
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


    private static bool IsUniqueViolation(
    DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException postgresException &&
            postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation;
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
                "Введите отображаемое имя.")]
        [StringLength(
            80,
            MinimumLength = 2,
            ErrorMessage =
                "Имя должно содержать от 2 до 80 символов.")]
        [Display(
            Name =
                "Как вас показывать на сайте")]
        public string DisplayName { get; set; }
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
