using PromptHub.Tests.Infrastructure;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.Moderation;

namespace PromptHub.Tests.Services.Moderation;

public sealed class PromptDuplicateDetectionServiceTests
    : IClassFixture<
        PromptDuplicatePostgresFixture>,
      IAsyncLifetime
{
    private const string AuthorId =
        "duplicate-test-author";

    private const int CategoryId =
        100;

    private readonly PromptDuplicatePostgresFixture
        _fixture;

    public PromptDuplicateDetectionServiceTests(
        PromptDuplicatePostgresFixture fixture)
    {
        _fixture =
            fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();

        await SeedBaseDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // EN: Exact copies must produce a 100% match.
    // RU: Точная копия должна давать совпадение 100%.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExactCopy_ReturnsOneHundredPercent()
    {
        const string text =
            """
            Ты — опытный академический консультант.
            Составь подробный план курсовой работы по заданной теме.
            Раздели работу на введение, две главы, параграфы,
            заключение и список использованных источников.
            Для каждого параграфа кратко объясни его содержание.
            """;

        await CreatePublishedPromptAsync(
            promptId:
                1001,
            versionId:
                2001,
            slug:
                "original-course-plan",
            title:
                "Оригинальный промпт",
            readyText:
                text);

        var sourcePromptId =
            await CreatePendingPromptAsync(
                promptId:
                    1002,
                versionId:
                    2002,
                slug:
                    "copied-course-plan",
                title:
                    "Копия промпта",
                readyText:
                    text);

        await using var dbContext =
            _fixture.CreateDbContext();

        var service =
            new PromptDuplicateDetectionService(
                dbContext);

        var matches =
            await service
                .FindSimilarPublishedAsync(
                    sourcePromptId);

        var match =
            Assert.Single(
                matches);

        Assert.Equal(
            1001,
            match.PromptId);

        Assert.Equal(
            100,
            match.SimilarityPercent);

        Assert.Equal(
            100,
            match.ReadyTextSimilarityPercent);
    }

    // -------------------------------------------------------------------------
    // EN: Small wording changes should still produce a strong match.
    // RU: Небольшие изменения формулировок должны сохранять высокое сходство.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SlightlyChangedCopy_ReturnsHighSimilarity()
    {
        const string originalText =
            """
            Ты — опытный академический консультант.
            Составь подробный план курсовой работы по заданной теме.
            Раздели работу на введение, две главы, параграфы,
            заключение и список использованных источников.
            Для каждого параграфа кратко объясни его содержание.
            Учитывай академический стиль и логическую связь разделов.
            """;

        const string changedText =
            """
            Ты — профессиональный академический консультант.
            Составь подробный план курсовой работы по указанной теме.
            Раздели работу на введение, две главы, параграфы,
            заключение и список использованных источников.
            Для каждого параграфа кратко объясни его содержание.
            Учитывай академический стиль и логическую связь разделов.
            """;

        await CreatePublishedPromptAsync(
            promptId:
                1101,
            versionId:
                2101,
            slug:
                "original-slight-copy",
            title:
                "Исходный промпт",
            readyText:
                originalText);

        var sourcePromptId =
            await CreatePendingPromptAsync(
                promptId:
                    1102,
                versionId:
                    2102,
                slug:
                    "changed-slight-copy",
                title:
                    "Изменённая копия",
                readyText:
                    changedText);

        await using var dbContext =
            _fixture.CreateDbContext();

        var service =
            new PromptDuplicateDetectionService(
                dbContext);

        var matches =
            await service
                .FindSimilarPublishedAsync(
                    sourcePromptId);

        var match =
            Assert.Single(
                matches);

        Assert.Equal(
            1101,
            match.PromptId);

        Assert.True(
            match.SimilarityPercent >= 70,
            $"Ожидалось высокое сходство, получено {match.SimilarityPercent}%.");
    }

    // -------------------------------------------------------------------------
    // EN: Completely unrelated prompts should not be reported.
    // RU: Совершенно разные промпты не должны попадать в результаты.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UnrelatedPrompt_IsNotReturned()
    {
        const string publishedText =
            """
            Ты — персональный фитнес-тренер.
            Составь недельную программу силовых тренировок
            для спортсмена среднего уровня.
            Укажи упражнения, количество подходов,
            повторений и дни восстановления.
            """;

        const string pendingText =
            """
            Проанализируй SQL-запрос к PostgreSQL.
            Найди возможные проблемы производительности,
            предложи подходящие индексы и объясни,
            как изменится план выполнения запроса.
            """;

        await CreatePublishedPromptAsync(
            promptId:
                1201,
            versionId:
                2201,
            slug:
                "fitness-program",
            title:
                "Фитнес-программа",
            readyText:
                publishedText);

        var sourcePromptId =
            await CreatePendingPromptAsync(
                promptId:
                    1202,
                versionId:
                    2202,
                slug:
                    "sql-analysis",
                title:
                    "Анализ SQL",
                readyText:
                    pendingText);

        await using var dbContext =
            _fixture.CreateDbContext();

        var service =
            new PromptDuplicateDetectionService(
                dbContext);

        var matches =
            await service
                .FindSimilarPublishedAsync(
                    sourcePromptId);

        Assert.Empty(
            matches);
    }

    // -------------------------------------------------------------------------
    // EN: A pending update must never be compared with the published version
    //     of the same prompt.
    //
    // RU: Обновление опубликованного промпта не должно считаться дубликатом
    //     его собственной предыдущей версии.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishedVersionOfSamePrompt_IsExcluded()
    {
        const string text =
            """
            Подготовь структурированный анализ бизнес-идеи.
            Определи целевую аудиторию, преимущества продукта,
            риски, конкурентов и возможную модель монетизации.
            """;

        await using (
            var dbContext =
                _fixture.CreateDbContext())
        {
            var prompt =
                new Prompt
                {
                    Id =
                        1301,

                    Title =
                        "Анализ бизнес-идеи",

                    Slug =
                        "business-analysis",

                    Summary =
                        "Анализ бизнес-идеи.",

                    LanguageCode =
                        "ru",

                    Status =
                        PromptStatus.Published,

                    Difficulty =
                        PromptDifficulty.Beginner,

                    AuthorId =
                        AuthorId,

                    CategoryId =
                        CategoryId,

                    CreatedAtUtc =
                        DateTime.UtcNow,

                    PublishedAtUtc =
                        DateTime.UtcNow
                };

            var publishedVersion =
                new PromptVersion
                {
                    Id =
                        2301,

                    Prompt =
                        prompt,

                    VersionNumber =
                        1,

                    Title =
                        prompt.Title,

                    Summary =
                        prompt.Summary,

                    LanguageCode =
                        "ru",

                    Difficulty =
                        PromptDifficulty.Beginner,

                    CategoryId =
                        CategoryId,

                    Status =
                        PromptVersionStatus.Published,

                    ReadyText =
                        text,

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
                        DateTime.UtcNow,

                    PublishedAtUtc =
                        DateTime.UtcNow
                };

            var pendingVersion =
                new PromptVersion
                {
                    Id =
                        2302,

                    Prompt =
                        prompt,

                    VersionNumber =
                        2,

                    Title =
                        prompt.Title,

                    Summary =
                        prompt.Summary,

                    LanguageCode =
                        "ru",

                    Difficulty =
                        PromptDifficulty.Beginner,

                    CategoryId =
                        CategoryId,

                    Status =
                        PromptVersionStatus.PendingModeration,

                    ReadyText =
                        text,

                    TemplateText =
                        string.Empty,

                    UsageInstructions =
                        string.Empty,

                    ExampleResult =
                        string.Empty,

                    ChangeSummary =
                        string.Empty,

                    IsCurrent =
                        false,

                    CreatedAtUtc =
                        DateTime.UtcNow,

                    SubmittedAtUtc =
                        DateTime.UtcNow
                };

            prompt.Versions.Add(
                publishedVersion);

            prompt.Versions.Add(
                pendingVersion);

            dbContext.Prompts.Add(
                prompt);

            await dbContext.SaveChangesAsync();
        }

        await using var verificationContext =
            _fixture.CreateDbContext();

        var service =
            new PromptDuplicateDetectionService(
                verificationContext);

        var matches =
            await service
                .FindSimilarPublishedAsync(
                    1301);

        Assert.Empty(
            matches);
    }

    private async Task SeedBaseDataAsync()
    {
        await using var dbContext =
            _fixture.CreateDbContext();

        dbContext.Users.Add(
            new ApplicationUser
            {
                Id =
                    AuthorId,

                UserName =
                    "duplicate-test-author",

                NormalizedUserName =
                    "DUPLICATE-TEST-AUTHOR",

                DisplayName =
                    "Тестовый автор",

                CreatedAtUtc =
                    DateTime.UtcNow
            });

        dbContext.Categories.Add(
            new Category
            {
                Id =
                    CategoryId,

                Name =
                    "Тестовая категория",

                Slug =
                    "duplicate-tests",

                Description =
                    "Категория для тестов.",

                IsActive =
                    true,

                CreatedAtUtc =
                    DateTime.UtcNow
            });

        await dbContext.SaveChangesAsync();
    }

    private async Task<int>
        CreatePublishedPromptAsync(
            int promptId,
            int versionId,
            string slug,
            string title,
            string readyText)
    {
        await using var dbContext =
            _fixture.CreateDbContext();

        var prompt =
            new Prompt
            {
                Id =
                    promptId,

                Title =
                    title,

                Slug =
                    slug,

                Summary =
                    "Опубликованный тестовый промпт.",

                LanguageCode =
                    "ru",

                Status =
                    PromptStatus.Published,

                Difficulty =
                    PromptDifficulty.Beginner,

                AuthorId =
                    AuthorId,

                CategoryId =
                    CategoryId,

                CreatedAtUtc =
                    DateTime.UtcNow,

                PublishedAtUtc =
                    DateTime.UtcNow
            };

        prompt.Versions.Add(
            new PromptVersion
            {
                Id =
                    versionId,

                Prompt =
                    prompt,

                VersionNumber =
                    1,

                Title =
                    title,

                Summary =
                    prompt.Summary,

                LanguageCode =
                    "ru",

                Difficulty =
                    PromptDifficulty.Beginner,

                CategoryId =
                    CategoryId,

                Status =
                    PromptVersionStatus.Published,

                ReadyText =
                    readyText,

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
                    DateTime.UtcNow,

                PublishedAtUtc =
                    DateTime.UtcNow
            });

        dbContext.Prompts.Add(
            prompt);

        await dbContext.SaveChangesAsync();

        return promptId;
    }

    private async Task<int>
        CreatePendingPromptAsync(
            int promptId,
            int versionId,
            string slug,
            string title,
            string readyText)
    {
        await using var dbContext =
            _fixture.CreateDbContext();

        var prompt =
            new Prompt
            {
                Id =
                    promptId,

                Title =
                    title,

                Slug =
                    slug,

                Summary =
                    "Промпт на модерации.",

                LanguageCode =
                    "ru",

                Status =
                    PromptStatus.PendingModeration,

                Difficulty =
                    PromptDifficulty.Beginner,

                AuthorId =
                    AuthorId,

                CategoryId =
                    CategoryId,

                CreatedAtUtc =
                    DateTime.UtcNow,

                SubmittedAtUtc =
                    DateTime.UtcNow
            };

        prompt.Versions.Add(
            new PromptVersion
            {
                Id =
                    versionId,

                Prompt =
                    prompt,

                VersionNumber =
                    1,

                Title =
                    title,

                Summary =
                    prompt.Summary,

                LanguageCode =
                    "ru",

                Difficulty =
                    PromptDifficulty.Beginner,

                CategoryId =
                    CategoryId,

                Status =
                    PromptVersionStatus.PendingModeration,

                ReadyText =
                    readyText,

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
                    DateTime.UtcNow,

                SubmittedAtUtc =
                    DateTime.UtcNow
            });

        dbContext.Prompts.Add(
            prompt);

        await dbContext.SaveChangesAsync();

        return promptId;
    }
}
