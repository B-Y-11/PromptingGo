using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.Moderation;

public sealed class PromptDuplicateDetectionService
    : IPromptDuplicateDetectionService
{
    private const int CandidatePoolSize =
        25;

    private const int MaxResults =
        10;

    private const double MinimumDisplayedSimilarity =
        0.20;

    private readonly ApplicationDbContext
        _dbContext;

    public PromptDuplicateDetectionService(
        ApplicationDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }

    public async Task<IReadOnlyList<PromptDuplicateMatch>>
        FindSimilarPublishedAsync(
            int promptId,
            CancellationToken cancellationToken = default)
    {
        // ---------------------------------------------------------------------
        // EN: Only the version currently awaiting moderation is checked.
        // RU: Проверяем только версию, которая сейчас находится на модерации.
        // ---------------------------------------------------------------------

        var sourceVersion =
            await _dbContext.PromptVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    version =>
                        version.PromptId ==
                            promptId &&
                        version.Status ==
                            PromptVersionStatus
                                .PendingModeration,
                    cancellationToken);

        if (sourceVersion is null)
        {
            return Array.Empty<
                PromptDuplicateMatch>();
        }

        var hasReadyText =
            !string.IsNullOrWhiteSpace(
                sourceVersion.ReadyText);

        var hasTemplateText =
            !string.IsNullOrWhiteSpace(
                sourceVersion.TemplateText);

        if (!hasReadyText &&
            !hasTemplateText)
        {
            return Array.Empty<
                PromptDuplicateMatch>();
        }

        // ---------------------------------------------------------------------
        // EN: Search only against the current public versions of other prompts.
        //
        // RU: Сравниваем только с текущими публичными версиями других
        //     промптов. Версии того же PromptId специально исключаются,
        //     иначе обычное обновление v2 почти всегда совпадёт с v1.
        // ---------------------------------------------------------------------

        IQueryable<PromptVersion>
            publishedCandidates =
                _dbContext.PromptVersions
                    .AsNoTracking()
                    .Where(
                        version =>
                            version.PromptId !=
                                sourceVersion.PromptId &&
                            version.IsCurrent &&
                            version.Status ==
                                PromptVersionStatus
                                    .Published &&
                            version.Prompt.Status ==
                                PromptStatus.Published &&
                            version.Prompt.Category
                                .IsActive);

        var candidateVersionIds =
            new HashSet<int>();

        // ---------------------------------------------------------------------
        // EN: Find nearest candidates by ready prompt text.
        // RU: Ищем ближайшие варианты по готовому тексту промпта.
        // ---------------------------------------------------------------------

        if (hasReadyText)
        {
            var readyCandidateIds =
                await publishedCandidates
                    .Where(
                        version =>
                            !string.IsNullOrWhiteSpace(
                                version.ReadyText))
                    .OrderBy(
                        version =>
                            EF.Functions
                                .TrigramsSimilarityDistance(
                                    version.ReadyText,
                                    sourceVersion.ReadyText))
                    .Select(
                        version =>
                            version.Id)
                    .Take(
                        CandidatePoolSize)
                    .ToListAsync(
                        cancellationToken);

            candidateVersionIds.UnionWith(
                readyCandidateIds);
        }

        // ---------------------------------------------------------------------
        // EN: Find nearest candidates by constructor template.
        // RU: Ищем ближайшие варианты по шаблону конструктора.
        // ---------------------------------------------------------------------

        if (hasTemplateText)
        {
            var templateCandidateIds =
                await publishedCandidates
                    .Where(
                        version =>
                            !string.IsNullOrWhiteSpace(
                                version.TemplateText))
                    .OrderBy(
                        version =>
                            EF.Functions
                                .TrigramsSimilarityDistance(
                                    version.TemplateText,
                                    sourceVersion.TemplateText))
                    .Select(
                        version =>
                            version.Id)
                    .Take(
                        CandidatePoolSize)
                    .ToListAsync(
                        cancellationToken);

            candidateVersionIds.UnionWith(
                templateCandidateIds);
        }

        if (candidateVersionIds.Count == 0)
        {
            return Array.Empty<
                PromptDuplicateMatch>();
        }

        var ids =
            candidateVersionIds
                .ToArray();

        // ---------------------------------------------------------------------
        // EN: Load public metadata for the candidate set.
        // RU: Загружаем публичные данные найденных кандидатов.
        // ---------------------------------------------------------------------

        var candidateMetadata =
            await publishedCandidates
                .Where(
                    version =>
                        ids.Contains(
                            version.Id))
                .Select(
                    version =>
                        new
                        {
                            VersionId =
                                version.Id,

                            version.PromptId,

                            version.Title,

                            version.Prompt.Slug,

                            AuthorDisplayName =
                                version.Prompt.Author
                                    .DisplayName,

                            AuthorUserName =
                                version.Prompt.Author
                                    .UserName,

                            PublishedAtUtc =
                                version.PublishedAtUtc ??
                                version.Prompt
                                    .PublishedAtUtc
                        })
                .ToListAsync(
                    cancellationToken);

        var readyScores =
            new Dictionary<int, double>();

        if (hasReadyText)
        {
            readyScores =
                await publishedCandidates
                    .Where(
                        version =>
                            ids.Contains(
                                version.Id) &&
                            !string.IsNullOrWhiteSpace(
                                version.ReadyText))
                    .Select(
                        version =>
                            new
                            {
                                version.Id,

                                Similarity =
                                    EF.Functions
                                        .TrigramsSimilarity(
                                            version.ReadyText,
                                            sourceVersion
                                                .ReadyText)
                            })
                    .ToDictionaryAsync(
                        item =>
                            item.Id,
                        item =>
                            item.Similarity,
                        cancellationToken);
        }

        var templateScores =
            new Dictionary<int, double>();

        if (hasTemplateText)
        {
            templateScores =
                await publishedCandidates
                    .Where(
                        version =>
                            ids.Contains(
                                version.Id) &&
                            !string.IsNullOrWhiteSpace(
                                version.TemplateText))
                    .Select(
                        version =>
                            new
                            {
                                version.Id,

                                Similarity =
                                    EF.Functions
                                        .TrigramsSimilarity(
                                            version.TemplateText,
                                            sourceVersion
                                                .TemplateText)
                            })
                    .ToDictionaryAsync(
                        item =>
                            item.Id,
                        item =>
                            item.Similarity,
                        cancellationToken);
        }

        // ---------------------------------------------------------------------
        // EN: The main score is the strongest match of the two principal
        //     prompt representations.
        //
        // RU: Итоговый процент — максимальное совпадение среди двух основных
        //     представлений промпта: готового текста и шаблона конструктора.
        // ---------------------------------------------------------------------

        return candidateMetadata
            .Select(
                candidate =>
                {
                    readyScores.TryGetValue(
                        candidate.VersionId,
                        out var readySimilarity);

                    templateScores.TryGetValue(
                        candidate.VersionId,
                        out var templateSimilarity);

                    var overallSimilarity =
                        Math.Max(
                            readySimilarity,
                            templateSimilarity);

                    return new PromptDuplicateMatch
                    {
                        PromptId =
                            candidate.PromptId,

                        PromptVersionId =
                            candidate.VersionId,

                        Title =
                            candidate.Title,

                        Slug =
                            candidate.Slug,

                        AuthorName =
                            !string.IsNullOrWhiteSpace(
                                candidate.AuthorDisplayName)
                                ? candidate.AuthorDisplayName
                                : candidate.AuthorUserName ??
                                  "Автор",

                        PublishedAtUtc =
                            candidate.PublishedAtUtc,

                        Similarity =
                            overallSimilarity,

                        ReadyTextSimilarity =
                            readySimilarity,

                        TemplateTextSimilarity =
                            templateSimilarity
                    };
                })
            .Where(
                match =>
                    match.Similarity >=
                        MinimumDisplayedSimilarity)
            .OrderByDescending(
                match =>
                    match.Similarity)
            .ThenByDescending(
                match =>
                    match.PublishedAtUtc)
            .Take(
                MaxResults)
            .ToArray();
    }
}
