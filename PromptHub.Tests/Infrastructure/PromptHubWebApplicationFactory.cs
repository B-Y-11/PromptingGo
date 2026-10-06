using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.PromptStats;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Tests.Infrastructure;

public sealed class PromptHubWebApplicationFactory
    : WebApplicationFactory<Program>
{
    public const string AuthorId =
        "seo-test-author";

    public const int PublishedPromptId =
        1001;

    public const string PublishedPromptSlug =
        "seo-published-prompt";

    public const string DraftPromptSlug =
        "seo-draft-prompt";

    private const int CategoryId =
        100;

    private const int PublishedVersionId =
        2001;

    private const int DraftPromptId =
        1002;

    private const int DraftVersionId =
        2002;

    private readonly string _databaseName =
        $"PromptHubSeoTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(
            "Testing");

        builder.ConfigureServices(
            services =>
            {
                // -------------------------------------------------------------------------
                // EN: Integration tests use an isolated test-only OTP hashing key.
                // RU: Integration-тесты используют отдельный тестовый ключ хеширования OTP.
                // -------------------------------------------------------------------------

                services.PostConfigure<
                    PhoneOtpOptions>(
                        options =>
                        {
                            options.HashingKey =
                                "PromptHub-SEO-integration-tests-only-key-2026";
                        });
                // -------------------------------------------------------------
                // EN: Replace PostgreSQL with an isolated in-memory database.
                // RU: Заменяем PostgreSQL изолированной InMemory-базой.
                // -------------------------------------------------------------

                services.RemoveAll<
                    ApplicationDbContext>();

                services.RemoveAll<
                    DbContextOptions<ApplicationDbContext>>();

                services.RemoveAll<
                    IDbContextOptionsConfiguration<
                        ApplicationDbContext>>();

                services.AddDbContext<
                    ApplicationDbContext>(
                        options =>
                            options.UseInMemoryDatabase(
                                _databaseName));

                // -------------------------------------------------------------
                // EN: Public prompt rendering should not exercise the
                //     PostgreSQL-specific metric transaction in SEO tests.
                //
                // RU: SEO-тесты не должны проверять PostgreSQL-специфичную
                //     транзакцию статистики просмотров.
                // -------------------------------------------------------------

                services.RemoveAll<
                    IPromptMetricService>();

                services.AddScoped<
                    IPromptMetricService,
                    TestPromptMetricService>();

                // -------------------------------------------------------------
                // EN: Header-based authentication is used only by tests.
                // RU: Авторизация по заголовку существует только в тестах.
                // -------------------------------------------------------------

                services
                    .AddAuthentication(
                        options =>
                        {
                            options.DefaultAuthenticateScheme =
                                TestAuthenticationHandler
                                    .SchemeName;

                            options.DefaultChallengeScheme =
                                TestAuthenticationHandler
                                    .SchemeName;
                        })
                    .AddScheme<
                        AuthenticationSchemeOptions,
                        TestAuthenticationHandler>(
                            TestAuthenticationHandler
                                .SchemeName,
                            _ =>
                            {
                            });
            });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope =
            Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    ApplicationDbContext>();

        await dbContext.Database
            .EnsureDeletedAsync();

        await dbContext.Database
            .EnsureCreatedAsync();

        var now =
            new DateTime(
                2026,
                8,
                11,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var author =
            new ApplicationUser
            {
                Id =
                    AuthorId,

                UserName =
                    "seo-author",

                NormalizedUserName =
                    "SEO-AUTHOR",

                Email =
                    "seo-author@example.test",

                NormalizedEmail =
                    "SEO-AUTHOR@EXAMPLE.TEST",

                EmailConfirmed =
                    true,

                DisplayName =
                    "SEO Автор",

                CreatedAtUtc =
                    now
            };

        var category =
            new Category
            {
                Id =
                    CategoryId,

                Name =
                    "Программирование",

                Slug =
                    "programming",

                Description =
                    "Тестовая категория.",

                IsActive =
                    true,

                CreatedAtUtc =
                    now
            };

        var publishedPrompt =
            new Prompt
            {
                Id =
                    PublishedPromptId,

                Title =
                    "SEO опубликованный промпт",

                Slug =
                    PublishedPromptSlug,

                Summary =
                    "Описание опубликованного SEO-промпта.",

                LanguageCode =
                    "ru",

                Status =
                    PromptStatus.Published,

                Difficulty =
                    PromptDifficulty.Beginner,

                AuthorId =
                    AuthorId,

                Author =
                    author,

                CategoryId =
                    CategoryId,

                Category =
                    category,

                CreatedAtUtc =
                    now,

                PublishedAtUtc =
                    now
            };

        var publishedVersion =
            new PromptVersion
            {
                Id =
                    PublishedVersionId,

                PromptId =
                    PublishedPromptId,

                Prompt =
                    publishedPrompt,

                VersionNumber =
                    1,

                Title =
                    publishedPrompt.Title,

                Summary =
                    publishedPrompt.Summary,

                LanguageCode =
                    "ru",

                Difficulty =
                    PromptDifficulty.Beginner,

                CategoryId =
                    CategoryId,

                Category =
                    category,

                Status =
                    PromptVersionStatus.Published,

                ReadyText =
                    "Готовый SEO-текст.",

                TemplateText =
                    string.Empty,

                UsageInstructions =
                    string.Empty,

                ExampleResult =
                    string.Empty,

                ChangeSummary =
                    string.Empty,

                IsCurrent =
                    true,

                CreatedAtUtc =
                    now,

                PublishedAtUtc =
                    now
            };

        publishedPrompt.Versions.Add(
            publishedVersion);

        var draftPrompt =
            new Prompt
            {
                Id =
                    DraftPromptId,

                Title =
                    "SEO черновик",

                Slug =
                    DraftPromptSlug,

                Summary =
                    "Этот промпт не должен попасть в sitemap.",

                LanguageCode =
                    "ru",

                Status =
                    PromptStatus.Draft,

                Difficulty =
                    PromptDifficulty.Beginner,

                AuthorId =
                    AuthorId,

                Author =
                    author,

                CategoryId =
                    CategoryId,

                Category =
                    category,

                CreatedAtUtc =
                    now
            };

        var draftVersion =
            new PromptVersion
            {
                Id =
                    DraftVersionId,

                PromptId =
                    DraftPromptId,

                Prompt =
                    draftPrompt,

                VersionNumber =
                    1,

                Title =
                    draftPrompt.Title,

                Summary =
                    draftPrompt.Summary,

                LanguageCode =
                    "ru",

                Difficulty =
                    PromptDifficulty.Beginner,

                CategoryId =
                    CategoryId,

                Category =
                    category,

                Status =
                    PromptVersionStatus.Draft,

                ReadyText =
                    "Черновик.",

                TemplateText =
                    string.Empty,

                UsageInstructions =
                    string.Empty,

                ExampleResult =
                    string.Empty,

                ChangeSummary =
                    string.Empty,

                IsCurrent =
                    true,

                CreatedAtUtc =
                    now
            };

        draftPrompt.Versions.Add(
            draftVersion);

        dbContext.Users.Add(
            author);

        dbContext.Categories.Add(
            category);

        dbContext.Prompts.AddRange(
            publishedPrompt,
            draftPrompt);

        await dbContext.SaveChangesAsync();
    }

    public static HttpRequestMessage
        CreateAuthenticatedRequest(
            HttpMethod method,
            string requestUri)
    {
        var request =
            new HttpRequestMessage(
                method,
                requestUri);

        request.Headers.Add(
            TestAuthenticationHandler
                .UserIdHeaderName,
            AuthorId);

        return request;
    }

    private sealed class TestPromptMetricService
        : IPromptMetricService
    {
        public Task<
            PromptMetricRegistrationResult>
            RegisterAsync(
                int promptId,
                PromptMetricKind kind,
                string actorKeyHash,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new PromptMetricRegistrationResult(
                    PromptAvailable:
                        true,

                    Counted:
                        false,

                    CurrentCount:
                        10));
        }
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<
            AuthenticationSchemeOptions>
    {
        public const string SchemeName =
            "PromptHubTest";

        public const string UserIdHeaderName =
            "X-PromptHub-Test-User";

        public TestAuthenticationHandler(
            IOptionsMonitor<
                AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(
                options,
                logger,
                encoder)
        {
        }

        protected override Task<
            AuthenticateResult>
            HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(
                    UserIdHeaderName,
                    out var values))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var userId =
                values.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(
                    userId))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

            var claims =
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId),

                    new Claim(
                        ClaimTypes.Name,
                        "SEO Автор")
                };

            var identity =
                new ClaimsIdentity(
                    claims,
                    SchemeName);

            var principal =
                new ClaimsPrincipal(
                    identity);

            var ticket =
                new AuthenticationTicket(
                    principal,
                    SchemeName);

            return Task.FromResult(
                AuthenticateResult.Success(
                    ticket));
        }
    }
}
