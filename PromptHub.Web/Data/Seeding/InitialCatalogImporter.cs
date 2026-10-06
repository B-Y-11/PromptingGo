using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.PromptTemplates;
using PromptHub.Web.Services.Slugs;

namespace PromptHub.Web.Data.Seeding;

public sealed class InitialCatalogImporter
{
    private const string CatalogRelativePath =
        "Data/SeedData/initial-catalog-pack-01.json";

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ISlugGenerator _slugGenerator;
    private readonly IVisualPromptTemplateCompiler _templateCompiler;

    public InitialCatalogImporter(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment,
        ISlugGenerator slugGenerator,
        IVisualPromptTemplateCompiler templateCompiler)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _environment = environment;
        _slugGenerator = slugGenerator;
        _templateCompiler = templateCompiler;
    }

    public async Task<InitialCatalogImportResult> ImportAsync(
        string authorEmail,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(authorEmail))
        {
            errors.Add("Не указан email автора стартового каталога.");
            return InitialCatalogImportResult.Failure(errors);
        }

        var author = await _userManager.FindByEmailAsync(
            authorEmail.Trim());

        if (author is null)
        {
            errors.Add(
                $"Пользователь с email «{authorEmail.Trim()}» не найден.");

            return InitialCatalogImportResult.Failure(errors);
        }

        var catalogPath = Path.Combine(
            _environment.ContentRootPath,
            CatalogRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(catalogPath))
        {
            errors.Add(
                $"Файл стартового каталога не найден: {catalogPath}");

            return InitialCatalogImportResult.Failure(errors);
        }

        InitialCatalogFile? catalog;

        try
        {
            await using var stream = File.OpenRead(catalogPath);

            catalog = await JsonSerializer.DeserializeAsync<InitialCatalogFile>(
                stream,
                CreateJsonOptions(),
                cancellationToken);
        }
        catch (JsonException exception)
        {
            errors.Add(
                $"Не удалось прочитать JSON стартового каталога: {exception.Message}");

            return InitialCatalogImportResult.Failure(errors);
        }

        if (catalog is null || catalog.Prompts.Count == 0)
        {
            errors.Add("Стартовый каталог не содержит промптов.");
            return InitialCatalogImportResult.Failure(errors);
        }

        var categorySlugs = catalog.Prompts
            .Select(prompt => prompt.CategorySlug.Trim())
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tagSlugs = catalog.Prompts
            .SelectMany(prompt => prompt.TagSlugs)
            .Select(slug => slug.Trim())
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var categoryRows = await _dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                category.IsActive &&
                categorySlugs.Contains(category.Slug))
            .ToListAsync(cancellationToken);

        var categories = categoryRows.ToDictionary(
            category => category.Slug,
            category => category,
            StringComparer.OrdinalIgnoreCase);

        var tagRows = await _dbContext.Tags
            .AsNoTracking()
            .Where(tag =>
                tag.IsActive &&
                tagSlugs.Contains(tag.Slug))
            .ToListAsync(cancellationToken);

        var tags = tagRows.ToDictionary(
            tag => tag.Slug,
            tag => tag,
            StringComparer.OrdinalIgnoreCase);

        foreach (var categorySlug in categorySlugs)
        {
            if (!categories.ContainsKey(categorySlug))
            {
                errors.Add(
                    $"Не найдена активная категория со slug «{categorySlug}».");
            }
        }

        foreach (var tagSlug in tagSlugs)
        {
            if (!tags.ContainsKey(tagSlug))
            {
                errors.Add(
                    $"Не найден активный тег со slug «{tagSlug}».");
            }
        }

        var preparedPrompts = new List<PreparedPrompt>();
        var slugsInsideFile = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < catalog.Prompts.Count; index++)
        {
            var definition = catalog.Prompts[index];
            var number = index + 1;

            ValidateDefinition(
                definition,
                number,
                errors);

            if (string.IsNullOrWhiteSpace(definition.Title))
            {
                continue;
            }

            var slug = _slugGenerator.Generate(definition.Title);

            if (!slugsInsideFile.Add(slug))
            {
                errors.Add(
                    $"Промпт №{number}: slug «{slug}» повторяется внутри JSON-файла.");
            }

            if (!categories.TryGetValue(
                    definition.CategorySlug.Trim(),
                    out var category))
            {
                continue;
            }

            var selectedTags = new List<Tag>();
            var missingTag = false;

            foreach (var tagSlug in definition.TagSlugs
                         .Select(value => value.Trim())
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!tags.TryGetValue(tagSlug, out var tag))
                {
                    missingTag = true;
                    break;
                }

                selectedTags.Add(tag);
            }

            if (missingTag)
            {
                continue;
            }

            var fields = definition.Fields
                .Select(field => new VisualPromptFieldDefinition
                {
                    Label = field.Label.Trim(),
                    VariableType = field.VariableType,
                    IsRequired = field.IsRequired,
                    Placeholder = field.Placeholder.Trim(),
                    HelpText = field.HelpText.Trim(),
                    Options = field.Options
                        .Select(option => option.Trim())
                        .Where(option => !string.IsNullOrWhiteSpace(option))
                        .ToList()
                })
                .ToArray();

            var compileResult = _templateCompiler.Compile(
                definition.TemplateText,
                fields);

            if (!compileResult.IsSuccess)
            {
                foreach (var error in compileResult.Errors)
                {
                    errors.Add(
                        $"Промпт №{number} «{definition.Title}»: {error}");
                }

                continue;
            }

            var readyText = BuildReadyText(
                definition,
                number,
                errors);

            if (string.IsNullOrWhiteSpace(readyText))
            {
                continue;
            }

            preparedPrompts.Add(
                new PreparedPrompt(
                    Definition: definition,
                    Slug: slug,
                    Category: category,
                    Tags: selectedTags,
                    ReadyText: readyText,
                    CompileResult: compileResult));
        }

        if (errors.Count > 0)
        {
            return InitialCatalogImportResult.Failure(errors);
        }

        var preparedSlugs = preparedPrompts
            .Select(prompt => prompt.Slug)
            .ToArray();

        var existingSlugRows = await _dbContext.Prompts
            .AsNoTracking()
            .Where(prompt => preparedSlugs.Contains(prompt.Slug))
            .Select(prompt => prompt.Slug)
            .ToListAsync(cancellationToken);

        var existingSlugs = existingSlugRows.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        var importedCount = 0;
        var skippedCount = 0;
        var nowUtc = DateTime.UtcNow;

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        foreach (var prepared in preparedPrompts)
        {
            if (existingSlugs.Contains(prepared.Slug))
            {
                skippedCount++;
                continue;
            }

            var version = new PromptVersion
            {
                VersionNumber = 1,
                Title = prepared.Definition.Title.Trim(),
                Summary = prepared.Definition.Summary.Trim(),
                LanguageCode = "ru",
                Difficulty = prepared.Definition.Difficulty,
                CategoryId = prepared.Category.Id,
                Status = PromptVersionStatus.Published,
                ReadyText = prepared.ReadyText,
                TemplateText = prepared.CompileResult.TemplateText,
                UsageInstructions = prepared.Definition.UsageInstructions.Trim(),
                ExampleResult = prepared.Definition.ExampleResult.Trim(),
                ChangeSummary = "Стартовый каталог",
                IsCurrent = true,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                SubmittedAtUtc = nowUtc,
                ModeratedAtUtc = nowUtc,
                PublishedAtUtc = nowUtc,
                ModeratedById = author.Id,
                Variables = prepared.CompileResult.Variables
                    .Select(variable => new PromptVariable
                    {
                        Key = variable.Key,
                        Label = variable.Label,
                        VariableType = variable.VariableType,
                        Placeholder = variable.Placeholder,
                        HelpText = variable.HelpText,
                        IsRequired = variable.IsRequired,
                        DefaultValue = variable.DefaultValue,
                        OptionsJson = variable.OptionsJson,
                        SortOrder = variable.SortOrder
                    })
                    .ToList()
            };

            foreach (var tag in prepared.Tags)
            {
                version.VersionTags.Add(
                    new PromptVersionTag
                    {
                        TagId = tag.Id
                    });
            }

            var prompt = new Prompt
            {
                Title = prepared.Definition.Title.Trim(),
                Slug = prepared.Slug,
                Summary = prepared.Definition.Summary.Trim(),
                LanguageCode = "ru",
                Status = PromptStatus.Published,
                Difficulty = prepared.Definition.Difficulty,
                AuthorId = author.Id,
                CategoryId = prepared.Category.Id,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                SubmittedAtUtc = nowUtc,
                ModeratedAtUtc = nowUtc,
                PublishedAtUtc = nowUtc,
                ModeratedById = author.Id,
                Versions = new List<PromptVersion>
                {
                    version
                }
            };

            foreach (var tag in prepared.Tags)
            {
                prompt.PromptTags.Add(
                    new PromptTag
                    {
                        TagId = tag.Id
                    });
            }

            _dbContext.Prompts.Add(prompt);
            importedCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return InitialCatalogImportResult.Success(
            importedCount,
            skippedCount);
    }

    private static void ValidateDefinition(
        CatalogPromptDefinition definition,
        int number,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(definition.Title))
        {
            errors.Add($"Промпт №{number}: отсутствует название.");
        }
        else if (definition.Title.Trim().Length > 180)
        {
            errors.Add(
                $"Промпт №{number}: название превышает 180 символов.");
        }

        if (string.IsNullOrWhiteSpace(definition.Summary))
        {
            errors.Add($"Промпт №{number}: отсутствует краткое описание.");
        }
        else if (definition.Summary.Trim().Length > 700)
        {
            errors.Add(
                $"Промпт №{number}: краткое описание превышает 700 символов.");
        }

        if (string.IsNullOrWhiteSpace(definition.CategorySlug))
        {
            errors.Add($"Промпт №{number}: не указана категория.");
        }

        if (definition.TagSlugs.Count == 0)
        {
            errors.Add($"Промпт №{number}: не указан ни один тег.");
        }

        if (string.IsNullOrWhiteSpace(definition.TemplateText))
        {
            errors.Add($"Промпт №{number}: отсутствует текст промпта.");
        }

        if (string.IsNullOrWhiteSpace(definition.UsageInstructions))
        {
            errors.Add($"Промпт №{number}: отсутствует инструкция.");
        }

        if (string.IsNullOrWhiteSpace(definition.ExampleResult))
        {
            errors.Add($"Промпт №{number}: отсутствует пример результата.");
        }

        if (definition.Fields.Count == 0)
        {
            errors.Add($"Промпт №{number}: отсутствуют поля конструктора.");
            return;
        }

        var labels = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (var fieldIndex = 0;
             fieldIndex < definition.Fields.Count;
             fieldIndex++)
        {
            var field = definition.Fields[fieldIndex];
            var fieldNumber = fieldIndex + 1;
            var label = field.Label.Trim();

            if (string.IsNullOrWhiteSpace(label))
            {
                errors.Add(
                    $"Промпт №{number}, поле №{fieldNumber}: отсутствует название.");

                continue;
            }

            if (!labels.Add(label))
            {
                errors.Add(
                    $"Промпт №{number}: поле «{label}» повторяется.");
            }

            if (string.IsNullOrWhiteSpace(field.ExampleValue))
            {
                errors.Add(
                    $"Промпт №{number}: для поля «{label}» не задан exampleValue.");
            }

            if (field.VariableType == PromptVariableType.Select)
            {
                var normalizedOptions = field.Options
                    .Select(option => option.Trim())
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!normalizedOptions.Contains(field.ExampleValue.Trim()))
                {
                    errors.Add(
                        $"Промпт №{number}: exampleValue поля «{label}» отсутствует среди options.");
                }
            }
        }
    }

    private static string BuildReadyText(
        CatalogPromptDefinition definition,
        int number,
        ICollection<string> errors)
    {
        var readyText = definition.TemplateText;

        foreach (var field in definition.Fields)
        {
            var marker = $"[{field.Label.Trim()}]";
            var exampleValue = field.ExampleValue.Trim();

            if (string.IsNullOrWhiteSpace(exampleValue))
            {
                continue;
            }

            if (!readyText.Contains(
                    marker,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Промпт №{number}: маркер «{marker}» не найден в templateText.");

                continue;
            }

            readyText = readyText.Replace(
                marker,
                exampleValue,
                StringComparison.Ordinal);
        }

        return readyText.Trim();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }

    private sealed class InitialCatalogFile
    {
        public int Version { get; set; }

        public List<CatalogPromptDefinition> Prompts { get; set; }
            = new();
    }

    private sealed class CatalogPromptDefinition
    {
        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string CategorySlug { get; set; } = string.Empty;

        public List<string> TagSlugs { get; set; } = new();

        public PromptDifficulty Difficulty { get; set; }
            = PromptDifficulty.Beginner;

        public string TemplateText { get; set; } = string.Empty;

        public string UsageInstructions { get; set; } = string.Empty;

        public string ExampleResult { get; set; } = string.Empty;

        public List<CatalogFieldDefinition> Fields { get; set; }
            = new();
    }

    private sealed class CatalogFieldDefinition
    {
        public string Label { get; set; } = string.Empty;

        public PromptVariableType VariableType { get; set; }
            = PromptVariableType.Text;

        public bool IsRequired { get; set; } = true;

        public string Placeholder { get; set; } = string.Empty;

        public string HelpText { get; set; } = string.Empty;

        public List<string> Options { get; set; } = new();

        public string ExampleValue { get; set; } = string.Empty;
    }

    private sealed record PreparedPrompt(
        CatalogPromptDefinition Definition,
        string Slug,
        Category Category,
        IReadOnlyList<Tag> Tags,
        string ReadyText,
        VisualPromptTemplateCompileResult CompileResult);
}

public sealed class InitialCatalogImportResult
{
    private InitialCatalogImportResult()
    {
    }

    public int ImportedCount { get; init; }

    public int SkippedCount { get; init; }

    public IReadOnlyList<string> Errors { get; init; }
        = Array.Empty<string>();

    public bool IsSuccess => Errors.Count == 0;

    public static InitialCatalogImportResult Success(
        int importedCount,
        int skippedCount)
    {
        return new InitialCatalogImportResult
        {
            ImportedCount = importedCount,
            SkippedCount = skippedCount
        };
    }

    public static InitialCatalogImportResult Failure(
        IReadOnlyList<string> errors)
    {
        return new InitialCatalogImportResult
        {
            Errors = errors.ToArray()
        };
    }
}
