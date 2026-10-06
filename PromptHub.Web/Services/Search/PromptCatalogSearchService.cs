using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.Search;

public sealed class PromptCatalogSearchService
    : IPromptCatalogSearchService
{
    // -------------------------------------------------------------------------
    // EN: Fuzzy matching is intentionally conservative.
    //     Generic summaries such as
    //     "for the task..." can admit unrelated prompts to the result set.
    //
    // RU: Нечёткий поиск намеренно сделан более строгим.
    //     Общие формулировки в Summary могут добавлять слишком много
    //     нерелевантных результатов.
    // -------------------------------------------------------------------------
    private const double TitleWordSimilarityThreshold = 0.34;
    private const double TagWordSimilarityThreshold = 0.46;

    public Task<IQueryable<Prompt>> ApplySearchAsync(
        IQueryable<Prompt> source,
        string search,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(source);

        var normalizedSearch =
            search.Trim();

        if (string.IsNullOrWhiteSpace(
                normalizedSearch))
        {
            return Task.FromResult(source);
        }

        var escapedSearch =
            EscapeLikePattern(
                normalizedSearch);

        var exactPattern =
            escapedSearch;

        var startsWithPattern =
            $"{escapedSearch}%";

        var containsPattern =
            $"%{escapedSearch}%";

        const string likeEscape = "\\";

        // ---------------------------------------------------------------------
        // EN: Admission to the result set uses only reasonably strong signals.
        //
        //     Admission rules:
        //     - Summary trigram similarity does not admit a prompt by itself.
        //     - Category trigram similarity does not admit a prompt by itself.
        //
        //     Summary/body still participate through full-text search and
        //     still contribute to ranking.
        //
        // RU: В выдачу пропускаем только по достаточно сильным сигналам.
        //
        //     Правила включения в выдачу:
        //     - fuzzy-совпадение Summary само по себе недостаточно;
        //     - fuzzy-совпадение категории само по себе недостаточно.
        //
        //     Summary и тело промпта по-прежнему участвуют через FTS и в score.
        // ---------------------------------------------------------------------
        Expression<Func<Prompt, bool>> matches =
            prompt =>
                // Exact phrase in public text.
                EF.Functions.ILike(
                    prompt.Title,
                    containsPattern,
                    likeEscape) ||
                EF.Functions.ILike(
                    prompt.Summary,
                    containsPattern,
                    likeEscape) ||

                // Russian full-text search requires the meaningful query terms
                // to be present, while handling morphology.
                EF.Functions
                    .ToTsVector(
                        "russian",
                        prompt.Title + " " +
                        prompt.Summary)
                    .Matches(
                        EF.Functions
                            .WebSearchToTsQuery(
                                "russian",
                                normalizedSearch)) ||

                // Conservative fuzzy title matching helps with wording variants
                // and small typos without opening the floodgates through Summary.
                EF.Functions
                    .TrigramsWordSimilarity(
                        normalizedSearch,
                        prompt.Title) >=
                    TitleWordSimilarityThreshold ||

                // Category can match lexically, but not merely by weak fuzziness.
                EF.Functions.ILike(
                    prompt.Category.Name,
                    containsPattern,
                    likeEscape) ||

                // Tags remain a strong navigation/search signal.
                prompt.PromptTags.Any(
                    promptTag =>
                        promptTag.Tag.IsActive &&
                        (
                            EF.Functions.ILike(
                                promptTag.Tag.Name,
                                containsPattern,
                                likeEscape) ||
                            EF.Functions.ILike(
                                promptTag.Tag.Slug,
                                containsPattern,
                                likeEscape) ||
                            EF.Functions
                                .TrigramsWordSimilarity(
                                    normalizedSearch,
                                    promptTag.Tag.Name) >=
                                TagWordSimilarityThreshold
                        )) ||

                // Body is recall-only: it can find a prompt when its actual
                // instructions contain the meaningful terms, but its rank stays
                // intentionally low below title/summary/tags.
                prompt.Versions.Any(
                    version =>
                        version.IsCurrent &&
                        version.Status ==
                            PromptVersionStatus.Published &&
                        EF.Functions
                            .ToTsVector(
                                "russian",
                                version.ReadyText + " " +
                                version.TemplateText)
                            .Matches(
                                EF.Functions
                                    .WebSearchToTsQuery(
                                        "russian",
                                        normalizedSearch)));

        Expression<Func<Prompt, double>> score =
            prompt =>
                // -------------------------------------------------------------
                // EN: Exact title wording dominates.
                // RU: Точное совпадение с заголовком имеет максимальный вес.
                // -------------------------------------------------------------
                (EF.Functions.ILike(
                    prompt.Title,
                    exactPattern,
                    likeEscape)
                    ? 160d
                    : 0d) +
                (EF.Functions.ILike(
                    prompt.Title,
                    startsWithPattern,
                    likeEscape)
                    ? 110d
                    : 0d) +
                (EF.Functions.ILike(
                    prompt.Title,
                    containsPattern,
                    likeEscape)
                    ? 85d
                    : 0d) +

                // -------------------------------------------------------------
                // EN: Title-only FTS is a very strong relevance signal.
                // RU: FTS только по заголовку — очень сильный сигнал.
                // -------------------------------------------------------------
                (EF.Functions
                    .ToTsVector(
                        "russian",
                        prompt.Title)
                    .Matches(
                        EF.Functions
                            .WebSearchToTsQuery(
                                "russian",
                                normalizedSearch))
                    ? 65d
                    : 0d) +
                EF.Functions
                    .ToTsVector(
                        "russian",
                        prompt.Title)
                    .RankCoverDensity(
                        EF.Functions
                            .WebSearchToTsQuery(
                                "russian",
                                normalizedSearch)) *
                    120d +

                // -------------------------------------------------------------
                // EN: Summary helps, but must not overpower a precise title.
                // RU: Summary помогает, но не должен перебивать точный title.
                // -------------------------------------------------------------
                (EF.Functions.ILike(
                    prompt.Summary,
                    containsPattern,
                    likeEscape)
                    ? 35d
                    : 0d) +
                (EF.Functions
                    .ToTsVector(
                        "russian",
                        prompt.Title + " " +
                        prompt.Summary)
                    .Matches(
                        EF.Functions
                            .WebSearchToTsQuery(
                                "russian",
                                normalizedSearch))
                    ? 35d
                    : 0d) +
                EF.Functions
                    .ToTsVector(
                        "russian",
                        prompt.Title + " " +
                        prompt.Summary)
                    .RankCoverDensity(
                        EF.Functions
                            .WebSearchToTsQuery(
                                "russian",
                                normalizedSearch)) *
                    70d +

                // -------------------------------------------------------------
                // EN: Fuzzy title matching stays useful for wording/typos.
                //     Summary fuzzy similarity is ranking-only and very weak.
                // RU: Fuzzy title нужен для формулировок/опечаток.
                //     Fuzzy Summary используется как слабый ranking-сигнал.
                // -------------------------------------------------------------
                EF.Functions
                    .TrigramsWordSimilarity(
                        normalizedSearch,
                        prompt.Title) *
                    60d +
                EF.Functions
                    .TrigramsWordSimilarity(
                        normalizedSearch,
                        prompt.Summary) *
                    8d +

                // -------------------------------------------------------------
                // EN: Category is helpful context, but less specific than tags.
                // RU: Категория полезна, но менее конкретна, чем теги.
                // -------------------------------------------------------------
                (EF.Functions.ILike(
                    prompt.Category.Name,
                    exactPattern,
                    likeEscape)
                    ? 45d
                    : 0d) +
                (EF.Functions.ILike(
                    prompt.Category.Name,
                    containsPattern,
                    likeEscape)
                    ? 20d
                    : 0d) +

                // -------------------------------------------------------------
                // EN: Exact/phrase tag matches are among the strongest signals.
                // RU: Точный/фразовый tag match — один из главных сигналов.
                // -------------------------------------------------------------
                (prompt.PromptTags.Any(
                    promptTag =>
                        promptTag.Tag.IsActive &&
                        EF.Functions.ILike(
                            promptTag.Tag.Name,
                            exactPattern,
                            likeEscape))
                    ? 120d
                    : 0d) +
                (prompt.PromptTags.Any(
                    promptTag =>
                        promptTag.Tag.IsActive &&
                        (
                            EF.Functions.ILike(
                                promptTag.Tag.Name,
                                containsPattern,
                                likeEscape) ||
                            EF.Functions.ILike(
                                promptTag.Tag.Slug,
                                containsPattern,
                                likeEscape)
                        ))
                    ? 75d
                    : 0d) +
                (prompt.PromptTags.Any(
                    promptTag =>
                        promptTag.Tag.IsActive &&
                        EF.Functions
                            .TrigramsWordSimilarity(
                                normalizedSearch,
                                promptTag.Tag.Name) >=
                            TagWordSimilarityThreshold)
                    ? 30d
                    : 0d) +

                // -------------------------------------------------------------
                // EN: Body is deliberately recall-only.
                // RU: Тело промпта намеренно имеет минимальный вес.
                // -------------------------------------------------------------
                (prompt.Versions.Any(
                    version =>
                        version.IsCurrent &&
                        version.Status ==
                            PromptVersionStatus.Published &&
                        EF.Functions
                            .ToTsVector(
                                "russian",
                                version.ReadyText + " " +
                                version.TemplateText)
                            .Matches(
                                EF.Functions
                                    .WebSearchToTsQuery(
                                        "russian",
                                        normalizedSearch)))
                    ? 5d
                    : 0d);

        IQueryable<Prompt> result =
            source
                .Where(matches)
                .OrderByDescending(score)
                .ThenByDescending(prompt =>
                    prompt.PublishedAtUtc)
                .ThenByDescending(prompt =>
                    prompt.Id);

        return Task.FromResult(result);
    }

    private static string EscapeLikePattern(
        string value)
    {
        return value
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "%",
                "\\%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                "\\_",
                StringComparison.Ordinal);
    }
}
