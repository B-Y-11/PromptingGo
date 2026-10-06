using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using System.Buffers.Binary;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneOtpService
    : IPhoneOtpService
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly IPhoneNumberNormalizer
        _phoneNumberNormalizer;

    private readonly IPhoneOtpSender
        _phoneOtpSender;

    private readonly PhoneOtpOptions
        _options;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<PhoneOtpService>
        _logger;

    private readonly byte[]
        _hashingKey;

    public PhoneOtpService(
        ApplicationDbContext dbContext,
        IPhoneNumberNormalizer phoneNumberNormalizer,
        IPhoneOtpSender phoneOtpSender,
        IOptions<PhoneOtpOptions> options,
        TimeProvider timeProvider,
        ILogger<PhoneOtpService> logger)
    {
        _dbContext =
            dbContext;

        _phoneNumberNormalizer =
            phoneNumberNormalizer;

        _phoneOtpSender =
            phoneOtpSender;

        _options =
            options.Value;

        _timeProvider =
            timeProvider;

        _logger =
            logger;

        _hashingKey =
            Encoding.UTF8.GetBytes(
                _options.HashingKey);
    }

    // -------------------------------------------------------------------------
    // EN: Create and send a new OTP challenge
    // RU: Создание и отправка новой OTP-проверки
    // -------------------------------------------------------------------------

    public async Task<PhoneOtpRequestResult> RequestAsync(
    string? phoneNumber,
    PhoneOtpPurpose purpose,
    string? userId = null,
    CancellationToken cancellationToken = default)
    {
        if (!_phoneNumberNormalizer.TryNormalize(
                phoneNumber,
                out var normalizedPhoneNumber))
        {
            return new PhoneOtpRequestResult(
                PhoneOtpRequestStatus.InvalidPhoneNumber);
        }

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        string code;
        PhoneOtpChallenge challenge;

        // -------------------------------------------------------------------------
        // EN: Serialize requests for the same phone and, when applicable,
        //     the same authenticated user.
        //
        // RU: Сериализуем параллельные запросы одного номера и,
        //     при наличии пользователя, одного аккаунта.
        // -------------------------------------------------------------------------

        await using (
            var transaction =
                await _dbContext
                    .Database
                    .BeginTransactionAsync(
                        cancellationToken))
        {
            await AcquireRequestLocksAsync(
                normalizedPhoneNumber,
                userId,
                cancellationToken);

            // ---------------------------------------------------------------------
            // EN: Cooldown is global per phone number.
            // RU: Cooldown общий для всех назначений OTP одного номера.
            // ---------------------------------------------------------------------

            var latestCreatedAtUtc =
                await _dbContext
                    .PhoneOtpChallenges
                    .AsNoTracking()
                    .Where(challenge =>
                        challenge.PhoneNumber ==
                            normalizedPhoneNumber)
                    .OrderByDescending(challenge =>
                        challenge.CreatedAtUtc)
                    .Select(challenge =>
                        (DateTime?)challenge.CreatedAtUtc)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (latestCreatedAtUtc.HasValue)
            {
                var cooldown =
                    TimeSpan.FromSeconds(
                        _options.ResendCooldownSeconds);

                var elapsed =
                    nowUtc -
                    latestCreatedAtUtc.Value;

                var remaining =
                    cooldown -
                    elapsed;

                if (remaining >
                    TimeSpan.Zero)
                {
                    return new PhoneOtpRequestResult(
                        PhoneOtpRequestStatus.CooldownActive,
                        RetryAfterSeconds:
                            ToRetryAfterSeconds(
                                remaining));
                }
            }

            var requestWindowStartUtc =
                nowUtc.AddHours(
                    -1);

            // ---------------------------------------------------------------------
            // EN: Hourly per-phone limit.
            // RU: Часовой лимит для номера.
            // ---------------------------------------------------------------------

            var recentRequestsQuery =
                _dbContext
                    .PhoneOtpChallenges
                    .AsNoTracking()
                    .Where(challenge =>
                        challenge.PhoneNumber ==
                            normalizedPhoneNumber &&
                        challenge.CreatedAtUtc >=
                            requestWindowStartUtc);

            var recentRequestCount =
                await recentRequestsQuery
                    .CountAsync(
                        cancellationToken);

            if (recentRequestCount >=
                _options.MaxRequestsPerHour)
            {
                var oldestRequestUtc =
                    await recentRequestsQuery
                        .OrderBy(challenge =>
                            challenge.CreatedAtUtc)
                        .Select(challenge =>
                            challenge.CreatedAtUtc)
                        .FirstAsync(
                            cancellationToken);

                var retryAtUtc =
                    oldestRequestUtc.AddHours(
                        1);

                return new PhoneOtpRequestResult(
                    PhoneOtpRequestStatus.RateLimited,
                    RetryAfterSeconds:
                        ToRetryAfterSeconds(
                            retryAtUtc -
                            nowUtc));
            }

            // ---------------------------------------------------------------------
            // EN: Per-user limit for authenticated account operations.
            // RU: Отдельный лимит для операций авторизованного пользователя.
            // ---------------------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                    userId))
            {
                var userRequestCount =
                    await _dbContext
                        .PhoneOtpChallenges
                        .AsNoTracking()
                        .CountAsync(
                            challenge =>
                                challenge.UserId ==
                                    userId &&
                                challenge.CreatedAtUtc >=
                                    requestWindowStartUtc,
                            cancellationToken);

                if (userRequestCount >=
                    _options.MaxRequestsPerUserPerHour)
                {
                    return new PhoneOtpRequestResult(
                        PhoneOtpRequestStatus.RateLimited);
                }
            }

            // ---------------------------------------------------------------------
            // EN: Replace previous active OTP for the same phone and purpose.
            // RU: Аннулируем предыдущий активный OTP того же назначения.
            // ---------------------------------------------------------------------

            await _dbContext
                .PhoneOtpChallenges
                .Where(challenge =>
                    challenge.PhoneNumber ==
                        normalizedPhoneNumber &&
                    challenge.Purpose ==
                        purpose &&
                    challenge.ConsumedAtUtc ==
                        null &&
                    challenge.InvalidatedAtUtc ==
                        null)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            challenge =>
                                challenge.InvalidatedAtUtc,
                            nowUtc),
                    cancellationToken);

            code =
                GenerateCode();

            challenge =
                new PhoneOtpChallenge
                {
                    Id =
                        Guid.NewGuid(),

                    PhoneNumber =
                        normalizedPhoneNumber,

                    Purpose =
                        purpose,

                    UserId =
                        userId,

                    CreatedAtUtc =
                        nowUtc,

                    ExpiresAtUtc =
                        nowUtc.AddMinutes(
                            _options.LifetimeMinutes),

                    FailedAttempts =
                        0
                };

            challenge.CodeHash =
                ComputeCodeHash(
                    challenge.Id,
                    challenge.PhoneNumber,
                    challenge.Purpose,
                    code);

            _dbContext
                .PhoneOtpChallenges
                .Add(
                    challenge);

            try
            {
                await _dbContext
                    .SaveChangesAsync(
                        cancellationToken);
            }
            catch (DbUpdateException exception)
                when (IsUniqueViolation(
                    exception))
            {
                _dbContext
                    .Entry(challenge)
                    .State =
                        EntityState.Detached;

                return new PhoneOtpRequestResult(
                    PhoneOtpRequestStatus.ConcurrentRequest,
                    RetryAfterSeconds:
                        _options.ResendCooldownSeconds);
            }

            await transaction
                .CommitAsync(
                    cancellationToken);
        }

        // -------------------------------------------------------------------------
        // EN: Transaction is already finished here. Do not hold database locks
        //     while calling an external SMS provider.
        //
        // RU: Транзакция уже завершена. Не держим блокировки БД во время
        //     обращения к внешнему SMS-провайдеру.
        // -------------------------------------------------------------------------

        _dbContext
            .Entry(challenge)
            .State =
                EntityState.Detached;

        try
        {
            await _phoneOtpSender
                .SendAsync(
                    normalizedPhoneNumber,
                    code,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedAtUtc =
                _timeProvider
                    .GetUtcNow()
                    .UtcDateTime;

            await _dbContext
                .PhoneOtpChallenges
                .Where(item =>
                    item.Id ==
                        challenge.Id &&
                    item.ConsumedAtUtc ==
                        null &&
                    item.InvalidatedAtUtc ==
                        null)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            item =>
                                item.InvalidatedAtUtc,
                            failedAtUtc),
                    cancellationToken);

            _logger.LogError(
                exception,
                "Failed to deliver phone OTP challenge {ChallengeId}.",
                challenge.Id);

            return new PhoneOtpRequestResult(
                PhoneOtpRequestStatus.DeliveryFailed);
        }

        return new PhoneOtpRequestResult(
            PhoneOtpRequestStatus.Sent,
            challenge.Id,
            normalizedPhoneNumber);
    }

    // -------------------------------------------------------------------------
    // EN: Verify and atomically consume an OTP challenge
    // RU: Проверка и атомарное погашение OTP-проверки
    // -------------------------------------------------------------------------

    public async Task<PhoneOtpVerificationResult> VerifyAsync(
        Guid challengeId,
        PhoneOtpPurpose purpose,
        string? code,
        CancellationToken cancellationToken = default)
    {
        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.Id ==
                            challengeId &&
                        item.Purpose ==
                            purpose,
                    cancellationToken);

        if (challenge is null ||
            challenge.ConsumedAtUtc.HasValue ||
            challenge.InvalidatedAtUtc.HasValue)
        {
            return new PhoneOtpVerificationResult(
                PhoneOtpVerificationStatus.Unavailable);
        }

        if (challenge.ExpiresAtUtc <=
            nowUtc)
        {
            await InvalidateAsync(
                challenge.Id,
                nowUtc,
                cancellationToken);

            return new PhoneOtpVerificationResult(
                PhoneOtpVerificationStatus.Expired);
        }

        if (challenge.FailedAttempts >=
            _options.MaxVerificationAttempts)
        {
            await InvalidateAsync(
                challenge.Id,
                nowUtc,
                cancellationToken);

            return new PhoneOtpVerificationResult(
                PhoneOtpVerificationStatus.AttemptsExceeded);
        }

        var submittedCode =
            code?.Trim() ??
            string.Empty;

        var codeMatches =
            IsSixDigitCode(
                submittedCode) &&
            VerifyCodeHash(
                challenge,
                submittedCode);

        if (!codeMatches)
        {
            // -----------------------------------------------------------------
            // EN: Increment is performed in SQL so parallel guesses cannot
            //     overwrite each other's attempt counters.
            // RU: Счётчик увеличивается непосредственно в SQL, поэтому
            //     параллельные попытки не затирают друг друга.
            // -----------------------------------------------------------------

            var affectedRows =
                await _dbContext
                    .PhoneOtpChallenges
                    .Where(item =>
                        item.Id ==
                            challenge.Id &&
                        item.ConsumedAtUtc ==
                            null &&
                        item.InvalidatedAtUtc ==
                            null &&
                        item.ExpiresAtUtc >
                            nowUtc &&
                        item.FailedAttempts <
                            _options.MaxVerificationAttempts)
                    .ExecuteUpdateAsync(
                        setters =>
                            setters.SetProperty(
                                item =>
                                    item.FailedAttempts,
                                item =>
                                    item.FailedAttempts +
                                    1),
                        cancellationToken);

            if (affectedRows != 1)
            {
                return new PhoneOtpVerificationResult(
                    PhoneOtpVerificationStatus.Unavailable);
            }

            var failedAttempts =
                await _dbContext
                    .PhoneOtpChallenges
                    .AsNoTracking()
                    .Where(item =>
                        item.Id ==
                            challenge.Id)
                    .Select(item =>
                        item.FailedAttempts)
                    .SingleAsync(
                        cancellationToken);

            if (failedAttempts >=
                _options.MaxVerificationAttempts)
            {
                await InvalidateAsync(
                    challenge.Id,
                    nowUtc,
                    cancellationToken);

                return new PhoneOtpVerificationResult(
                    PhoneOtpVerificationStatus.AttemptsExceeded);
            }

            return new PhoneOtpVerificationResult(
                PhoneOtpVerificationStatus.InvalidCode);
        }

        // ---------------------------------------------------------------------
        // EN: Conditional SQL update guarantees one-time consumption even
        //     if the correct code is submitted concurrently.
        // RU: Условный SQL UPDATE гарантирует одноразовое использование,
        //     даже если правильный код отправили параллельно.
        // ---------------------------------------------------------------------

        var consumedRows =
            await _dbContext
                .PhoneOtpChallenges
                .Where(item =>
                    item.Id ==
                        challenge.Id &&
                    item.ConsumedAtUtc ==
                        null &&
                    item.InvalidatedAtUtc ==
                        null &&
                    item.ExpiresAtUtc >
                        nowUtc &&
                    item.FailedAttempts <
                        _options.MaxVerificationAttempts)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            item =>
                                item.ConsumedAtUtc,
                            nowUtc),
                    cancellationToken);

        if (consumedRows != 1)
        {
            return new PhoneOtpVerificationResult(
                PhoneOtpVerificationStatus.Unavailable);
        }

        return new PhoneOtpVerificationResult(
            PhoneOtpVerificationStatus.Succeeded,
            challenge.PhoneNumber,
            challenge.UserId);
    }


    private async Task AcquireRequestLocksAsync(
    string phoneNumber,
    string? userId,
    CancellationToken cancellationToken)
    {
        // -------------------------------------------------------------------------
        // EN: User lock prevents concurrent requests to different phone numbers
        //     from bypassing the per-user hourly limit.
        //
        // RU: Блокировка пользователя не позволяет параллельными запросами
        //     разных номеров обойти пользовательский лимит.
        // -------------------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
                userId))
        {
            await AcquireAdvisoryLockAsync(
                $"user:{userId}",
                cancellationToken);
        }

        // -------------------------------------------------------------------------
        // EN: Phone lock protects global cooldown and per-phone limits.
        // RU: Блокировка номера защищает общий cooldown и лимиты номера.
        // -------------------------------------------------------------------------

        await AcquireAdvisoryLockAsync(
            $"phone:{phoneNumber}",
            cancellationToken);
    }

    private async Task AcquireAdvisoryLockAsync(
        string resource,
        CancellationToken cancellationToken)
    {
        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    resource));

        var lockKey =
            BinaryPrimitives
                .ReadInt64BigEndian(
                    hash.AsSpan(
                        0,
                        sizeof(long)));

        await _dbContext
            .Database
            .ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({lockKey})",
                cancellationToken);
    }




    private static string GenerateCode()
    {
        var value =
            RandomNumberGenerator.GetInt32(
                0,
                1_000_000);

        return value.ToString(
            "D6",
            CultureInfo.InvariantCulture);
    }


    private string ComputeCodeHash(
        Guid challengeId,
        string phoneNumber,
        PhoneOtpPurpose purpose,
        string code)
    {
        var payload =
            $"{challengeId:N}|" +
            $"{phoneNumber}|" +
            $"{(int)purpose}|" +
            code;

        using var hmac =
            new HMACSHA256(
                _hashingKey);

        var hash =
            hmac.ComputeHash(
                Encoding.UTF8.GetBytes(
                    payload));

        return Convert.ToBase64String(
            hash);
    }

    private bool VerifyCodeHash(
        PhoneOtpChallenge challenge,
        string code)
    {
        byte[] storedHash;

        try
        {
            storedHash =
                Convert.FromBase64String(
                    challenge.CodeHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var computedHashBase64 =
            ComputeCodeHash(
                challenge.Id,
                challenge.PhoneNumber,
                challenge.Purpose,
                code);

        var computedHash =
            Convert.FromBase64String(
                computedHashBase64);

        return storedHash.Length ==
                computedHash.Length &&
            CryptographicOperations
                .FixedTimeEquals(
                    storedHash,
                    computedHash);
    }

    private async Task InvalidateAsync(
        Guid challengeId,
        DateTime invalidatedAtUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext
            .PhoneOtpChallenges
            .Where(challenge =>
                challenge.Id ==
                    challengeId &&
                challenge.ConsumedAtUtc ==
                    null &&
                challenge.InvalidatedAtUtc ==
                    null)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        challenge =>
                            challenge.InvalidatedAtUtc,
                        invalidatedAtUtc),
                cancellationToken);
    }

    private static bool IsSixDigitCode(
        string code)
    {
        if (code.Length != 6)
        {
            return false;
        }

        foreach (var character in code)
        {
            if (character < '0' ||
                character > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static int ToRetryAfterSeconds(
        TimeSpan remaining)
    {
        return Math.Max(
            1,
            (int)Math.Ceiling(
                remaining.TotalSeconds));
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException postgresException &&
            postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation;
    }
}
