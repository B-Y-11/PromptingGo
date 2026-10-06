using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using Testcontainers.PostgreSql;

namespace PromptHub.Tests.Infrastructure;

public sealed class PostgreSqlFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer
        _container =
            new PostgreSqlBuilder(
                "postgres:18-alpine")
                .WithDatabase(
                    "prompthub_tests")
                .WithUsername(
                    "postgres")
                .WithPassword(
                    "postgres")
                .Build();

    public string ConnectionString =>
        _container.GetConnectionString();


    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }


    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }


    public ApplicationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(
                    ConnectionString)
                .EnableDetailedErrors()
                .Options;

        return new ApplicationDbContext(
            options);
    }


    public async Task ResetDatabaseAsync()
    {
        await using var dbContext =
            CreateDbContext();

        // -------------------------------------------------------------------------
        // EN: Recreate the disposable PostgreSQL schema from the current EF model.
        // RU: Пересоздаём временную PostgreSQL-схему из текущей EF-модели.
        // -------------------------------------------------------------------------

        await dbContext.Database
            .EnsureDeletedAsync();

        await dbContext.Database
            .EnsureCreatedAsync();
    }
}