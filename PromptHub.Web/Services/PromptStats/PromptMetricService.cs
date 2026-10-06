using Microsoft.EntityFrameworkCore;
using Npgsql;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.PromptStats;

public sealed record PromptMetricRegistrationResult(
    bool PromptAvailable,
    bool Counted,
    int CurrentCount);

public interface IPromptMetricService
{
    Task<PromptMetricRegistrationResult> RegisterAsync(
        int promptId,
        PromptMetricKind kind,
        string actorKeyHash,
        CancellationToken cancellationToken);
}

public sealed class PromptMetricService
    : IPromptMetricService
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly TimeProvider
        _timeProvider;

    public PromptMetricService(
        ApplicationDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext =
            dbContext;

        _timeProvider =
            timeProvider;
    }

    public async Task<PromptMetricRegistrationResult> RegisterAsync(
        int promptId,
        PromptMetricKind kind,
        string actorKeyHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            actorKeyHash);

        var nowUtc =
            _timeProvider
                .GetUtcNow()
                .UtcDateTime;

        var bucketStartUtc =
            GetBucketStartUtc(
                kind,
                nowUtc);

        var hit =
            new PromptMetricHit
            {
                PromptId =
                    promptId,

                Kind =
                    kind,

                ActorKeyHash =
                    actorKeyHash,

                BucketStartUtc =
                    bucketStartUtc,

                CreatedAtUtc =
                    nowUtc
            };

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        _dbContext.PromptMetricHits.Add(
            hit);

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(
                exception))
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _dbContext.Entry(
                    hit)
                .State =
                    EntityState.Detached;

            // -----------------------------------------------------------------
            // EN: The actor was already counted in this time bucket.
            // RU: Этот посетитель уже был учтён в текущем временном окне.
            // -----------------------------------------------------------------

            var currentCount =
                await GetCurrentCountAsync(
                    promptId,
                    kind,
                    cancellationToken);

            if (!currentCount.HasValue)
            {
                return new PromptMetricRegistrationResult(
                    PromptAvailable:
                        false,

                    Counted:
                        false,

                    CurrentCount:
                        0);
            }

            return new PromptMetricRegistrationResult(
                PromptAvailable:
                    true,

                Counted:
                    false,

                CurrentCount:
                    currentCount.Value);
        }

        var updatedRows =
            kind switch
            {
                PromptMetricKind.View =>
                    await IncrementViewCountAsync(
                        promptId,
                        cancellationToken),

                PromptMetricKind.Copy =>
                    await IncrementCopyCountAsync(
                        promptId,
                        cancellationToken),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "Unknown prompt metric kind.")
            };

        // ---------------------------------------------------------------------
        // EN: The prompt may have become unavailable between page lookup
        //     and metric registration. Do not leave an orphan metric hit.
        //
        // RU: Между загрузкой страницы и регистрацией метрики промпт мог
        //     перестать быть публичным. Не оставляем лишнюю запись метрики.
        // ---------------------------------------------------------------------

        if (updatedRows != 1)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _dbContext.Entry(
                    hit)
                .State =
                    EntityState.Detached;

            return new PromptMetricRegistrationResult(
                PromptAvailable:
                    false,

                Counted:
                    false,

                CurrentCount:
                    0);
        }

        await transaction.CommitAsync(
            cancellationToken);

        var count =
            await GetCurrentCountAsync(
                promptId,
                kind,
                cancellationToken);

        return new PromptMetricRegistrationResult(
            PromptAvailable:
                true,

            Counted:
                true,

            CurrentCount:
                count ?? 0);
    }


    private Task<int> IncrementViewCountAsync(
        int promptId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Prompts
            .Where(prompt =>
                prompt.Id ==
                    promptId &&
                prompt.Status ==
                    PromptStatus.Published &&
                prompt.Category.IsActive)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        prompt =>
                            prompt.ViewCount,
                        prompt =>
                            prompt.ViewCount + 1),
                cancellationToken);
    }

    private Task<int> IncrementCopyCountAsync(
        int promptId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Prompts
            .Where(prompt =>
                prompt.Id ==
                    promptId &&
                prompt.Status ==
                    PromptStatus.Published &&
                prompt.Category.IsActive)
            .ExecuteUpdateAsync(
                setters =>
                    setters.SetProperty(
                        prompt =>
                            prompt.CopyCount,
                        prompt =>
                            prompt.CopyCount + 1),
                cancellationToken);
    }

    private async Task<int?> GetCurrentCountAsync(
        int promptId,
        PromptMetricKind kind,
        CancellationToken cancellationToken)
    {
        var query =
            _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    prompt.Id ==
                        promptId &&
                    prompt.Status ==
                        PromptStatus.Published &&
                    prompt.Category.IsActive);

        return kind switch
        {
            PromptMetricKind.View =>
                await query
                    .Select(prompt =>
                        (int?)prompt.ViewCount)
                    .SingleOrDefaultAsync(
                        cancellationToken),

            PromptMetricKind.Copy =>
                await query
                    .Select(prompt =>
                        (int?)prompt.CopyCount)
                    .SingleOrDefaultAsync(
                        cancellationToken),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown prompt metric kind.")
        };
    }

    // -------------------------------------------------------------------------
    // EN: Metric deduplication windows
    // RU: Временные окна дедупликации метрик
    // -------------------------------------------------------------------------

    private static DateTime GetBucketStartUtc(
        PromptMetricKind kind,
        DateTime nowUtc)
    {
        return kind switch
        {
            PromptMetricKind.View =>
                nowUtc.Date,

            PromptMetricKind.Copy =>
                new DateTime(
                    nowUtc.Year,
                    nowUtc.Month,
                    nowUtc.Day,
                    nowUtc.Hour,
                    0,
                    0,
                    DateTimeKind.Utc),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown prompt metric kind.")
        };
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception)
    {
        return
            exception.InnerException
                is PostgresException postgresException &&
            postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation;
    }
}
