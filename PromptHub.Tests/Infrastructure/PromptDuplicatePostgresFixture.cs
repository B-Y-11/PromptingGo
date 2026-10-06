using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using Testcontainers.PostgreSql;

namespace PromptHub.Tests.Infrastructure;

public sealed class PromptDuplicatePostgresFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer
        _container =
            new PostgreSqlBuilder(
                "postgres:18-alpine")
                .Build();

    public ApplicationDbContext
        CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<
                ApplicationDbContext>()
                .UseNpgsql(
                    _container
                        .GetConnectionString())
                .Options;

        return new ApplicationDbContext(
            options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext =
            CreateDbContext();

        // -------------------------------------------------------------------------
        // EN: These tests verify PostgreSQL pg_trgm behavior, not migrations.
        //     The database is temporary and recreated for the test container.
        //
        // RU: Эти тесты проверяют pg_trgm и поиск дубликатов, а не миграции.
        //     База временная и создаётся заново внутри тестового контейнера.
        // -------------------------------------------------------------------------

        await dbContext.Database
            .ExecuteSqlRawAsync(
                """
            CREATE EXTENSION IF NOT EXISTS pg_trgm;
            """);

        await dbContext.Database
            .EnsureCreatedAsync();
    }

    public async Task ResetAsync()
    {
        await using var dbContext =
            CreateDbContext();

        await dbContext.Database
            .ExecuteSqlRawAsync(
                """
                TRUNCATE TABLE
                    "PromptVersions",
                    "Prompts",
                    "Categories",
                    "AspNetUsers"
                RESTART IDENTITY CASCADE;
                """);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
