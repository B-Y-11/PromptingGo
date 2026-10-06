using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;

namespace PromptHub.Web.Services.PromptStats;

public sealed class PromptMetricCleanupService
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly TimeProvider
        _timeProvider;

    private readonly PromptMetricCleanupOptions
        _options;

    private readonly ILogger<PromptMetricCleanupService>
        _logger;

    public PromptMetricCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<PromptMetricCleanupOptions> options,
        ILogger<PromptMetricCleanupService> logger)
    {
        _scopeFactory =
            scopeFactory;

        _timeProvider =
            timeProvider;

        _options =
            options.Value;

        _logger =
            logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // ---------------------------------------------------------------------
        // EN: Give the application time to finish startup before cleanup.
        // RU: Даём приложению завершить запуск перед первой очисткой.
        // ---------------------------------------------------------------------

        try
        {
            await Task.Delay(
                TimeSpan.FromMinutes(1),
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        await CleanupAsync(
            stoppingToken);

        using var timer =
            new PeriodicTimer(
                TimeSpan.FromHours(
                    _options.CleanupIntervalHours));

        try
        {
            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await CleanupAsync(
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
    }

    private async Task CleanupAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var nowUtc =
                _timeProvider
                    .GetUtcNow()
                    .UtcDateTime;

            var deleteBeforeUtc =
                nowUtc.AddDays(
                    -_options.RetentionDays);

            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            var deletedRows =
                await dbContext
                    .PromptMetricHits
                    .Where(hit =>
                        hit.CreatedAtUtc <
                            deleteBeforeUtc)
                    .ExecuteDeleteAsync(
                        cancellationToken);

            if (deletedRows > 0)
            {
                _logger.LogInformation(
                    "Deleted {DeletedRows} expired prompt metric hits.",
                    deletedRows);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // -----------------------------------------------------------------
            // EN: Cleanup failure must never terminate the web application.
            // RU: Ошибка фоновой очистки не должна останавливать сайт.
            // -----------------------------------------------------------------

            _logger.LogError(
                exception,
                "Failed to clean expired prompt metric hits.");
        }
    }
}
