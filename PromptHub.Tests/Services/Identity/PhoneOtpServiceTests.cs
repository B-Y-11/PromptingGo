using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PromptHub.Tests.Infrastructure;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Identity;
using PromptHub.Web.Models.Entities;


namespace PromptHub.Tests.Services.Identity;

[Collection(PostgreSqlCollection.Name)]
public sealed class PhoneOtpServiceTests
    : IAsyncLifetime
{
    private static readonly DateTimeOffset
        FixedUtcNow =
            new(
                2026,
                8,
                8,
                12,
                0,
                0,
                TimeSpan.Zero);

    private readonly PostgreSqlFixture
        _fixture;

    private ApplicationDbContext
        _dbContext =
            null!;

    private CapturingPhoneOtpSender
        _sender =
            null!;

    private MutableTimeProvider
        _timeProvider =
            null!;

    private PhoneOtpService
        _service =
            null!;

    public PhoneOtpServiceTests(
        PostgreSqlFixture fixture)
    {
        _fixture =
            fixture;
    }


    public async Task InitializeAsync()
    {
        await _fixture
            .ResetDatabaseAsync();

        _dbContext =
            _fixture.CreateDbContext();

        _sender =
            new CapturingPhoneOtpSender();

        _timeProvider =
            new MutableTimeProvider(
                FixedUtcNow);

        _service =
            CreateService();
    }

    public async Task DisposeAsync()
    {
        await _dbContext
            .DisposeAsync();
    }


    [Fact]
    public async Task RequestAsync_ReturnsInvalidPhoneNumber_ForInvalidPhone()
    {
        var result =
            await _service.RequestAsync(
                "+123",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.InvalidPhoneNumber,
            result.Status);

        Assert.Null(
            result.ChallengeId);

        Assert.Equal(
            0,
            _sender.SendCount);

        Assert.False(
            await _dbContext
                .PhoneOtpChallenges
                .AnyAsync());
    }


    [Fact]
    public async Task RequestAsync_PersistsChallengeAndSendsCode()
    {
        var result =
            await _service.RequestAsync(
                "+7 (999) 123-45-67",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            result.Status);

        Assert.NotNull(
            result.ChallengeId);

        Assert.Equal(
            "+79991234567",
            result.PhoneNumber);

        Assert.Equal(
            1,
            _sender.SendCount);

        Assert.Equal(
            "+79991234567",
            _sender.LastPhoneNumber);

        Assert.NotNull(
            _sender.LastCode);

        Assert.Matches(
            @"^\d{6}$",
            _sender.LastCode!);

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            result.ChallengeId,
            challenge.Id);

        Assert.Equal(
            "+79991234567",
            challenge.PhoneNumber);

        Assert.Equal(
            PhoneOtpPurpose.Authentication,
            challenge.Purpose);

        Assert.Equal(
            FixedUtcNow.UtcDateTime,
            challenge.CreatedAtUtc);

        Assert.Equal(
            FixedUtcNow
                .AddMinutes(5)
                .UtcDateTime,
            challenge.ExpiresAtUtc);

        Assert.Null(
            challenge.ConsumedAtUtc);

        Assert.Null(
            challenge.InvalidatedAtUtc);

        Assert.Equal(
            0,
            challenge.FailedAttempts);

        // ---------------------------------------------------------------------
        // EN: Plain OTP must never be stored in the database.
        // RU: Открытый OTP никогда не должен сохраняться в БД.
        // ---------------------------------------------------------------------

        Assert.NotEqual(
            _sender.LastCode,
            challenge.CodeHash);
    }

    // -------------------------------------------------------------------------
    // EN: Successful verification and one-time consumption
    // RU: Успешная проверка и одноразовое использование
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyAsync_ConsumesCorrectCodeOnlyOnce()
    {
        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            request.Status);

        var code =
            Assert.IsType<string>(
                _sender.LastCode);

        var firstVerification =
            await _service.VerifyAsync(
                request.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                code);

        Assert.Equal(
            PhoneOtpVerificationStatus.Succeeded,
            firstVerification.Status);

        Assert.Equal(
            "+79991234567",
            firstVerification.PhoneNumber);

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.NotNull(
            challenge.ConsumedAtUtc);

        // ---------------------------------------------------------------------
        // EN: Reusing the same correct code must fail.
        // RU: Повторное использование правильного кода запрещено.
        // ---------------------------------------------------------------------

        var secondVerification =
            await _service.VerifyAsync(
                request.ChallengeId.Value,
                PhoneOtpPurpose.Authentication,
                code);

        Assert.Equal(
            PhoneOtpVerificationStatus.Unavailable,
            secondVerification.Status);
    }


    [Fact]
    public async Task VerifyAsync_IncrementsFailedAttempts_ForWrongCode()
    {
        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        var actualCode =
            Assert.IsType<string>(
                _sender.LastCode);

        var wrongCode =
            actualCode == "000000"
                ? "000001"
                : "000000";

        var verification =
            await _service.VerifyAsync(
                request.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                wrongCode);

        Assert.Equal(
            PhoneOtpVerificationStatus.InvalidCode,
            verification.Status);

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            1,
            challenge.FailedAttempts);

        Assert.Null(
            challenge.ConsumedAtUtc);

        Assert.Null(
            challenge.InvalidatedAtUtc);
    }


    [Fact]
    public async Task RequestAsync_InvalidatesChallenge_WhenDeliveryFails()
    {
        _sender.ThrowOnSend =
            true;

        var result =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.DeliveryFailed,
            result.Status);

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.NotNull(
            challenge.InvalidatedAtUtc);

        Assert.Null(
            challenge.ConsumedAtUtc);
    }


    // -------------------------------------------------------------------------
    // EN: Cooldown is shared across all OTP purposes for the same phone.
    // RU: Cooldown общий для всех назначений OTP одного номера.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_UsesGlobalCooldownAcrossPurposes()
    {
        var first =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            first.Status);

        _timeProvider.Advance(
            TimeSpan.FromSeconds(30));

        var second =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.LinkPhone);

        Assert.Equal(
            PhoneOtpRequestStatus.CooldownActive,
            second.Status);

        Assert.Equal(
            30,
            second.RetryAfterSeconds);

        Assert.Equal(
            1,
            _sender.SendCount);
    }

    // -------------------------------------------------------------------------
    // EN: A new OTP invalidates the previous active OTP for the same purpose.
    // RU: Новый OTP аннулирует предыдущий активный OTP того же назначения.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_InvalidatesPreviousChallenge_ForSamePhoneAndPurpose()
    {
        var first =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            first.Status);

        var firstCode =
            Assert.IsType<string>(
                _sender.LastCode);

        _timeProvider.Advance(
            TimeSpan.FromSeconds(60));

        var second =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            second.Status);

        var secondCode =
            Assert.IsType<string>(
                _sender.LastCode);

        var firstChallenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync(
                    challenge =>
                        challenge.Id ==
                        first.ChallengeId);

        Assert.NotNull(
            firstChallenge.InvalidatedAtUtc);

        var oldVerification =
            await _service.VerifyAsync(
                first.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                firstCode);

        Assert.Equal(
            PhoneOtpVerificationStatus.Unavailable,
            oldVerification.Status);

        var newVerification =
            await _service.VerifyAsync(
                second.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                secondCode);

        Assert.Equal(
            PhoneOtpVerificationStatus.Succeeded,
            newVerification.Status);
    }

    // -------------------------------------------------------------------------
    // EN: Per-phone hourly request limit.
    // RU: Часовой лимит запросов для одного номера.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_RateLimitsPhone_AfterHourlyLimit()
    {
        _service =
            CreateService(
                options =>
                {
                    options.MaxRequestsPerHour =
                        3;
                });

        for (var attempt = 0;
             attempt < 3;
             attempt++)
        {
            var request =
                await _service.RequestAsync(
                    "+79991234567",
                    PhoneOtpPurpose.Authentication);

            Assert.Equal(
                PhoneOtpRequestStatus.Sent,
                request.Status);

            var challengeId =
    Assert.IsType<Guid>(
        request.ChallengeId);

            _timeProvider.Advance(
                TimeSpan.FromSeconds(60));
        }

        var blocked =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.RateLimited,
            blocked.Status);

        Assert.Equal(
            3420,
            blocked.RetryAfterSeconds);

        Assert.Equal(
            3,
            _sender.SendCount);
    }

    // -------------------------------------------------------------------------
    // EN: Expired OTP cannot be verified.
    // RU: Просроченный OTP нельзя подтвердить.
    // -------------------------------------------------------------------------


    // -------------------------------------------------------------------------
    // EN: Concurrent invalid guesses must not lose increments or exceed limit.
    // RU: Параллельные неверные попытки не должны терять инкременты
    //     или превышать установленный лимит.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyAsync_ConcurrentWrongCodes_DoesNotLoseAttemptsOrExceedLimit()
    {
        _service =
            CreateService(
                options =>
                {
                    options.MaxVerificationAttempts =
                        3;
                });

        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            request.Status);

        var challengeId =
    Assert.IsType<Guid>(
        request.ChallengeId);

        var actualCode =
            Assert.IsType<string>(
                _sender.LastCode);

        var wrongCode =
            actualCode == "000000"
                ? "000001"
                : "000000";

        const int concurrentAttempts =
            5;

        var contexts =
            Enumerable
                .Range(
                    0,
                    concurrentAttempts)
                .Select(_ =>
                    _fixture.CreateDbContext())
                .ToArray();

        try
        {
            var services =
                contexts
                    .Select(context =>
                        CreateService(
                            context,
                            new CapturingPhoneOtpSender(),
                            new MutableTimeProvider(
                                FixedUtcNow),
                            options =>
                            {
                                options.MaxVerificationAttempts =
                                    3;
                            }))
                    .ToArray();

            var gate =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

            var tasks =
                services
                    .Select(
                        async service =>
                        {
                            await gate.Task;

                            return await service.VerifyAsync(
                                challengeId,
                                PhoneOtpPurpose.Authentication,
                                wrongCode);
                        })
                    .ToArray();

            gate.SetResult(
                true);

            var results =
                await Task.WhenAll(
                    tasks);

            // ---------------------------------------------------------------------
            // EN: No invalid guess may authenticate the user.
            // RU: Ни одна неверная попытка не должна завершиться успехом.
            // ---------------------------------------------------------------------

            Assert.DoesNotContain(
                results,
                result =>
                    result.Status ==
                    PhoneOtpVerificationStatus.Succeeded);

            // ---------------------------------------------------------------------
            // EN: At least one request must observe that the limit was reached.
            // RU: Хотя бы один запрос должен увидеть достижение лимита.
            // ---------------------------------------------------------------------

            Assert.Contains(
                results,
                result =>
                    result.Status ==
                    PhoneOtpVerificationStatus.AttemptsExceeded);

            // ---------------------------------------------------------------------
            // EN: Because execution order is intentionally concurrent,
            //     InvalidCode / AttemptsExceeded / Unavailable are all legitimate.
            //
            // RU: Из-за конкурентного порядка выполнения допустимы
            //     InvalidCode / AttemptsExceeded / Unavailable.
            // ---------------------------------------------------------------------

            Assert.All(
                results,
                result =>
                {
                    Assert.True(
                        result.Status is
                            PhoneOtpVerificationStatus.InvalidCode or
                            PhoneOtpVerificationStatus.AttemptsExceeded or
                            PhoneOtpVerificationStatus.Unavailable);
                });

            await using var verificationDbContext =
                _fixture.CreateDbContext();

            var challenge =
                await verificationDbContext
                    .PhoneOtpChallenges
                    .AsNoTracking()
                    .SingleAsync(
                        item =>
                            item.Id ==
                            challengeId);

            // ---------------------------------------------------------------------
            // EN: Five concurrent guesses must still stop exactly at three.
            // RU: Пять параллельных попыток должны остановиться ровно на трёх.
            // ---------------------------------------------------------------------

            Assert.Equal(
                3,
                challenge.FailedAttempts);

            Assert.NotNull(
                challenge.InvalidatedAtUtc);

            Assert.Null(
                challenge.ConsumedAtUtc);

            // ---------------------------------------------------------------------
            // EN: Even the real code is unusable after the attempt limit.
            // RU: После достижения лимита даже настоящий код использовать нельзя.
            // ---------------------------------------------------------------------

            await using var finalDbContext =
                _fixture.CreateDbContext();

            var finalService =
                CreateService(
                    finalDbContext,
                    new CapturingPhoneOtpSender(),
                    new MutableTimeProvider(
                        FixedUtcNow),
                    options =>
                    {
                        options.MaxVerificationAttempts =
                            3;
                    });

            var afterLimit =
                await finalService.VerifyAsync(
                    challengeId,
                    PhoneOtpPurpose.Authentication,
                    actualCode);

            Assert.Equal(
                PhoneOtpVerificationStatus.Unavailable,
                afterLimit.Status);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }



    [Fact]
    public async Task VerifyAsync_ReturnsExpired_WhenLifetimeHasElapsed()
    {
        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            request.Status);

        var challengeId =
    Assert.IsType<Guid>(
        request.ChallengeId);

        var code =
            Assert.IsType<string>(
                _sender.LastCode);

        _timeProvider.Advance(
            TimeSpan.FromMinutes(5));

        var verification =
            await _service.VerifyAsync(
                request.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                code);

        Assert.Equal(
            PhoneOtpVerificationStatus.Expired,
            verification.Status);

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.NotNull(
            challenge.InvalidatedAtUtc);

        Assert.Null(
            challenge.ConsumedAtUtc);
    }

    // -------------------------------------------------------------------------
    // EN: Too many invalid guesses invalidate the challenge.
    // RU: После превышения числа неверных попыток challenge аннулируется.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyAsync_InvalidatesChallenge_WhenAttemptsExceeded()
    {
        _service =
            CreateService(
                options =>
                {
                    options.MaxVerificationAttempts =
                        3;
                });

        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        var actualCode =
            Assert.IsType<string>(
                _sender.LastCode);

        var wrongCode =
            actualCode == "000000"
                ? "000001"
                : "000000";

        var first =
            await _service.VerifyAsync(
                request.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                wrongCode);

        var second =
            await _service.VerifyAsync(
                request.ChallengeId.Value,
                PhoneOtpPurpose.Authentication,
                wrongCode);

        var third =
            await _service.VerifyAsync(
                request.ChallengeId.Value,
                PhoneOtpPurpose.Authentication,
                wrongCode);

        Assert.Equal(
            PhoneOtpVerificationStatus.InvalidCode,
            first.Status);

        Assert.Equal(
            PhoneOtpVerificationStatus.InvalidCode,
            second.Status);

        Assert.Equal(
            PhoneOtpVerificationStatus.AttemptsExceeded,
            third.Status);

        var challenge =
            await _dbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            3,
            challenge.FailedAttempts);

        Assert.NotNull(
            challenge.InvalidatedAtUtc);

        var afterLimit =
            await _service.VerifyAsync(
                request.ChallengeId.Value,
                PhoneOtpPurpose.Authentication,
                actualCode);

        Assert.Equal(
            PhoneOtpVerificationStatus.Unavailable,
            afterLimit.Status);
    }

    // -------------------------------------------------------------------------
    // EN: A code cannot be used with another OTP purpose.
    // RU: Код нельзя использовать для другого назначения OTP.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyAsync_ReturnsUnavailable_ForDifferentPurpose()
    {
        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        var code =
            Assert.IsType<string>(
                _sender.LastCode);

        var wrongPurpose =
            await _service.VerifyAsync(
                request.ChallengeId!.Value,
                PhoneOtpPurpose.LinkPhone,
                code);

        Assert.Equal(
            PhoneOtpVerificationStatus.Unavailable,
            wrongPurpose.Status);

        // Проверка с неправильным purpose не должна погасить OTP.
        var correctPurpose =
            await _service.VerifyAsync(
                request.ChallengeId.Value,
                PhoneOtpPurpose.Authentication,
                code);

        Assert.Equal(
            PhoneOtpVerificationStatus.Succeeded,
            correctPurpose.Status);
    }



    // -------------------------------------------------------------------------
    // EN: Authenticated account operations share a per-user hourly limit,
    //     even when different phone numbers are used.
    // RU: Операции одного аккаунта используют общий часовой лимит,
    //     даже если запрашиваются разные номера.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestAsync_RateLimitsUser_AcrossDifferentPhones()
    {
        const string userId =
            "otp-test-user";

        await SeedUserAsync(
            userId);

        _service =
            CreateService(
                options =>
                {
                    options.MaxRequestsPerHour =
                        100;

                    options.MaxRequestsPerUserPerHour =
                        2;
                });

        var first =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.LinkPhone,
                userId);

        var second =
            await _service.RequestAsync(
                "+79991234568",
                PhoneOtpPurpose.AddCredentials,
                userId);

        var third =
            await _service.RequestAsync(
                "+79991234569",
                PhoneOtpPurpose.ChangePhoneNew,
                userId);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            first.Status);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            second.Status);

        Assert.Equal(
            PhoneOtpRequestStatus.RateLimited,
            third.Status);

        Assert.Equal(
            2,
            _sender.SendCount);

        var challengeCount =
            await _dbContext
                .PhoneOtpChallenges
                .CountAsync();

        Assert.Equal(
            2,
            challengeCount);
    }



    private async Task SeedUserAsync(
    string userId)
    {
        _dbContext.Users.Add(
            new ApplicationUser
            {
                Id =
                    userId,

                UserName =
                    $"test_{userId}",

                NormalizedUserName =
                    $"TEST_{userId}"
                        .ToUpperInvariant(),

                DisplayName =
                    "OTP Test User",

                SecurityStamp =
                    Guid.NewGuid()
                        .ToString()
            });

        await _dbContext
            .SaveChangesAsync();
    }





    private PhoneOtpService CreateService(
    Action<PhoneOtpOptions>? configure = null)
    {
        return CreateService(
            _dbContext,
            _sender,
            _timeProvider,
            configure);
    }


    private static PhoneOtpService CreateService(
    ApplicationDbContext dbContext,
    IPhoneOtpSender sender,
    TimeProvider timeProvider,
    Action<PhoneOtpOptions>? configure = null)
    {
        var options =
            new PhoneOtpOptions
            {
                LifetimeMinutes =
                    5,

                ResendCooldownSeconds =
                    60,

                MaxRequestsPerHour =
                    5,

                MaxRequestsPerUserPerHour =
                    10,

                MaxVerificationAttempts =
                    5,

                HashingKey =
                    "PromptHub.Tests.PhoneOtp.HashingKey.2026"
            };

        configure?.Invoke(
            options);

        return new PhoneOtpService(
            dbContext,
            new PhoneNumberNormalizer(),
            sender,
            Options.Create(
                options),
            timeProvider,
            NullLogger<PhoneOtpService>
                .Instance);
    }

    // -------------------------------------------------------------------------
    // EN: Controllable application clock
    // RU: Управляемые системные часы
    // -------------------------------------------------------------------------

    private sealed class MutableTimeProvider
        : TimeProvider
    {
        private DateTimeOffset
            _utcNow;

        public MutableTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow =
                utcNow;
        }

        public override DateTimeOffset
            GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(
            TimeSpan duration)
        {
            _utcNow +=
                duration;
        }
    }



    [Fact]
    public async Task RequestAsync_ConcurrentDifferentPurposes_OnlyOneRequestIsSent()
    {
        await using var firstDbContext =
            _fixture.CreateDbContext();

        await using var secondDbContext =
            _fixture.CreateDbContext();

        var firstSender =
            new CapturingPhoneOtpSender();

        var secondSender =
            new CapturingPhoneOtpSender();

        var firstService =
            CreateService(
                firstDbContext,
                firstSender,
                new MutableTimeProvider(
                    FixedUtcNow));

        var secondService =
            CreateService(
                secondDbContext,
                secondSender,
                new MutableTimeProvider(
                    FixedUtcNow));

        var gate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        async Task<PhoneOtpRequestResult>
            RequestFirstAsync()
        {
            await gate.Task;

            return await firstService
                .RequestAsync(
                    "+79991234567",
                    PhoneOtpPurpose.Authentication);
        }

        async Task<PhoneOtpRequestResult>
            RequestSecondAsync()
        {
            await gate.Task;

            return await secondService
                .RequestAsync(
                    "+79991234567",
                    PhoneOtpPurpose.LinkPhone);
        }

        var firstTask =
            RequestFirstAsync();

        var secondTask =
            RequestSecondAsync();

        gate.SetResult(
            true);

        var results =
            await Task.WhenAll(
                firstTask,
                secondTask);

        Assert.Single(
    results,
    result =>
        result.Status ==
        PhoneOtpRequestStatus.Sent);

        Assert.Single(
    results,
    result =>
        result.Status ==
        PhoneOtpRequestStatus.CooldownActive);

        Assert.Equal(
            1,
            firstSender.SendCount +
            secondSender.SendCount);

        await using var verificationDbContext =
            _fixture.CreateDbContext();

        var challengeCount =
            await verificationDbContext
                .PhoneOtpChallenges
                .CountAsync();

        Assert.Equal(
            1,
            challengeCount);
    }



    [Fact]
    public async Task VerifyAsync_ConcurrentCorrectCode_OnlyOneSucceeds()
    {
        var request =
            await _service.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.Authentication);

        Assert.Equal(
            PhoneOtpRequestStatus.Sent,
            request.Status);

        var code =
            Assert.IsType<string>(
                _sender.LastCode);

        await using var firstDbContext =
            _fixture.CreateDbContext();

        await using var secondDbContext =
            _fixture.CreateDbContext();

        var firstService =
            CreateService(
                firstDbContext,
                new CapturingPhoneOtpSender(),
                new MutableTimeProvider(
                    FixedUtcNow));

        var secondService =
            CreateService(
                secondDbContext,
                new CapturingPhoneOtpSender(),
                new MutableTimeProvider(
                    FixedUtcNow));

        var gate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        async Task<PhoneOtpVerificationResult>
            VerifyAsync(
                PhoneOtpService service)
        {
            await gate.Task;

            return await service.VerifyAsync(
                request.ChallengeId!.Value,
                PhoneOtpPurpose.Authentication,
                code);
        }

        var firstTask =
            VerifyAsync(
                firstService);

        var secondTask =
            VerifyAsync(
                secondService);

        gate.SetResult(
            true);

        var results =
            await Task.WhenAll(
                firstTask,
                secondTask);

        Assert.Single(
    results,
    result =>
        result.Status ==
        PhoneOtpVerificationStatus.Succeeded);

        Assert.Single(
            results,
            result =>
                result.Status ==
                PhoneOtpVerificationStatus.Unavailable);

        await using var verificationDbContext =
            _fixture.CreateDbContext();

        var challenge =
            await verificationDbContext
                .PhoneOtpChallenges
                .AsNoTracking()
                .SingleAsync();

        Assert.NotNull(
            challenge.ConsumedAtUtc);
    }




    [Fact]
    public async Task RequestAsync_ConcurrentDifferentPhones_CannotBypassUserLimit()
    {
        const string userId =
            "concurrent-user";

        await SeedUserAsync(
            userId);

        await using var firstDbContext =
            _fixture.CreateDbContext();

        await using var secondDbContext =
            _fixture.CreateDbContext();

        var firstSender =
            new CapturingPhoneOtpSender();

        var secondSender =
            new CapturingPhoneOtpSender();

        var firstService =
            CreateService(
                firstDbContext,
                firstSender,
                new MutableTimeProvider(
                    FixedUtcNow),
                options =>
                {
                    options.MaxRequestsPerHour =
                        100;

                    options.MaxRequestsPerUserPerHour =
                        1;
                });

        var secondService =
            CreateService(
                secondDbContext,
                secondSender,
                new MutableTimeProvider(
                    FixedUtcNow),
                options =>
                {
                    options.MaxRequestsPerHour =
                        100;

                    options.MaxRequestsPerUserPerHour =
                        1;
                });

        var gate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        async Task<PhoneOtpRequestResult>
            FirstRequestAsync()
        {
            await gate.Task;

            return await firstService.RequestAsync(
                "+79991234567",
                PhoneOtpPurpose.LinkPhone,
                userId);
        }

        async Task<PhoneOtpRequestResult>
            SecondRequestAsync()
        {
            await gate.Task;

            return await secondService.RequestAsync(
                "+79991234568",
                PhoneOtpPurpose.ChangePhoneNew,
                userId);
        }

        var firstTask =
            FirstRequestAsync();

        var secondTask =
            SecondRequestAsync();

        gate.SetResult(
            true);

        var results =
            await Task.WhenAll(
                firstTask,
                secondTask);

        Assert.Single(
    results,
    result =>
        result.Status ==
        PhoneOtpRequestStatus.Sent);

Assert.Single(
    results,
    result =>
        result.Status ==
        PhoneOtpRequestStatus.RateLimited);

        Assert.Equal(
            1,
            firstSender.SendCount +
            secondSender.SendCount);
    }

    // -------------------------------------------------------------------------
    // EN: Captures generated OTP instead of sending an SMS.
    // RU: Перехватывает OTP вместо реальной отправки SMS.
    // -------------------------------------------------------------------------

    private sealed class CapturingPhoneOtpSender
        : IPhoneOtpSender
    {
        public int SendCount { get; private set; }

        public string? LastPhoneNumber { get; private set; }

        public string? LastCode { get; private set; }

        public bool ThrowOnSend { get; set; }

        public Task SendAsync(
            string phoneNumber,
            string code,
            CancellationToken cancellationToken = default)
        {
            SendCount++;

            LastPhoneNumber =
                phoneNumber;

            LastCode =
                code;

            if (ThrowOnSend)
            {
                throw new InvalidOperationException(
                    "Simulated SMS delivery failure.");
            }

            return Task.CompletedTask;
        }
    }
}