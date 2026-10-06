using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Data.Seeding;

public static class DevelopmentDataSeeder
{
    private const string DemoPromptSlug =
        "plan-kursovoy-raboty";

    public static async Task SeedAsync(
        IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevelopmentDataSeeder");

        var educationCategory =
            await dbContext.Categories
                .SingleOrDefaultAsync(
                    category =>
                        category.Slug == "ucheba");

        if (educationCategory is null)
        {
            educationCategory = new Category
            {
                Name = "Учёба",
                Slug = "ucheba",
                Description =
                    "Промпты для учёбы и подготовки учебных работ",
                SortOrder = 10,
                IsActive = true
            };

            dbContext.Categories.Add(
                educationCategory);
        }

        var courseworkCategory =
            await dbContext.Categories
                .SingleOrDefaultAsync(
                    category =>
                        category.Slug ==
                        "kursovye-raboty");

        if (courseworkCategory is null)
        {
            courseworkCategory = new Category
            {
                Name = "Курсовые работы",
                Slug = "kursovye-raboty",
                Description =
                    "Промпты для подготовки курсовых работ",
                Parent = educationCategory,
                SortOrder = 10,
                IsActive = true
            };

            dbContext.Categories.Add(
                courseworkCategory);
        }

        await dbContext.SaveChangesAsync();

        var promptAlreadyExists =
            await dbContext.Prompts.AnyAsync(
                prompt =>
                    prompt.Slug == DemoPromptSlug);

        if (promptAlreadyExists)
        {
            return;
        }

        var author = await dbContext.Users
            .OrderBy(user => user.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (author is null)
        {
            logger.LogWarning(
                "Демонстрационный промпт не создан: " +
                "в базе нет зарегистрированных пользователей.");

            return;
        }

        var promptVersion = new PromptVersion
        {
            VersionNumber = 1,
            IsCurrent = true,
            ChangeSummary = "Первая версия промпта",

            ReadyText =
                """
                Выступи в роли научного консультанта.

                Сначала задай мне необходимые вопросы:
                1. Какова тема работы?
                2. Какая у меня специальность?
                3. Какой это тип учебной работы?
                4. Сколько глав должно быть?
                5. Есть ли дополнительные требования?

                Задавай вопросы последовательно и дождись моих ответов.

                После получения информации составь подробный план работы.
                План должен включать введение, главы и параграфы,
                заключение и список использованных источников.

                Для каждого раздела кратко объясни, какие вопросы
                необходимо раскрыть. Используй научный стиль,
                но формулируй названия понятно и конкретно.
                """,

            UsageInstructions =
                """
                Заполните тему работы, специальность,
                тип работы и количество глав.
                Затем нажмите «Сформировать промпт».
                """,

            TemplateText =
                """
                Выступи в роли научного консультанта.

                Составь подробный план {{work_type}} на тему:
                «{{topic}}».

                Специальность: {{speciality}}.
                Количество глав: {{chapter_count}}.

                [[if requirements]]
                Дополнительные требования:
                {{requirements}}
                [[endif]]

                План должен включать:
                — введение;
                — главы и параграфы;
                — заключение;
                — список использованных источников.

                Для каждого раздела кратко объясни,
                какие вопросы необходимо раскрыть.

                Используй научный стиль и формулируй
                названия разделов понятно и конкретно.
                """,

            ExampleResult =
                """
                Введение

                Глава 1. Теоретические основы разработки чат-ботов
                1.1. Понятие и назначение чат-ботов
                1.2. Классификация чат-ботов
                1.3. Применение чат-ботов в аптечной сфере

                Глава 2. Проектирование Telegram-бота для аптеки
                2.1. Анализ требований
                2.2. Проектирование архитектуры
                2.3. Проектирование базы данных

                Глава 3. Реализация и тестирование системы
                3.1. Выбор средств разработки
                3.2. Реализация основных функций
                3.3. Тестирование приложения

                Заключение
                Список использованных источников
                """,

            Variables = new List<PromptVariable>
            {
                new()
                {
                    Key = "topic",
                    Label = "Тема работы",
                    VariableType =
                        PromptVariableType.Text,
                    Placeholder =
                        "Разработка Telegram-бота для аптеки",
                    HelpText =
                        "Введите полное название темы.",
                    IsRequired = true,
                    SortOrder = 10
                },
                new()
                {
                    Key = "speciality",
                    Label = "Специальность",
                    VariableType =
                        PromptVariableType.Text,
                    Placeholder =
                        "Прикладная информатика",
                    IsRequired = true,
                    SortOrder = 20
                },
                new()
                {
                    Key = "work_type",
                    Label = "Тип работы",
                    VariableType =
                        PromptVariableType.Select,
                    DefaultValue = "курсовой работы",
                    OptionsJson =
                        """
                        [
                          {
                            "value": "курсовой работы",
                            "label": "Курсовая работа"
                          },
                          {
                            "value": "выпускной квалификационной работы",
                            "label": "ВКР"
                          },
                          {
                            "value": "реферата",
                            "label": "Реферат"
                          }
                        ]
                        """,
                    IsRequired = true,
                    SortOrder = 30
                },
                new()
                {
                    Key = "chapter_count",
                    Label = "Количество глав",
                    VariableType =
                        PromptVariableType.Number,
                    DefaultValue = "3",
                    IsRequired = true,
                    SortOrder = 40
                },
                new()
                {
                    Key = "requirements",
                    Label = "Дополнительные требования",
                    VariableType =
                        PromptVariableType.TextArea,
                    Placeholder =
                        "Например: добавить практическую главу",
                    IsRequired = false,
                    SortOrder = 50
                }
            }
        };

        var prompt = new Prompt
        {
            Title =
                "Промпт для составления плана курсовой работы",
            Slug = DemoPromptSlug,
            Summary =
                "Поможет сформировать логичную структуру " +
                "курсовой работы с главами, параграфами " +
                "и кратким описанием содержания.",
            LanguageCode = "ru",
            Status = PromptStatus.Published,
            Difficulty = PromptDifficulty.Beginner,
            AuthorId = author.Id,
            CategoryId = courseworkCategory.Id,
            PublishedAtUtc = DateTime.UtcNow,
            Versions = new List<PromptVersion>
            {
                promptVersion
            }
        };

        dbContext.Prompts.Add(prompt);
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Создан демонстрационный промпт {Slug}.",
            DemoPromptSlug);
    }
}
