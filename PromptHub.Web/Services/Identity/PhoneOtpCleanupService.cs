using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneOtpCleanupService
    : BackgroundService
{
    private static readonly TimeSpan InitialDelay =
        TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly PhoneOtpOptions
        _options;

    private readonly TimeProvider
        _timeProvider;

    private readonly ILogger<PhoneOtpCleanupService>
        _logger;

    public PhoneOtpCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<PhoneOtpOptions> options,
        TimeProvider timeProvider,
        ILogger<PhoneOtpCleanupService> logger)
    {
        _scopeFactory =
            scopeFactory;

        _options =
            options.Value;

        _timeProvider =
            timeProvider;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // ---------------------------------------------------------------------
        // EN: Do not compete with application startup and development seeding.
        // RU: Не конкурируем с запуском приложения и development-seeding.
        // ---------------------------------------------------------------------

        try
        {
            await Task.Delay(
                InitialDelay,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        await CleanupSafelyAsync(
            stoppingToken);

        using var timer =
            new PeriodicTimer(
                TimeSpan.FromMinutes(
                    _options.CleanupIntervalMinutes),
                _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await CleanupSafelyAsync(
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // -----------------------------------------------------------------
            // EN: Normal application shutdown.
            // RU: Нормальная остановка приложения.
            // -----------------------------------------------------------------
        }
    }

    private async Task CleanupSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await CleanupAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // -----------------------------------------------------------------
            // EN: Cleanup must never terminate the web application.
            // RU: Ошибка очистки не должна останавливать веб-приложение.
            // -----------------------------------------------------------------

            _logger.LogError(
                exception,
                "Phone OTP cleanup failed.");
        }
    }

    private async Task CleanupAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var deleteBeforeUtc =
            nowUtc.AddHours(
                -_options.RetentionHours);

        // ---------------------------------------------------------------------
        // EN: Expired challenges are retained for a limited period so recent
        //     rows remain available for anti-abuse checks and diagnostics.
        //
        // RU: Просроченные challenge некоторое время сохраняются для
        //     антиабуз-проверок и диагностики, затем удаляются.
        // ---------------------------------------------------------------------

        var deletedCount =
            await dbContext
                .PhoneOtpChallenges
                .Where(challenge =>
                    challenge.ExpiresAtUtc <
                        deleteBeforeUtc)
                .ExecuteDeleteAsync(
                    cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Deleted {DeletedCount} expired phone OTP challenges.",
                deletedCount);
        }
    }
}
