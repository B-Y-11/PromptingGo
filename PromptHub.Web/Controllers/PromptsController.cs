using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Prompts;
using PromptHub.Web.Security;
using PromptHub.Web.Services.Notifications;
using PromptHub.Web.Services.PromptRendering;
using PromptHub.Web.Services.PromptStats;
using PromptHub.Web.Services.PromptTemplates;
using PromptHub.Web.Services.Slugs;
using PromptHub.Web.Services.PromptMedia;
using Microsoft.Extensions.Options;
using PromptHub.Web.Services.MediaStorage;
using PromptHub.Web.Services.AuthorSupport;
using PromptHub.Web.Services.Search;

namespace PromptHub.Web.Controllers;

[Route("prompts")]
public sealed class PromptsController : Controller
{
    // -------------------------------------------------------------------------
    // Dependencies and local records
    // -------------------------------------------------------------------------

    private readonly ApplicationDbContext _dbContext;

    private readonly IPromptTemplateRenderer _renderer;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IPromptTemplateVariableDetector
        _variableDetector;

    private readonly ISlugGenerator _slugGenerator;

    private readonly IVisualPromptTemplateCompiler
        _visualTemplateCompiler;

    private readonly IUserNotificationService _notificationService;

    private readonly IPromptStatsService _promptStatsService;

    private readonly IPromptMetricActorKeyProvider
    _promptMetricActorKeyProvider;

    private readonly IPromptMetricService
        _promptMetricService;

    private readonly IPromptCardCoverService
    _promptCardCoverService;

    private readonly IMediaStorageService
    _mediaStorageService;

    private readonly MediaStorageOptions
        _mediaStorageOptions;

    private readonly IAuthorSupportService
    _authorSupportService;

    private readonly IPromptCatalogSearchService
        _promptCatalogSearchService;

    public PromptsController(
        ApplicationDbContext dbContext,
        IPromptTemplateRenderer renderer,
        UserManager<ApplicationUser> userManager,
        IPromptTemplateVariableDetector variableDetector,
        ISlugGenerator slugGenerator,
        IVisualPromptTemplateCompiler visualTemplateCompiler,
        IUserNotificationService notificationService,
        IPromptStatsService promptStatsService,
        IPromptCardCoverService promptCardCoverService,
        IMediaStorageService mediaStorageService,
        IOptions<MediaStorageOptions> mediaStorageOptions,
        IPromptMetricActorKeyProvider promptMetricActorKeyProvider,
        IPromptMetricService promptMetricService,
        IAuthorSupportService authorSupportService,
        IPromptCatalogSearchService promptCatalogSearchService)
    {
        _dbContext = dbContext;
        _renderer = renderer;
        _userManager = userManager;
        _variableDetector = variableDetector;
        _slugGenerator = slugGenerator;
        _visualTemplateCompiler = visualTemplateCompiler;
        _notificationService = notificationService;
        _promptStatsService = promptStatsService;
        _promptCardCoverService = promptCardCoverService;
        _mediaStorageService = mediaStorageService;
        _mediaStorageOptions = mediaStorageOptions.Value;
        _promptMetricActorKeyProvider =
    promptMetricActorKeyProvider;

        _promptMetricService =
            promptMetricService;
        _authorSupportService = authorSupportService;
        _promptCatalogSearchService = promptCatalogSearchService;
    }

    private sealed record PromptRatingSummary(
        double AverageRating,
        int RatingCount);


    // -------------------------------------------------------------------------
    // Public catalog and prompt lists
    // -------------------------------------------------------------------------

    [HttpGet("", Name = "PromptsCatalog")]
    [HttpGet(
    "category/{category}",
    Name = "PromptsCategory")]
    [HttpGet(
    "tag/{tag}",
    Name = "PromptsTag")]
    [EnableRateLimiting("CatalogSearch")]
    public async Task<IActionResult> Index(
    string? search,
    string? category,
    string? difficulty,
    string? tag,
    string? sort,
    int page = 1,
    CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;

        const int maxSearchLength = 200;

        if (normalizedSearch.Length >
            maxSearchLength)
        {
            normalizedSearch =
                normalizedSearch[..maxSearchLength]
                    .Trim();
        }

        var normalizedCategory = category?.Trim() ?? string.Empty;

        var hasSearch =
            !string.IsNullOrWhiteSpace(
                normalizedSearch);

        var requestedSort =
            sort?
                .Trim()
                .ToLowerInvariant();

        var normalizedSort =
            string.IsNullOrWhiteSpace(
                requestedSort)
                ? hasSearch
                    ? "relevance"
                    : "newest"
                : requestedSort;

        if (normalizedSort is not
    ("relevance" or
     "newest" or
     "popular" or
     "viewed" or
     "rating" or
     "oldest") ||
            (!hasSearch &&
             normalizedSort == "relevance"))
        {
            return NotFound();
        }

        var normalizedDifficulty =
    difficulty?
        .Trim()
        .ToLowerInvariant() ??
    string.Empty;

        var normalizedTag =
            tag?
                .Trim()
                .ToLowerInvariant() ??
            string.Empty;

        const int pageSize =
    24;

        // -------------------------------------------------------------------------
        // EN: Category and tag landing pages have dedicated canonical routes.
        //     Redirect their simple query-string variants to the clean URL.
        //
        // RU: Для категорий и тегов есть отдельные канонические маршруты.
        //     Простые варианты через query string перенаправляем на чистый URL.
        // -------------------------------------------------------------------------

        var categoryComesFromRoute =
            Request.RouteValues.ContainsKey(
                "category");

        var tagComesFromRoute =
            Request.RouteValues.ContainsKey(
                "tag");

        var isBaseCatalogRoute =
            !categoryComesFromRoute &&
            !tagComesFromRoute;

        var hasSearchParameter =
            hasSearch;

        var hasDifficultyParameter =
            !string.IsNullOrWhiteSpace(
                normalizedDifficulty);

        var hasTagParameter =
            !string.IsNullOrWhiteSpace(
                normalizedTag);

        var hasCategoryParameter =
            !string.IsNullOrWhiteSpace(
                normalizedCategory);

        var hasCustomSort =
            normalizedSort !=
                "newest";

        if (isBaseCatalogRoute &&
            hasCategoryParameter &&
            !hasTagParameter &&
            !hasSearchParameter &&
            !hasDifficultyParameter &&
            !hasCustomSort)
        {
            return RedirectToRoutePermanent(
                "PromptsCategory",
                new
                {
                    category =
                        normalizedCategory,

                    page =
                        page > 1
                            ? page
                            : (int?)null
                });
        }

        if (isBaseCatalogRoute &&
            hasTagParameter &&
            !hasCategoryParameter &&
            !hasSearchParameter &&
            !hasDifficultyParameter &&
            !hasCustomSort)
        {
            return RedirectToRoutePermanent(
                "PromptsTag",
                new
                {
                    tag =
                        normalizedTag,

                    page =
                        page > 1
                            ? page
                            : (int?)null
                });
        }

        var normalizedPage =
            Math.Max(
                page,
                1);

        if (!string.IsNullOrWhiteSpace(
        normalizedCategory))
        {
            var categoryExists =
                await _dbContext.Categories
                    .AsNoTracking()
                    .AnyAsync(
                        categoryEntity =>
                            categoryEntity.IsActive &&
                            categoryEntity.Slug ==
                                normalizedCategory,
                        cancellationToken);

            if (!categoryExists)
            {
                return NotFound();
            }
        }

        var query = _dbContext.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Author)
            .Include(prompt => prompt.Category)
            .Include(prompt => prompt.PromptTags)
            .ThenInclude(promptTag => promptTag.Tag)
            .Include(prompt => prompt.Versions)
                .ThenInclude(version =>
                    version.Variables)
                .Where(prompt =>
                    prompt.Status == PromptStatus.Published &&
                    prompt.Category.IsActive);

        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            query = query.Where(prompt =>
                prompt.Category.Slug == normalizedCategory);
        }

        PromptDifficulty? difficultyFilter =
    normalizedDifficulty switch
    {
        "beginner" =>
            PromptDifficulty.Beginner,

        "intermediate" =>
            PromptDifficulty.Intermediate,

        "advanced" =>
            PromptDifficulty.Advanced,

        _ =>
            null
    };

        if (!string.IsNullOrWhiteSpace(
        normalizedDifficulty) &&
    !difficultyFilter.HasValue)
        {
            return NotFound();
        }

        if (difficultyFilter.HasValue)
        {
            query = query.Where(prompt =>
                prompt.Difficulty ==
                    difficultyFilter.Value);
        }

        var selectedTagName =
    string.Empty;

        if (!string.IsNullOrWhiteSpace(
                normalizedTag))
        {
            var selectedTag =
                await _dbContext.Tags
                    .AsNoTracking()
                    .Where(tagEntity =>
                        tagEntity.IsActive &&
                        tagEntity.Slug ==
                            normalizedTag)
                    .Select(tagEntity =>
                        new
                        {
                            tagEntity.Id,
                            tagEntity.Name
                        })
                    .SingleOrDefaultAsync(
                        cancellationToken);

            if (selectedTag is null)
            {
                return NotFound();
            }

            selectedTagName =
                selectedTag.Name;

            query = query.Where(prompt =>
                prompt.PromptTags.Any(
                    promptTag =>
                        promptTag.TagId ==
                            selectedTag.Id));
        }

        // ---------------------------------------------------------------------
        // EN: Apply search after category/difficulty/tag filters so both the
        //     lexical and semantic candidate sets respect the selected filters.
        //
        // RU: Поиск применяем после категории/сложности/тега, чтобы lexical
        //     и semantic-кандидаты учитывали выбранные фильтры одинаково.
        // ---------------------------------------------------------------------

        if (hasSearchParameter)
        {
            query =
                await _promptCatalogSearchService
                    .ApplySearchAsync(
                        query,
                        normalizedSearch,
                        cancellationToken);
        }

        var totalCount =
    await query.CountAsync(
        cancellationToken);

        var totalPages =
            totalCount == 0
                ? 1
                : (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize);

        normalizedPage =
            Math.Min(
                normalizedPage,
                totalPages);

        if (page != normalizedPage)
        {
            var routeValues =
                new RouteValueDictionary
                {
                    ["page"] =
                        normalizedPage > 1
                            ? normalizedPage
                            : null,

                    ["search"] =
                        string.IsNullOrWhiteSpace(
                            normalizedSearch)
                            ? null
                            : normalizedSearch,

                    ["difficulty"] =
                        string.IsNullOrWhiteSpace(
                            normalizedDifficulty)
                            ? null
                            : normalizedDifficulty,

                    ["sort"] =
                        normalizedSort ==
                            (hasSearchParameter
                                ? "relevance"
                                : "newest")
                            ? null
                            : normalizedSort
                };

            string routeName;

            if (Request.RouteValues.ContainsKey(
                    "category"))
            {
                routeName =
                    "PromptsCategory";

                routeValues["category"] =
                    normalizedCategory;

                routeValues["tag"] =
                    string.IsNullOrWhiteSpace(
                        normalizedTag)
                        ? null
                        : normalizedTag;
            }
            else if (Request.RouteValues.ContainsKey(
                         "tag"))
            {
                routeName =
                    "PromptsTag";

                routeValues["tag"] =
                    normalizedTag;

                routeValues["category"] =
                    string.IsNullOrWhiteSpace(
                        normalizedCategory)
                        ? null
                        : normalizedCategory;
            }
            else
            {
                routeName =
                    "PromptsCatalog";

                routeValues["category"] =
                    string.IsNullOrWhiteSpace(
                        normalizedCategory)
                        ? null
                        : normalizedCategory;

                routeValues["tag"] =
                    string.IsNullOrWhiteSpace(
                        normalizedTag)
                        ? null
                        : normalizedTag;
            }

            return RedirectToRoutePermanent(
                routeName,
                routeValues);
        }

        query = normalizedSort switch
        {
            "relevance" when hasSearchParameter =>
                query,

            "popular" => query
                .OrderByDescending(prompt =>
                    prompt.CopyCount)
                .ThenByDescending(prompt =>
                    prompt.ViewCount)
                .ThenByDescending(prompt =>
                    prompt.PublishedAtUtc),

            "viewed" => query
                .OrderByDescending(prompt =>
                    prompt.ViewCount)
                .ThenByDescending(prompt =>
                    prompt.PublishedAtUtc),

            "rating" => query
                .OrderByDescending(prompt =>
                    prompt.Ratings.Any()
                        ? prompt.Ratings.Average(rating =>
                            (double)rating.Score)
                        : 0)
                .ThenByDescending(prompt =>
                    prompt.Ratings.Count)
                .ThenByDescending(prompt =>
                    prompt.PublishedAtUtc),

            "oldest" => query
                .OrderBy(prompt =>
                    prompt.PublishedAtUtc),

            _ => query
                .OrderByDescending(prompt =>
                    prompt.PublishedAtUtc)
        };

        var promptEntities =
    await query
        .Skip(
            (normalizedPage - 1) *
            pageSize)
        .Take(
            pageSize)
        .ToListAsync(
            cancellationToken);

        var currentUserId =
            _userManager.GetUserId(User);

        var favoritePromptIds =
            new HashSet<int>();

        if (!string.IsNullOrWhiteSpace(
                currentUserId) &&
            promptEntities.Count > 0)
        {
            var promptIds = promptEntities
                .Select(prompt => prompt.Id)
                .ToArray();

            favoritePromptIds =
                (await _dbContext.PromptFavorites
                    .AsNoTracking()
                    .Where(favorite =>
                        favorite.UserId ==
                            currentUserId &&
                        promptIds.Contains(
                            favorite.PromptId))
                    .Select(favorite =>
                        favorite.PromptId)
                    .ToListAsync(
                        cancellationToken))
                .ToHashSet();
        }


        var visiblePromptIds =
            promptEntities
            .Select(prompt => prompt.Id)
            .ToArray();


        var promptCovers =
            await _promptCardCoverService
                .GetPublishedCoversAsync(
                    visiblePromptIds,
                    cancellationToken);

        var ratingSummaries =
            await LoadRatingSummariesAsync(
                visiblePromptIds,
                cancellationToken);

        var currentUserRatings =
            await LoadCurrentUserRatingsAsync(
                visiblePromptIds,
                currentUserId,
                cancellationToken);


        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(categoryEntity => categoryEntity.IsActive)
            .OrderBy(categoryEntity => categoryEntity.SortOrder)
            .ThenBy(categoryEntity => categoryEntity.Name)
            .Select(categoryEntity =>
                new PromptCategoryFilterViewModel
                {
                    Name = categoryEntity.Name,
                    Slug = categoryEntity.Slug
                })
            .ToListAsync(cancellationToken);

        var visibleCommentCounts =
            await _promptStatsService.GetVisibleCommentCountsAsync(
                visiblePromptIds,
                cancellationToken);



        var promptItems = promptEntities
            .Select(prompt =>
                new PromptCatalogItemViewModel
                {
                    PromptId = prompt.Id,

                    Title = prompt.Title,
                    Slug = prompt.Slug,
                    Summary = prompt.Summary,
                    CategoryName =
    prompt.Category.Name,

                    CategorySlug =
    prompt.Category.Slug,
                    Cover =
                        promptCovers.GetValueOrDefault(
                            prompt.Id),

                    Tags =
                        CreateTagBadges(
                            prompt,
                            activeOnly: true),

                    AuthorId =
                        prompt.AuthorId,

                    AuthorName =
                        GetAuthorName(prompt.Author),

                    DifficultyName =
    GetDifficultyName(
        prompt.Difficulty),

                    DifficultyValue =
    prompt.Difficulty
        .ToString()
        .ToLowerInvariant(),

                    HasConstructor =
                        prompt.Versions.Any(version =>
                            version.IsCurrent &&
                            !string.IsNullOrWhiteSpace(
                                version.TemplateText) &&
                            version.Variables.Count > 0),

                    IsFavorite =
                        favoritePromptIds.Contains(
                            prompt.Id),

                    AverageRating =
                    ratingSummaries.TryGetValue(
                        prompt.Id,
                        out var ratingSummary)
                        ? ratingSummary.AverageRating
                        : 0,

                    RatingCount =
                    ratingSummaries.TryGetValue(
                        prompt.Id,
                        out var ratingCountSummary)
                        ? ratingCountSummary.RatingCount
                        : 0,

                    CurrentUserRating =
                    currentUserRatings.TryGetValue(
                        prompt.Id,
                        out var currentRating)
                        ? currentRating
                        : null,


                    ViewCount = prompt.ViewCount,
                    CopyCount = prompt.CopyCount,
                    VisibleCommentCount =
                        visibleCommentCounts.GetValueOrDefault(prompt.Id),
                    PublishedAtUtc = prompt.PublishedAtUtc
                })
            .ToArray();

        var model =
    new PromptCatalogViewModel
    {
        Search =
            normalizedSearch,

        Category =
            normalizedCategory,

        Difficulty =
    normalizedDifficulty,

        Tag =
    normalizedTag,

        TagName =
    selectedTagName,

        Sort =
            normalizedSort,

        Page =
            normalizedPage,

        PageSize =
            pageSize,

        TotalCount =
            totalCount,

        Prompts =
            promptItems,

        Categories =
            categories
    };

        return View(model);
    }


    // -------------------------------------------------------------------------
    // EN: Alphabetical public directory of tags that have published prompts.
    // RU: Публичный алфавитный список тегов с опубликованными промптами.
    // -------------------------------------------------------------------------

    [HttpGet("tags", Name = "PromptsTags")]
    public async Task<IActionResult> Tags(
        CancellationToken cancellationToken = default)
    {
        var tags =
            await _dbContext.Tags
                .AsNoTracking()
                .Where(tag =>
                    tag.IsActive)
                .Select(tag =>
                    new AllTagsItemViewModel
                    {
                        Name = tag.Name,
                        Slug = tag.Slug,
                        PromptCount =
                            tag.PromptTags.Count(promptTag =>
                                promptTag.Prompt.Status ==
                                    PromptStatus.Published &&
                                promptTag.Prompt.Category.IsActive)
                    })
                .Where(tag =>
                    tag.PromptCount > 0)
                .ToListAsync(
                    cancellationToken);

        var russianComparer =
            StringComparer.Create(
                System.Globalization.CultureInfo.GetCultureInfo(
                    "ru-RU"),
                ignoreCase: true);

        var orderedTags =
            tags
                .OrderBy(tag =>
                    tag.Name,
                    russianComparer)
                .ToArray();

        var groups =
            orderedTags
                .GroupBy(tag =>
                    GetTagAlphabetGroup(
                        tag.Name))
                .Select(group =>
                    new AllTagsGroupViewModel
                    {
                        Letter = group.Key,
                        Tags = group.ToArray()
                    })
                .ToArray();

        return View(
            new AllTagsViewModel
            {
                TagCount = orderedTags.Length,
                Groups = groups
            });
    }


    [Authorize]
    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites(
    CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var favoriteEntities =
            await _dbContext.PromptFavorites
                .AsNoTracking()
                .Where(favorite =>
                    favorite.UserId == userId &&
                    favorite.Prompt.Status ==
                        PromptStatus.Published &&
                        favorite.Prompt.Category.IsActive)
                .Include(favorite =>
                    favorite.Prompt)
                    .ThenInclude(prompt =>
                        prompt.Author)
                .Include(favorite =>
                    favorite.Prompt)
                    .ThenInclude(prompt =>
                        prompt.Category)
                .Include(favorite =>
                        favorite.Prompt)
                    .ThenInclude(prompt =>
                        prompt.PromptTags)
                        .ThenInclude(promptTag =>
                            promptTag.Tag)
                .Include(favorite =>
                    favorite.Prompt)
                    .ThenInclude(prompt =>
                        prompt.Versions)
                        .ThenInclude(version =>
                            version.Variables)
                .OrderByDescending(favorite =>
                    favorite.CreatedAtUtc)
                .Take(100)
                .ToListAsync(
                    cancellationToken);

        var favoritePromptIds =
            favoriteEntities
            .Select(favorite =>
                favorite.PromptId)
            .ToArray();

        var promptCovers =
            await _promptCardCoverService
                .GetPublishedCoversAsync(
                    favoritePromptIds,
                    cancellationToken);

        var ratingSummaries =
            await LoadRatingSummariesAsync(
                favoritePromptIds,
                cancellationToken);

        var currentUserRatings =
            await LoadCurrentUserRatingsAsync(
                favoritePromptIds,
                userId,
                cancellationToken);

        var visibleCommentCounts =
            await _promptStatsService.GetVisibleCommentCountsAsync(
                favoritePromptIds,
                cancellationToken);

        var promptItems = favoriteEntities
            .Select(favorite =>
            {
                var prompt =
                    favorite.Prompt;

                return new PromptCatalogItemViewModel
                {
                    PromptId = prompt.Id,
                    Title = prompt.Title,
                    Slug = prompt.Slug,
                    Summary = prompt.Summary,

                    CategoryName =
                        prompt.Category.Name,
                    Cover =
                        promptCovers.GetValueOrDefault(
                            prompt.Id),

                    Tags =
                        CreateTagBadges(
                            prompt,
                            activeOnly: true),

                    AuthorId = prompt.AuthorId,

                    AuthorName =
                        GetAuthorName(
                            prompt.Author),

                    DifficultyName =
                        GetDifficultyName(
                            prompt.Difficulty),

                    HasConstructor =
                        prompt.Versions.Any(version =>
                            version.IsCurrent &&
                            !string.IsNullOrWhiteSpace(
                                version.TemplateText) &&
                            version.Variables.Count > 0),

                    IsFavorite = true,

                    AverageRating =
                    ratingSummaries.TryGetValue(
                        prompt.Id,
                        out var ratingSummary)
                        ? ratingSummary.AverageRating
                        : 0,

                    RatingCount =
                    ratingSummaries.TryGetValue(
                        prompt.Id,
                        out var ratingCountSummary)
                        ? ratingCountSummary.RatingCount
                        : 0,

                    CurrentUserRating =
                    currentUserRatings.TryGetValue(
                        prompt.Id,
                        out var currentRating)
                        ? currentRating
                        : null,

                    ViewCount =
                        prompt.ViewCount,

                    CopyCount =
                        prompt.CopyCount,

                    VisibleCommentCount =
                        visibleCommentCounts.GetValueOrDefault(prompt.Id),

                    PublishedAtUtc =
                        prompt.PublishedAtUtc
                };
            })
            .ToArray();

        var model =
            new FavoritePromptsViewModel
            {
                Prompts = promptItems
            };

        return View(model);
    }



    // -------------------------------------------------------------------------
    // Prompt interaction actions
    // -------------------------------------------------------------------------

    [Authorize]
    [HttpPost("{slug}/favorite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(
    string slug,
    CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var promptId =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    prompt.Slug == slug &&
                    prompt.Status ==
                    PromptStatus.Published &&
                    prompt.Category.IsActive)
                .Select(prompt =>
                    (int?)prompt.Id)
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (!promptId.HasValue)
        {
            return NotFound();
        }

        var existingFavorite =
            await _dbContext.PromptFavorites
                .SingleOrDefaultAsync(
                    favorite =>
                        favorite.UserId ==
                            userId &&
                        favorite.PromptId ==
                            promptId.Value,
                    cancellationToken);

        bool isFavorite;

        if (existingFavorite is null)
        {
            _dbContext.PromptFavorites.Add(
                new PromptFavorite
                {
                    UserId = userId,
                    PromptId =
                        promptId.Value,

                    CreatedAtUtc =
                        DateTime.UtcNow
                });

            isFavorite = true;
        }
        else
        {
            _dbContext.PromptFavorites.Remove(
                existingFavorite);

            isFavorite = false;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Json(new
        {
            isFavorite
        });
    }


    [Authorize]
    [HttpPost("{slug}/report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportPrompt(
    string slug,
    [FromForm] PromptReportReason reason,
    [FromForm] string? details,
    CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid ||
            !Enum.IsDefined(reason))
        {
            TempData["ErrorMessage"] =
                "Выберите корректную причину жалобы.";

            return RedirectToAction(
                nameof(Details),
                new { slug });
        }

        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var normalizedDetails =
            details?.Trim()
            ?? string.Empty;

        if (normalizedDetails.Length > 1000)
        {
            TempData["ErrorMessage"] =
                "Описание жалобы не должно превышать 1000 символов.";

            return RedirectToAction(
                nameof(Details),
                new { slug });
        }

        var promptInfo =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
    prompt.Slug == slug &&
    prompt.Status ==
        PromptStatus.Published &&
    prompt.Category.IsActive)
                .Select(prompt => new
                {
                    prompt.Id,
                    prompt.AuthorId
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (promptInfo is null)
        {
            return NotFound();
        }

        if (promptInfo.AuthorId == userId)
        {
            TempData["ErrorMessage"] =
                "Нельзя пожаловаться на собственный промпт.";

            return RedirectToAction(
                nameof(Details),
                new { slug });
        }

        var existingOpenReport =
            await _dbContext.PromptReports
                .SingleOrDefaultAsync(
                    report =>
                        report.PromptId == promptInfo.Id &&
                        report.ReporterId == userId &&
                        report.Status == PromptReportStatus.New,
                    cancellationToken);

        if (existingOpenReport is null)
        {
            _dbContext.PromptReports.Add(
                new PromptReport
                {
                    PromptId = promptInfo.Id,
                    ReporterId = userId,
                    Reason = reason,
                    Details = normalizedDetails,
                    Status = PromptReportStatus.New,
                    CreatedAtUtc = DateTime.UtcNow
                });

            TempData["SuccessMessage"] =
                "Жалоба отправлена модераторам.";
        }
        else
        {
            existingOpenReport.Reason = reason;
            existingOpenReport.Details = normalizedDetails;
            existingOpenReport.CreatedAtUtc = DateTime.UtcNow;

            TempData["SuccessMessage"] =
                "Ваша открытая жалоба обновлена.";
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return RedirectToAction(
            nameof(Details),
            new { slug });
    }


    [Authorize]
    [HttpPost("{slug}/rating")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRating(
    string slug,
    [FromForm] int score,
    CancellationToken cancellationToken)
    {
        if (score is < 1 or > 5)
        {
            return BadRequest(
                "Оценка должна быть от 1 до 5.");
        }

        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var promptId =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
    prompt.Slug == slug &&
    prompt.Status ==
        PromptStatus.Published &&
    prompt.Category.IsActive)
                .Select(prompt =>
                    (int?)prompt.Id)
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (!promptId.HasValue)
        {
            return NotFound();
        }

        var now =
            DateTime.UtcNow;

        var existingRating =
            await _dbContext.PromptRatings
                .SingleOrDefaultAsync(
                    rating =>
                        rating.UserId == userId &&
                        rating.PromptId == promptId.Value,
                    cancellationToken);

        if (existingRating is null)
        {
            _dbContext.PromptRatings.Add(
                new PromptRating
                {
                    UserId = userId,
                    PromptId = promptId.Value,
                    Score = score,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
        }
        else
        {
            existingRating.Score = score;
            existingRating.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await CreateRatingJsonResponseAsync(
            promptId.Value,
            userId,
            cancellationToken);
    }

    [Authorize]
    [HttpPost("{slug}/rating/clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearRating(
        string slug,
        CancellationToken cancellationToken)
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var promptId =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
    prompt.Slug == slug &&
    prompt.Status ==
        PromptStatus.Published &&
    prompt.Category.IsActive)
                .Select(prompt =>
                    (int?)prompt.Id)
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (!promptId.HasValue)
        {
            return NotFound();
        }

        var existingRating =
            await _dbContext.PromptRatings
                .SingleOrDefaultAsync(
                    rating =>
                        rating.UserId == userId &&
                        rating.PromptId == promptId.Value,
                    cancellationToken);

        if (existingRating is not null)
        {
            _dbContext.PromptRatings.Remove(
                existingRating);

            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }

        return await CreateRatingJsonResponseAsync(
            promptId.Value,
            userId,
            cancellationToken);
    }

    private async Task<IActionResult> CreateRatingJsonResponseAsync(
        int promptId,
        string userId,
        CancellationToken cancellationToken)
    {
        var summary =
            await GetPromptRatingSummaryAsync(
                promptId,
                cancellationToken);

        var currentUserRating =
            await _dbContext.PromptRatings
                .AsNoTracking()
                .Where(rating =>
                    rating.UserId == userId &&
                    rating.PromptId == promptId)
                .Select(rating =>
                    (int?)rating.Score)
                .SingleOrDefaultAsync(
                    cancellationToken);

        return Json(new
        {
            averageRating =
                summary.AverageRating,

            ratingCount =
                summary.RatingCount,

            currentUserRating
        });
    }


    // -------------------------------------------------------------------------
    // Author workspace
    // -------------------------------------------------------------------------

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> My(
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var promptEntities =
            await _dbContext.Prompts
                .AsNoTracking()

                .Include(prompt =>
                    prompt.Category)

                .Include(prompt =>
                    prompt.PromptTags)
                    .ThenInclude(promptTag =>
                        promptTag.Tag)

                .Include(prompt =>
                    prompt.Versions)
                    .ThenInclude(version =>
                        version.Variables)

                .Where(prompt =>
                    prompt.AuthorId ==
                        authorId)

                .OrderByDescending(prompt =>
                    prompt.UpdatedAtUtc ??
                    prompt.CreatedAtUtc)

                .AsSplitQuery()

                .ToListAsync(
                    cancellationToken);

        var promptIds =
            promptEntities
                .Select(prompt =>
                    prompt.Id)
                .ToArray();

        var visibleCommentCounts =
            await _promptStatsService
                .GetVisibleCommentCountsAsync(
                    promptIds,
                    cancellationToken);

        var promptItems =
            promptEntities
                .Select(prompt =>
                {
                    // -------------------------------------------------------------
                    // EN: A published prompt may have a separate hidden
                    //     working version.
                    //
                    // RU: У опубликованного промпта может существовать
                    //     отдельная скрытая рабочая версия.
                    // -------------------------------------------------------------

                    var workingVersion =
                        GetWorkingVersion(
                            prompt.Versions);

                    var publishedVersion =
                        GetPublishedVersion(
                            prompt.Versions);

                    var displayVersion =
                        workingVersion ??
                        publishedVersion ??
                        prompt.Versions
                            .SingleOrDefault(version =>
                                version.IsCurrent);

                    return new MyPromptItemViewModel
                    {
                        Id =
                            prompt.Id,

                        Title =
                            prompt.Title,

                        Slug =
                            prompt.Slug,

                        Summary =
                            prompt.Summary,

                        CategoryName =
                            prompt.Category.Name,

                        DifficultyName =
                            GetDifficultyName(
                                prompt.Difficulty),

                        Tags =
                            CreateTagBadges(
                                prompt,
                                activeOnly:
                                    false),

                        Status =
                            prompt.Status,

                        WorkingVersionStatus =
                            workingVersion?.Status,

                        WorkingModerationComment =
                            workingVersion
                                ?.ModerationComment
                            ?? string.Empty,

                        StatusName =
                            GetPromptStatusName(
                                prompt.Status),

                        StatusBadgeClass =
                            GetPromptStatusBadgeClass(
                                prompt.Status),

                        HasConstructor =
                            displayVersion is not null &&
                            !string.IsNullOrWhiteSpace(
                                displayVersion.TemplateText) &&
                            displayVersion.Variables.Count >
                                0,

                        ViewCount =
                            prompt.ViewCount,

                        CopyCount =
                            prompt.CopyCount,

                        VisibleCommentCount =
                            visibleCommentCounts
                                .GetValueOrDefault(
                                    prompt.Id),

                        CreatedAtUtc =
                            prompt.CreatedAtUtc,

                        UpdatedAtUtc =
                            prompt.UpdatedAtUtc,

                        PublishedAtUtc =
                            prompt.PublishedAtUtc,

                        ModerationComment =
                            prompt.ModerationComment
                            ?? string.Empty,

                        ArchivedAtUtc =
                            prompt.ArchivedAtUtc,

                        ArchiveReason =
                            prompt.ArchiveReason
                            ?? string.Empty
                    };
                })
                .ToArray();

        var model =
            new MyPromptsViewModel
            {
                Prompts =
                    promptItems
            };

        return View(
            model);
    }


    [Authorize]
    [HttpPost("{id:int}/unpublish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts

                .Include(promptEntity =>
                    promptEntity.Versions)

                .AsSplitQuery()

                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        if (prompt.Status !=
            PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "Снять с публикации можно только опубликованный промпт.";

            return RedirectToAction(
                nameof(My));
        }

        // -------------------------------------------------------------------------
        // EN: Do not mix unpublishing with an existing working-version workflow.
        //
        // RU: Не смешиваем снятие публикации с уже существующей рабочей версией.
        //     Сначала автор должен закончить либо отменить её.
        // -------------------------------------------------------------------------

        var workingVersion =
            GetWorkingVersion(
                prompt.Versions);

        if (workingVersion is not null)
        {
            TempData["ErrorMessage"] =
                workingVersion.Status ==
                    PromptVersionStatus.PendingModeration
                    ? "Изменения уже находятся на модерации. Снять публикацию сейчас нельзя."
                    : "Сначала отмените неопубликованные изменения, затем снимите промпт с публикации.";

            return RedirectToAction(
                nameof(My));
        }

        var publishedVersion =
            GetPublishedVersion(
                prompt.Versions);

        if (publishedVersion is null)
        {
            TempData["ErrorMessage"] =
                "Не найдена текущая опубликованная версия промпта.";

            return RedirectToAction(
                nameof(My));
        }

        var nowUtc =
            DateTime.UtcNow;

        // -------------------------------------------------------------------------
        // EN: Unpublishing returns the current published version to Draft.
        //     It can then be edited and submitted for moderation again.
        //
        // RU: При снятии с публикации текущая опубликованная версия становится
        //     Draft. После этого её можно редактировать и повторно отправить
        //     на модерацию.
        // -------------------------------------------------------------------------

        publishedVersion.Status =
            PromptVersionStatus.Draft;

        publishedVersion.SubmittedAtUtc =
            null;

        publishedVersion.ModeratedAtUtc =
            null;

        publishedVersion.ModeratedById =
            null;

        publishedVersion.ModerationComment =
            null;

        publishedVersion.UpdatedAtUtc =
            nowUtc;

        // IsCurrent оставляем true.
        //
        // Для неопубликованного промпта это всё ещё его основная редактируемая
        // версия. После повторного Approve она снова станет Published.

        prompt.Status =
            PromptStatus.Draft;

        prompt.SubmittedAtUtc =
            null;

        prompt.ModeratedAtUtc =
            null;

        prompt.ModeratedById =
            null;

        prompt.ModerationComment =
            null;

        prompt.UpdatedAtUtc =
            nowUtc;

        // -------------------------------------------------------------------------
        // EN: Resolve open reports because the author removed the content
        //     from public access.
        //
        // RU: Закрываем открытые жалобы, потому что автор самостоятельно
        //     убрал материал из публичной части.
        // -------------------------------------------------------------------------

        await _dbContext.PromptReports
            .Where(report =>
                report.PromptId ==
                    prompt.Id &&
                report.Status ==
                    PromptReportStatus.New)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(
                            report =>
                                report.Status,
                            PromptReportStatus.Resolved)

                        .SetProperty(
                            report =>
                                report.ResolvedAtUtc,
                            nowUtc)

                        .SetProperty(
                            report =>
                                report.ResolutionComment,
                            "Автор самостоятельно снял промпт с публикации."),
                cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Промпт снят с публикации и возвращён в черновики.";

        return RedirectToAction(
            nameof(My));
    }



    [Authorize]
    [HttpPost("{id:int}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForModeration(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .Include(promptEntity =>
                    promptEntity.Versions)

                .Include(promptEntity =>
                    promptEntity.MediaItems)

                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: Submission is allowed only for a normal draft or for a published
        //     prompt that has a hidden working version.
        //
        // RU: На модерацию можно отправить обычный черновик или скрытую рабочую
        //     версию уже опубликованного промпта.
        // -------------------------------------------------------------------------

        if (prompt.Status is not
            PromptStatus.Draft and not
            PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "Этот промпт сейчас нельзя отправить на модерацию.";

            return RedirectToAction(
                nameof(My));
        }

        var workingVersion =
            GetWorkingVersion(
                prompt.Versions);

        if (workingVersion is null)
        {
            TempData["ErrorMessage"] =
                "Не найдена версия промпта для отправки на модерацию.";

            return RedirectToAction(
                nameof(My));
        }

        if (workingVersion.Status !=
            PromptVersionStatus.Draft)
        {
            TempData["ErrorMessage"] =
                workingVersion.Status ==
                    PromptVersionStatus.PendingModeration
                    ? "Изменения уже находятся на модерации."
                    : "Эту версию сейчас нельзя отправить на модерацию.";

            return RedirectToAction(
                nameof(My));
        }

        if (string.IsNullOrWhiteSpace(
                workingVersion.ReadyText))
        {
            TempData["ErrorMessage"] =
                "Перед отправкой добавьте готовый текст промпта.";

            return RedirectToAction(
                nameof(My));
        }

        var publishedVersion =
            GetPublishedVersion(
                prompt.Versions);

        var isPublishedUpdate =
            publishedVersion is not null;

        // -------------------------------------------------------------------------
        // EN: Protect against inconsistent lifecycle state.
        // RU: Защита от неконсистентного состояния жизненного цикла.
        // -------------------------------------------------------------------------

        if (isPublishedUpdate &&
            prompt.Status !=
                PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "Обнаружено некорректное состояние опубликованного промпта.";

            return RedirectToAction(
                nameof(My));
        }

        if (!isPublishedUpdate &&
            prompt.Status !=
                PromptStatus.Draft)
        {
            TempData["ErrorMessage"] =
                "Обнаружено некорректное состояние черновика.";

            return RedirectToAction(
                nameof(My));
        }

        var submittedAtUtc =
            DateTime.UtcNow;


        workingVersion.Status =
            PromptVersionStatus.PendingModeration;

        workingVersion.SubmittedAtUtc =
            submittedAtUtc;

        workingVersion.UpdatedAtUtc =
            submittedAtUtc;

        workingVersion.ModerationComment =
            null;

        workingVersion.ModeratedAtUtc =
            null;

        workingVersion.ModeratedById =
            null;

        // -------------------------------------------------------------------------
        // EN: Before the first publication Prompt still owns the moderation state.
        //
        //     For an update of an already published prompt we DO NOT change
        //     Prompt.Status. The old published version must remain available.
        //
        // RU: До первой публикации состояние модерации хранится также в Prompt.
        //
        //     При изменении уже опубликованного промпта Prompt.Status НЕ меняем:
        //     старая опубликованная версия продолжает работать на сайте.
        // -------------------------------------------------------------------------

        if (!isPublishedUpdate)
        {
            prompt.Status =
                PromptStatus.PendingModeration;

            prompt.SubmittedAtUtc =
                submittedAtUtc;

            prompt.UpdatedAtUtc =
                submittedAtUtc;

            prompt.ModerationComment =
                null;

            prompt.ModeratedAtUtc =
                null;

            prompt.ModeratedById =
                null;
        }

        // -------------------------------------------------------------------------
        // EN: Only draft media belonging to the submitted working version joins
        //     this moderation request.
        //
        //     Media of the currently published version remains untouched.
        //
        // RU: Вместе с версией отправляем только её новые Draft-медиа.
        //
        //     Медиа действующей опубликованной версии не трогаем.
        // -------------------------------------------------------------------------

        foreach (var media in
                 prompt.MediaItems.Where(media =>
                     media.PromptVersionId ==
                         workingVersion.Id &&
                     media.Status ==
                         PromptMediaStatus.Draft))
        {
            media.Status =
                PromptMediaStatus.PendingModeration;

            media.SubmittedAtUtc =
                submittedAtUtc;

            media.ModeratedAtUtc =
                null;

            media.ModeratedById =
                null;

            media.ModerationComment =
                null;

            media.UpdatedAtUtc =
                submittedAtUtc;
        }

        await _notificationService.AddAsync(
            prompt.AuthorId,
            UserNotificationType.PromptSubmitted,

            isPublishedUpdate
                ? "Изменения отправлены на модерацию"
                : "Промпт отправлен на модерацию",

            isPublishedUpdate
                ? $"Изменения промпта «{workingVersion.Title}» отправлены на проверку. Текущая опубликованная версия останется доступной до решения модератора."
                : $"Ваш промпт «{workingVersion.Title}» отправлен на проверку. Мы сообщим о решении модератора.",

            "/prompts/my",
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            isPublishedUpdate
                ? "Изменения отправлены на модерацию. Текущая публикация остаётся доступной."
                : "Промпт отправлен на модерацию.";

        return RedirectToAction(
            nameof(My));
    }


    // -------------------------------------------------------------------------
    // Prompt creation and editing
    // -------------------------------------------------------------------------

    [Authorize]
    [HttpGet("create")]
    public async Task<IActionResult> Create(
    CancellationToken cancellationToken)
    {
        var model =
    new CreatePromptViewModel
    {
        Categories =
            await LoadCategoryOptionsAsync(
                cancellationToken),

        TagOptions =
            await LoadTagOptionsAsync(
                Array.Empty<int>(),
                cancellationToken),

        MaxImagesPerPrompt =
            _mediaStorageOptions
                .MaxImagesPerPrompt,

        MaxImageSizeBytes =
            _mediaStorageOptions
                .MaxImageSizeBytes
    };

        return View(model);
    }

    [Authorize]
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(
    MultipartBodyLengthLimit =
        35 * 1024 * 1024)]
    public async Task<IActionResult> Create(
    CreatePromptViewModel model,
    CancellationToken cancellationToken)
    {
        model.Categories =
            await LoadCategoryOptionsAsync(
                cancellationToken);


        model.MaxImagesPerPrompt =
            _mediaStorageOptions
                .MaxImagesPerPrompt;

        model.MaxImageSizeBytes =
            _mediaStorageOptions
                .MaxImageSizeBytes;

        var submittedImages =
            model.Images?
                .Where(image =>
                    image is not null &&
                    image.Length > 0)
                .ToList()
            ?? [];

        model.Images =
            submittedImages;

        if (submittedImages.Count >
            _mediaStorageOptions.MaxImagesPerPrompt)
        {
            ModelState.AddModelError(
                nameof(model.Images),
                $"Можно загрузить не более " +
                $"{_mediaStorageOptions.MaxImagesPerPrompt} изображений.");
        }

        var oversizedImage =
            submittedImages.FirstOrDefault(image =>
                image.Length >
                _mediaStorageOptions.MaxImageSizeBytes);

        if (oversizedImage is not null)
        {
            var maxSizeMegabytes =
                _mediaStorageOptions
                    .MaxImageSizeBytes /
                1024d /
                1024d;

            ModelState.AddModelError(
                nameof(model.Images),
                $"Файл «{oversizedImage.FileName}» превышает " +
                $"допустимый размер {maxSizeMegabytes:0.#} МБ.");
        }

        var categoryExists =
            await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(
                    category =>
                        category.Id == model.CategoryId &&
                        category.IsActive,
                    cancellationToken);

        if (!categoryExists)
        {
            ModelState.AddModelError(
                nameof(model.CategoryId),
                "Выбранная категория не существует.");
        }


        var selectedTagIds =
            NormalizeSelectedTagIds(
        model.SelectedTagIds);

        model.SelectedTagIds =
            selectedTagIds;

        await ValidateSelectedTagsAsync(
            selectedTagIds,
            cancellationToken);



        VisualPromptTemplateCompileResult?
            compileResult = null;

        if (model.EnableConstructor)
        {
            List<VisualPromptFieldDefinition> fields;

            try
            {
                fields =
                    JsonSerializer.Deserialize<
                        List<VisualPromptFieldDefinition>>(
                        model.ConstructorFieldsJson
                            ?? "[]",
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        })
                    ?? new List<
                        VisualPromptFieldDefinition>();
            }
            catch (JsonException)
            {
                fields =
                    new List<
                        VisualPromptFieldDefinition>();

                ModelState.AddModelError(
                    nameof(model.TemplateText),
                    "Не удалось прочитать настройки заполняемых полей.");
            }

            if (ModelState.IsValid)
            {
                compileResult =
                    _visualTemplateCompiler.Compile(
                        model.TemplateText
                            ?? string.Empty,
                        fields);

                foreach (var error in
                         compileResult.Errors)
                {
                    ModelState.AddModelError(
                        nameof(model.TemplateText),
                        error);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            model.TagOptions =
                await LoadTagOptionsAsync(
                    selectedTagIds,
                    cancellationToken);
            return View(model);
        }

        var authorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(authorId))
        {
            return Challenge();
        }

        var baseSlug =
            _slugGenerator.Generate(
                model.Title);

        var uniqueSlug =
            await CreateUniqueSlugAsync(
                baseSlug,
                cancellationToken);

        var compiledVariables =
            compileResult?.Variables
            ?? Array.Empty<
                CompiledPromptVariable>();

        var promptVersion = new PromptVersion
        {
            VersionNumber = 1,

            Status =
    PromptVersionStatus.Draft,

            Title =
    model.Title.Trim(),

            Summary =
    model.Summary.Trim(),

            LanguageCode =
    "ru",

            Difficulty =
    model.Difficulty,

            CategoryId =
    model.CategoryId,

            ReadyText =
                (model.ReadyText
                 ?? string.Empty).Trim(),

            TemplateText =
                compileResult?.TemplateText
                ?? string.Empty,

            UsageInstructions =
                (model.UsageInstructions
                 ?? string.Empty).Trim(),

            ExampleResult =
                (model.ExampleResult
                 ?? string.Empty).Trim(),

            ChangeSummary = "Первая версия",
            IsCurrent = true,

            Variables = compiledVariables
                .Select(variable =>
                    new PromptVariable
                    {
                        Key = variable.Key,
                        Label = variable.Label,

                        VariableType =
                            variable.VariableType,

                        Placeholder =
                            variable.Placeholder,

                        HelpText =
                            variable.HelpText,

                        IsRequired =
                            variable.IsRequired,

                        DefaultValue =
                            variable.DefaultValue,

                        OptionsJson =
                            variable.OptionsJson,

                        SortOrder =
                            variable.SortOrder
                    })
                .ToList()
        };

        var prompt = new Prompt
        {
            Title =
                model.Title.Trim(),

            Slug = uniqueSlug,

            Summary =
                model.Summary.Trim(),

            LanguageCode = "ru",
            Status = PromptStatus.Draft,
            Difficulty = model.Difficulty,
            AuthorId = authorId,
            CategoryId = model.CategoryId,

            Versions = new List<PromptVersion>
        {
            promptVersion
        }
        };

        SyncPromptTags(
    prompt,
    selectedTagIds);

        SyncPromptVersionTags(
    promptVersion,
    selectedTagIds);

        // -------------------------------------------------------------------------
        // EN: Create the prompt and its initial images as one database operation.
        // RU: Создаём промпт и его первоначальные изображения как одну операцию БД.
        // -------------------------------------------------------------------------

        var storedFiles =
            new List<StoredMediaFile>();

        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            _dbContext.Prompts.Add(
                prompt);

            // ---------------------------------------------------------------------
            // EN: The first save assigns database identifiers to the prompt and
            //     its initial version.
            // RU: Первое сохранение назначает идентификаторы промпту и его
            //     первоначальной версии.
            // ---------------------------------------------------------------------

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            var createdAtUtc =
                DateTime.UtcNow;

            for (var index = 0;
                 index < submittedImages.Count;
                 index++)
            {
                var storedFile =
                    await _mediaStorageService
                        .SavePromptImageAsync(
                            submittedImages[index],
                            cancellationToken);

                storedFiles.Add(
                    storedFile);

                var media =
                    new PromptMedia
                    {
                        PromptId =
                            prompt.Id,

                        PromptVersionId =
                            promptVersion.Id,

                        UploadedById =
                            authorId,

                        MediaType =
                            storedFile.MediaType,

                        Status =
                            PromptMediaStatus.Draft,

                        StorageProvider =
                            storedFile.StorageProvider,

                        OriginalFileName =
                            storedFile.OriginalFileName,

                        ContentType =
                            storedFile.ContentType,

                        FileSizeBytes =
                            storedFile.FileSizeBytes,

                        StorageKey =
                            storedFile.StorageKey,

                        PublicUrl =
                            storedFile.PublicUrl,

                        OriginalWidth =
                            storedFile.OriginalWidth,

                        OriginalHeight =
                            storedFile.OriginalHeight,

                        ThumbnailStorageKey =
                            storedFile.ThumbnailStorageKey,

                        ThumbnailContentType =
                            storedFile.ThumbnailContentType,

                        ThumbnailFileSizeBytes =
                            storedFile.ThumbnailFileSizeBytes,

                        ThumbnailWidth =
                            storedFile.ThumbnailWidth,

                        ThumbnailHeight =
                            storedFile.ThumbnailHeight,

                        ThumbnailUrl =
                            storedFile.ThumbnailUrl,

                        Caption =
                            string.Empty,

                        AltText =
                            string.Empty,

                        SortOrder =
                            index,

                        IsCover =
                            index == 0,

                        CreatedAtUtc =
                            createdAtUtc
                    };

                _dbContext.PromptMediaItems.Add(
                    media);
            }

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch (MediaStorageException exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            await DeleteStoredPromptImagesQuietlyAsync(
                storedFiles);

            _dbContext.ChangeTracker.Clear();

            ModelState.AddModelError(
                nameof(model.Images),
                exception.UserMessage);

            model.TagOptions =
                await LoadTagOptionsAsync(
                    selectedTagIds,
                    cancellationToken);

            return View(
                model);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            await DeleteStoredPromptImagesQuietlyAsync(
                storedFiles);

            _dbContext.ChangeTracker.Clear();

            throw;
        }

        return RedirectToAction(
            nameof(DraftCreated),
            new
            {
                id =
                    prompt.Id
            });
    }

    [Authorize]
    [HttpGet("{id:int}/created")]
    public async Task<IActionResult> DraftCreated(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(authorId))
        {
            return Challenge();
        }

        var prompt = await _dbContext.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Versions)
                .ThenInclude(version =>
                    version.Variables)
            .SingleOrDefaultAsync(
                prompt =>
                    prompt.Id == id &&
                    prompt.AuthorId == authorId,
                cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        var currentVersion = prompt.Versions
            .Single(version => version.IsCurrent);

        var model =
            new PromptDraftCreatedViewModel
            {
                PromptId = prompt.Id,
                Title = prompt.Title,
                Slug = prompt.Slug,

                Variables = currentVersion.Variables
                    .OrderBy(variable =>
                        variable.SortOrder)
                    .Select(variable =>
                        new CreatedPromptVariableViewModel
                        {
                            Key = variable.Key,
                            Label = variable.Label,
                            IsRequired =
                                variable.IsRequired
                        })
                    .ToArray()
            };

        return View(model);
    }


    [Authorize]
    [HttpPost("{id:int}/discard-working-copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscardWorkingCopy(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts

                .Include(promptEntity =>
                    promptEntity.Versions)

                .Include(promptEntity =>
                    promptEntity.MediaItems)

                .AsSplitQuery()

                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: A working copy exists only alongside an already published prompt.
        //
        // RU: Рабочую копию отменяем только у уже опубликованного промпта.
        // -------------------------------------------------------------------------

        if (prompt.Status !=
            PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "У этого промпта нет отдельной рабочей копии.";

            return RedirectToAction(
                nameof(My));
        }

        var anyWorkingVersion =
            GetWorkingVersion(
                prompt.Versions);

        if (anyWorkingVersion is null)
        {
            TempData["ErrorMessage"] =
                "Неопубликованные изменения не найдены.";

            return RedirectToAction(
                nameof(My));
        }

        // -------------------------------------------------------------------------
        // EN: Do not allow withdrawal while a moderator may already be reviewing
        //     the version.
        //
        // RU: Версию, которая уже находится у модератора, не отзываем.
        // -------------------------------------------------------------------------

        if (anyWorkingVersion.Status ==
            PromptVersionStatus.PendingModeration)
        {
            TempData["ErrorMessage"] =
                "Изменения уже находятся на модерации. Отменить их сейчас нельзя.";

            return RedirectToAction(
                nameof(My));
        }

        var workingVersion =
            GetEditableVersion(
                prompt.Versions);

        if (workingVersion is null)
        {
            TempData["ErrorMessage"] =
                "Рабочую версию нельзя отменить в её текущем состоянии.";

            return RedirectToAction(
                nameof(My));
        }

        // -------------------------------------------------------------------------
        // EN: Published media must never disappear because a working version is
        //     discarded. Such a state would indicate inconsistent data.
        //
        // RU: Опубликованное медиа нельзя удалить вместе с рабочей версией.
        //     Если такое состояние появилось, лучше остановить операцию.
        // -------------------------------------------------------------------------

        var workingMedia =
            prompt.MediaItems
                .Where(media =>
                    media.PromptVersionId ==
                        workingVersion.Id)
                .ToArray();

        if (workingMedia.Any(media =>
                media.Status ==
                    PromptMediaStatus.Published))
        {
            TempData["ErrorMessage"] =
                "Рабочая версия содержит опубликованное медиа. Отмена изменений заблокирована.";

            return RedirectToAction(
                nameof(My));
        }

        var nowUtc =
            DateTime.UtcNow;

        // -------------------------------------------------------------------------
        // EN: Do not physically delete uploaded files here.
        //     Archive them and let the existing cleanup service remove storage
        //     files according to the retention policy.
        //
        // RU: Файлы физически здесь не удаляем.
        //     Архивируем записи, а существующий cleanup-сервис позже очистит
        //     хранилище согласно retention-политике.
        // -------------------------------------------------------------------------

        foreach (var media in
                 workingMedia)
        {
            if (media.Status ==
                PromptMediaStatus.Archived)
            {
                continue;
            }

            media.Status =
                PromptMediaStatus.Archived;

            media.IsCover =
                false;

            media.ArchivedAtUtc =
                nowUtc;

            media.ArchivedById =
                authorId;

            media.ArchiveReason =
                "Отменено вместе с неопубликованными изменениями промпта.";

            media.PendingCaption =
                null;

            media.PendingAltText =
                null;

            media.MetadataSubmittedAtUtc =
                null;

            media.MetadataModeratedAtUtc =
                null;

            media.MetadataModeratedById =
                null;

            media.MetadataModerationComment =
                null;

            media.UpdatedAtUtc =
                nowUtc;
        }

        // -------------------------------------------------------------------------
        // EN: Variables and version tags are deleted by cascade.
        //     Media uses SetNull, therefore it was archived above first.
        //
        // RU: Переменные и теги версии удалятся каскадно.
        //     У медиа связь настроена через SetNull, поэтому сначала мы его
        //     безопасно архивировали.
        // -------------------------------------------------------------------------

        _dbContext.PromptVersions.Remove(
            workingVersion);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Неопубликованные изменения отменены.";

        return RedirectToAction(
            nameof(My));
    }



    [Authorize]
    [HttpPost("{id:int}/start-edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartEdit(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.Variables)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.VersionTags)

                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: Unpublished prompts already have an editable version.
        // RU: У неопубликованного промпта уже есть редактируемая версия.
        // -------------------------------------------------------------------------

        if (prompt.Status is
            PromptStatus.Draft or
            PromptStatus.Rejected)
        {
            return RedirectToAction(
                nameof(Edit),
                new
                {
                    id =
                        prompt.Id
                });
        }

        if (prompt.Status !=
            PromptStatus.Published)
        {
            TempData["ErrorMessage"] =
                "Этот промпт сейчас нельзя редактировать.";

            return RedirectToAction(
                nameof(My));
        }

        // -------------------------------------------------------------------------
        // EN: A working version may already exist.
        // RU: Рабочая версия могла быть создана раньше.
        // -------------------------------------------------------------------------

        var workingVersion =
            GetWorkingVersion(
                prompt.Versions);

        if (workingVersion is not null)
        {
            if (workingVersion.Status is
                PromptVersionStatus.Draft or
                PromptVersionStatus.Rejected)
            {
                return RedirectToAction(
                    nameof(Edit),
                    new
                    {
                        id =
                            prompt.Id
                    });
            }

            if (workingVersion.Status ==
                PromptVersionStatus.PendingModeration)
            {
                TempData["ErrorMessage"] =
                    "Изменения этого промпта уже находятся на модерации.";

                return RedirectToAction(
                    nameof(My));
            }
        }

        var publishedVersion =
            GetPublishedVersion(
                prompt.Versions);

        if (publishedVersion is null)
        {
            TempData["ErrorMessage"] =
                "У опубликованного промпта не найдена публичная версия.";

            return RedirectToAction(
                nameof(My));
        }

        var nextVersionNumber =
            prompt.Versions.Count == 0
                ? 1
                : prompt.Versions.Max(
                      version =>
                          version.VersionNumber) + 1;

        var nowUtc =
            DateTime.UtcNow;

        var newVersion =
            CreateWorkingVersion(
                publishedVersion,
                nextVersionNumber,
                nowUtc);

        prompt.Versions.Add(
            newVersion);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Создана рабочая копия. Публичная версия останется без изменений до одобрения модератором.";

        return RedirectToAction(
            nameof(Edit),
            new
            {
                id =
                    prompt.Id
            });
    }



    [Authorize]
    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(authorId))
        {
            return Challenge();
        }

        var prompt = await _dbContext.Prompts
            .AsNoTracking()
            .Include(promptEntity =>
                promptEntity.Versions)
                .ThenInclude(version =>
                    version.Variables)
            .Include(promptEntity =>
    promptEntity.Versions)
    .ThenInclude(version =>
        version.VersionTags)
            .SingleOrDefaultAsync(
                promptEntity =>
                    promptEntity.Id == id &&
                    promptEntity.AuthorId == authorId,
                cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }


        var currentVersion =
    GetEditableVersion(
        prompt.Versions);

        if (currentVersion is null)
        {
            TempData["ErrorMessage"] =
                "У промпта сейчас нет версии, доступной для редактирования.";

            return RedirectToAction(
                nameof(My));
        }

        if (currentVersion is null)
        {
            TempData["ErrorMessage"] =
                "У промпта отсутствует текущая версия.";

            return RedirectToAction(
                nameof(My));
        }

        var hasConstructor =
            !string.IsNullOrWhiteSpace(
                currentVersion.TemplateText) &&
            currentVersion.Variables.Count > 0;

        var selectedTagIds =
    currentVersion.VersionTags
        .Select(versionTag =>
            versionTag.TagId)
        .ToArray();

        var model = new EditPromptViewModel
        {
            Id =
        prompt.Id,

            Title =
        currentVersion.Title,

            Summary =
        currentVersion.Summary,

            CategoryId =
        currentVersion.CategoryId,

            Difficulty =
        currentVersion.Difficulty,

            ReadyText =
                currentVersion.ReadyText,

            EnableConstructor =
                hasConstructor,

            TemplateText = hasConstructor
                ? CreateDisplayTemplate(
                    currentVersion.TemplateText,
                    currentVersion.Variables)
                : string.Empty,

            ConstructorFieldsJson =
                hasConstructor
                    ? CreateVisualFieldsJson(
                        currentVersion.Variables)
                    : "[]",

            UsageInstructions =
                currentVersion.UsageInstructions,

            ExampleResult =
                currentVersion.ExampleResult,

            Categories =
                await LoadCategoryOptionsAsync(
                    cancellationToken),

            SelectedTagIds =
                selectedTagIds,

            TagOptions =
                await LoadTagOptionsAsync(
                    selectedTagIds,
                    cancellationToken)
        };

        return View(model);
    }

    [Authorize]
    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
    int id,
    EditPromptViewModel model,
    CancellationToken cancellationToken)
    {
        model.Id =
            id;

        model.Categories =
            await LoadCategoryOptionsAsync(
                cancellationToken);

        var selectedTagIds =
            NormalizeSelectedTagIds(
                model.SelectedTagIds);

        model.SelectedTagIds =
            selectedTagIds;

        await ValidateSelectedTagsAsync(
            selectedTagIds,
            cancellationToken);

        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.Variables)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.VersionTags)

                .Include(promptEntity =>
                    promptEntity.PromptTags)

                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: Find the author's editable version.
        //     For an unpublished prompt this is normally v1 Draft/Rejected.
        //     For a published prompt this will later be the hidden working version.
        //
        // RU: Находим редактируемую версию автора.
        //     Для неопубликованного промпта это обычно v1 Draft/Rejected.
        //     Для опубликованного — скрытая рабочая версия.
        // -------------------------------------------------------------------------

        var currentVersion =
            GetEditableVersion(
                prompt.Versions);

        if (currentVersion is null)
        {
            TempData["ErrorMessage"] =
                "У промпта сейчас нет версии, доступной для редактирования.";

            return RedirectToAction(
                nameof(My));
        }

        var categoryExists =
            await _dbContext.Categories
                .AsNoTracking()
                .AnyAsync(
                    category =>
                        category.Id ==
                            model.CategoryId &&
                        category.IsActive,
                    cancellationToken);

        if (!categoryExists)
        {
            ModelState.AddModelError(
                nameof(model.CategoryId),
                "Выбранная категория не существует.");
        }

        VisualPromptTemplateCompileResult?
            compileResult =
                null;

        if (model.EnableConstructor)
        {
            List<VisualPromptFieldDefinition>
                fields;

            try
            {
                fields =
                    JsonSerializer.Deserialize<
                        List<VisualPromptFieldDefinition>>(
                        model.ConstructorFieldsJson
                            ?? "[]",
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive =
                                true
                        })
                    ?? new List<
                        VisualPromptFieldDefinition>();
            }
            catch (JsonException)
            {
                fields =
                    new List<
                        VisualPromptFieldDefinition>();

                ModelState.AddModelError(
                    nameof(model.TemplateText),
                    "Не удалось прочитать настройки заполняемых полей.");
            }

            if (ModelState.IsValid)
            {
                compileResult =
                    _visualTemplateCompiler.Compile(
                        model.TemplateText
                            ?? string.Empty,
                        fields);

                foreach (var error in
                         compileResult.Errors)
                {
                    ModelState.AddModelError(
                        nameof(model.TemplateText),
                        error);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            model.TagOptions =
                await LoadTagOptionsAsync(
                    selectedTagIds,
                    cancellationToken);

            return View(
                model);
        }

        var hasPublishedVersion =
            GetPublishedVersion(
                prompt.Versions) is not null;

        var updatedAtUtc =
            DateTime.UtcNow;

        // -------------------------------------------------------------------------
        // EN: Before the first publication Prompt also acts as the draft snapshot.
        //     Once a published version exists, working-version changes must not
        //     leak into the public Prompt snapshot before moderation.
        //
        // RU: До первой публикации Prompt также хранит данные черновика.
        //     После появления опубликованной версии изменения рабочей версии
        //     не должны попадать в публичный Prompt до модерации.
        // -------------------------------------------------------------------------

        if (!hasPublishedVersion)
        {
            prompt.Title =
                model.Title.Trim();

            prompt.Summary =
                model.Summary.Trim();

            prompt.CategoryId =
                model.CategoryId;

            prompt.Difficulty =
                model.Difficulty;

            prompt.UpdatedAtUtc =
                updatedAtUtc;

            SyncPromptTags(
                prompt,
                selectedTagIds);
        }


        currentVersion.Title =
            model.Title.Trim();

        currentVersion.Summary =
            model.Summary.Trim();

        currentVersion.CategoryId =
            model.CategoryId;

        currentVersion.Difficulty =
            model.Difficulty;

        currentVersion.LanguageCode =
            prompt.LanguageCode;

        currentVersion.ReadyText =
            (model.ReadyText
             ?? string.Empty).Trim();

        currentVersion.TemplateText =
            compileResult?.TemplateText
            ?? string.Empty;

        currentVersion.UsageInstructions =
            (model.UsageInstructions
             ?? string.Empty).Trim();

        currentVersion.ExampleResult =
            (model.ExampleResult
             ?? string.Empty).Trim();

        currentVersion.ChangeSummary =
            "Изменение черновика";

        currentVersion.UpdatedAtUtc =
            updatedAtUtc;

        // -------------------------------------------------------------------------
        // EN: Editing a rejected working version returns it to Draft.
        //     For a first unpublished version keep legacy Prompt status aligned.
        //
        // RU: Редактирование отклонённой рабочей версии возвращает её в Draft.
        //     Для первой неопубликованной версии синхронизируем и Prompt.
        // -------------------------------------------------------------------------

        if (currentVersion.Status ==
            PromptVersionStatus.Rejected)
        {
            currentVersion.Status =
                PromptVersionStatus.Draft;

            currentVersion.SubmittedAtUtc =
                null;

            currentVersion.ModeratedAtUtc =
                null;

            currentVersion.ModeratedById =
                null;

            currentVersion.ModerationComment =
                null;

            if (!hasPublishedVersion)
            {
                prompt.Status =
                    PromptStatus.Draft;

                prompt.SubmittedAtUtc =
                    null;

                prompt.ModeratedAtUtc =
                    null;

                prompt.ModeratedById =
                    null;

                prompt.ModerationComment =
                    null;
            }
        }


        var previousVariables =
            currentVersion.Variables
                .ToArray();

        _dbContext.PromptVariables
            .RemoveRange(
                previousVariables);

        currentVersion.Variables.Clear();

        var compiledVariables =
            compileResult?.Variables
            ?? Array.Empty<
                CompiledPromptVariable>();

        foreach (var variable in
                 compiledVariables)
        {
            currentVersion.Variables.Add(
                new PromptVariable
                {
                    Key =
                        variable.Key,

                    Label =
                        variable.Label,

                    VariableType =
                        variable.VariableType,

                    Placeholder =
                        variable.Placeholder,

                    HelpText =
                        variable.HelpText,

                    IsRequired =
                        variable.IsRequired,

                    DefaultValue =
                        variable.DefaultValue,

                    OptionsJson =
                        variable.OptionsJson,

                    SortOrder =
                        variable.SortOrder
                });
        }

        // -------------------------------------------------------------------------
        // EN: Tags belong to the working version.
        //     Public PromptTags are changed only before the first publication.
        //
        // RU: Теги сохраняем в рабочую версию.
        //     Публичные PromptTags менялись выше только до первой публикации.
        // -------------------------------------------------------------------------

        SyncPromptVersionTags(
            currentVersion,
            selectedTagIds);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Изменения сохранены.";

        return RedirectToAction(
            nameof(My));
    }

    [Authorize]
    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> Preview(
    int id,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .AsNoTracking()

                .Include(promptEntity =>
                    promptEntity.Author)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.Variables)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.Category)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.VersionTags)
                        .ThenInclude(versionTag =>
                            versionTag.Tag)

                .Include(promptEntity =>
                    promptEntity.MediaItems)

                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        // -------------------------------------------------------------------------
        // EN: Prefer the author's working version.
        //     If there is no working version, show the published one.
        //
        // RU: Для предпросмотра сначала берём рабочую версию автора.
        //     Если её нет — опубликованную.
        // -------------------------------------------------------------------------

        var currentVersion =
            GetWorkingVersion(
                prompt.Versions)
            ?? GetPublishedVersion(
                prompt.Versions);

        if (currentVersion is null)
        {
            return NotFound();
        }

        var model =
    CreateViewModel(
        prompt,
        currentVersion,
        includeWorkingVersionMedia:
            true);

        ConfigurePreviewModel(
            model,
            prompt,
            currentVersion);

        return View(
            "Details",
            model);
    }


    [Authorize]
    [HttpPost("{id:int}/preview/render")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenderPreview(
    int id,
    PromptDetailsViewModel submittedModel,
    CancellationToken cancellationToken)
    {
        var authorId =
            _userManager.GetUserId(
                User);

        if (string.IsNullOrWhiteSpace(
                authorId))
        {
            return Challenge();
        }

        var prompt =
            await _dbContext.Prompts
                .AsNoTracking()

                .Include(promptEntity =>
                    promptEntity.Author)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.Variables)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.Category)

                .Include(promptEntity =>
                    promptEntity.Versions)
                    .ThenInclude(version =>
                        version.VersionTags)
                        .ThenInclude(versionTag =>
                            versionTag.Tag)

                .Include(promptEntity =>
                    promptEntity.MediaItems)

                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    promptEntity =>
                        promptEntity.Id ==
                            id &&
                        promptEntity.AuthorId ==
                            authorId,
                    cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        var currentVersion =
            GetWorkingVersion(
                prompt.Versions)
            ?? GetPublishedVersion(
                prompt.Versions);

        if (currentVersion is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(
                currentVersion.TemplateText) ||
            currentVersion.Variables.Count ==
                0)
        {
            return BadRequest(
                "У этого промпта нет конструктора.");
        }

        var model =
    CreateViewModel(
        prompt,
        currentVersion,
        includeWorkingVersionMedia:
            true);

        ConfigurePreviewModel(
            model,
            prompt,
            currentVersion);

        model.Values =
            new Dictionary<string, string?>(
                submittedModel.Values,
                StringComparer.OrdinalIgnoreCase);

        var renderResult =
            _renderer.Render(
                currentVersion.TemplateText,
                currentVersion.Variables
                    .OrderBy(variable =>
                        variable.SortOrder)
                    .ToArray(),
                model.Values);

        model.Errors =
            renderResult.Errors;

        model.RenderedText =
            renderResult.RenderedText;

        return View(
            "Details",
            model);
    }



    // -------------------------------------------------------------------------
    // Public prompt details and rendering
    // -------------------------------------------------------------------------

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(
        string slug,
        CancellationToken cancellationToken)
    {
        var prompt = await LoadPromptAsync(
            slug,
            cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        var actorKeyHash =
    _promptMetricActorKeyProvider
        .GetActorKeyHash(
            HttpContext);

        if (!string.IsNullOrWhiteSpace(
                actorKeyHash))
        {
            var viewMetric =
                await _promptMetricService
                    .RegisterAsync(
                        prompt.Id,
                        PromptMetricKind.View,
                        actorKeyHash,
                        cancellationToken);

            if (!viewMetric.PromptAvailable)
            {
                return NotFound();
            }

            prompt.ViewCount =
                viewMetric.CurrentCount;
        }

        var currentVersion =
    GetPublishedVersion(
        prompt.Versions);

        if (currentVersion is null)
        {
            return NotFound();
        }

        var model = CreateViewModel(
            prompt,
            currentVersion);

        model.AuthorSupport =
    await _authorSupportService
        .GetPublicAsync(
            prompt.AuthorId,
            model.AuthorName,
            cancellationToken);

        var currentUserId =
    _userManager.GetUserId(
        User);

        model.CanUnpublish =
            !string.IsNullOrWhiteSpace(
                currentUserId) &&
            prompt.AuthorId ==
                currentUserId &&
            prompt.Status ==
                PromptStatus.Published;

        await ConfigureFavoriteStateAsync(
            model,
            prompt.Id,
            cancellationToken);

        await ConfigureRatingStateAsync(
            model,
            prompt.Id,
            cancellationToken);

        await ConfigureReportStateAsync(
            model,
            prompt.Id,
            cancellationToken);

        await ConfigureCommentsAsync(
            model,
            prompt.Id,
            cancellationToken);

        return View(model);
    }

    [HttpPost("{slug}/render")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Render(
        string slug,
        PromptDetailsViewModel submittedModel,
        CancellationToken cancellationToken)
    {
        var prompt = await LoadPromptAsync(
            slug,
            cancellationToken);

        if (prompt is null)
        {
            return NotFound();
        }

        var currentVersion =
    GetPublishedVersion(
        prompt.Versions);

        if (currentVersion is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(
                currentVersion.TemplateText) ||
            currentVersion.Variables.Count == 0)
        {
            return BadRequest(
                "У этого промпта нет конструктора.");
        }

        var model = CreateViewModel(
            prompt,
            currentVersion);

        model.AuthorSupport =
    await _authorSupportService
        .GetPublicAsync(
            prompt.AuthorId,
            model.AuthorName,
            cancellationToken);

        var currentUserId =
    _userManager.GetUserId(
        User);

        model.CanUnpublish =
            !string.IsNullOrWhiteSpace(
                currentUserId) &&
            prompt.AuthorId ==
                currentUserId &&
            prompt.Status ==
                PromptStatus.Published;

        model.Values =
            new Dictionary<string, string?>(
                submittedModel.Values,
                StringComparer.OrdinalIgnoreCase);

        var renderResult = _renderer.Render(
            currentVersion.TemplateText,
            currentVersion.Variables.ToArray(),
            model.Values);

        model.Errors = renderResult.Errors;

        model.RenderedText = renderResult.RenderedText;

        await ConfigureFavoriteStateAsync(
            model,
            prompt.Id,
            cancellationToken);

        await ConfigureRatingStateAsync(
            model,
            prompt.Id,
            cancellationToken);

        await ConfigureCommentsAsync(
            model,
            prompt.Id,
            cancellationToken);

        await ConfigureReportStateAsync(
            model,
            prompt.Id,
            cancellationToken);

        return View("Details", model);
    }



    // -------------------------------------------------------------------------
    // Copy metrics
    // -------------------------------------------------------------------------

    [HttpPost("{slug}/copy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCopy(
    string slug,
    CancellationToken cancellationToken)
    {
        var promptMetricData =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    prompt.Slug == slug &&
                    prompt.Status ==
                        PromptStatus.Published &&
                    prompt.Category.IsActive)
                .Select(prompt =>
                    new
                    {
                        prompt.Id,
                        prompt.CopyCount
                    })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (promptMetricData is null)
        {
            return NotFound();
        }

        var actorKeyHash =
            _promptMetricActorKeyProvider
                .GetActorKeyHash(
                    HttpContext);

        if (string.IsNullOrWhiteSpace(
                actorKeyHash))
        {
            return Json(
                new
                {
                    copyCount =
                        promptMetricData.CopyCount,

                    counted =
                        false
                });
        }

        var copyMetric =
            await _promptMetricService
                .RegisterAsync(
                    promptMetricData.Id,
                    PromptMetricKind.Copy,
                    actorKeyHash,
                    cancellationToken);

        if (!copyMetric.PromptAvailable)
        {
            return NotFound();
        }

        return Json(
            new
            {
                copyCount =
                    copyMetric.CurrentCount,

                counted =
                    copyMetric.Counted
            });
    }


    // -------------------------------------------------------------------------
    // Prompt loading and details mapping helpers
    // -------------------------------------------------------------------------

    private async Task<Prompt?> LoadPromptAsync(
    string slug,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Prompts
            .AsNoTracking()
            .Include(prompt =>
                prompt.Author)
            .Include(prompt =>
                prompt.Category)
            .Include(prompt =>
                prompt.PromptTags)
                .ThenInclude(promptTag =>
                    promptTag.Tag)
            .Include(prompt =>
                prompt.Versions)
                .ThenInclude(version =>
                    version.Variables)
            .Include(prompt =>
    prompt.Versions)
    .ThenInclude(version =>
        version.Category)

.Include(prompt =>
    prompt.Versions)
    .ThenInclude(version =>
        version.VersionTags)
        .ThenInclude(versionTag =>
            versionTag.Tag)
            .Include(prompt =>
                prompt.MediaItems)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
    prompt =>
        prompt.Slug == slug &&
        prompt.Status ==
            PromptStatus.Published &&
        prompt.Category.IsActive,
    cancellationToken);
    }

    private static PromptDetailsViewModel CreateViewModel(
    Prompt prompt,
    PromptVersion currentVersion,
    bool includeWorkingVersionMedia = false)
    {
        var variables = currentVersion.Variables
            .OrderBy(variable => variable.SortOrder)
            .Select(variable =>
                new PromptVariableFieldViewModel
                {
                    Key = variable.Key,
                    Label = variable.Label,
                    VariableType =
                        variable.VariableType,
                    Placeholder =
                        variable.Placeholder,
                    HelpText = variable.HelpText,
                    IsRequired = variable.IsRequired,
                    DefaultValue =
                        variable.DefaultValue,
                    Options = ReadOptions(
                        variable.OptionsJson)
                })
            .ToArray();

        var values = currentVersion.Variables
            .ToDictionary(
                variable => variable.Key,
                variable =>
                    (string?)variable.DefaultValue,
                StringComparer.OrdinalIgnoreCase);

        var authorName =
            !string.IsNullOrWhiteSpace(
                prompt.Author.DisplayName)
                ? prompt.Author.DisplayName
                : "Автор";

        return new PromptDetailsViewModel
        {
            PromptId = prompt.Id,
            Slug = prompt.Slug,

            Title =
    currentVersion.Title,

            Summary =
    currentVersion.Summary,

            CategoryName =
    currentVersion.Category.Name,

            CategorySlug =
    currentVersion.Category.Slug,

            Tags =
    CreateVersionTagBadges(
        currentVersion,
        activeOnly: true),
            AuthorId = prompt.AuthorId,
            AuthorName = authorName,
            DifficultyName =
    GetDifficultyName(
        currentVersion.Difficulty),

            DifficultyValue =
    currentVersion.Difficulty
        .ToString()
        .ToLowerInvariant(),

            LanguageCode =
    currentVersion.LanguageCode,
            VersionNumber =
                currentVersion.VersionNumber,
            ViewCount = prompt.ViewCount,
            CopyCount = prompt.CopyCount,
            ReadyText = currentVersion.ReadyText,
            HasConstructor =
                !string.IsNullOrWhiteSpace(
                    currentVersion.TemplateText) &&
                currentVersion.Variables.Count > 0,
            TemplateText =
                currentVersion.TemplateText,
            UsageInstructions =
                currentVersion.UsageInstructions,
            ExampleResult =
                currentVersion.ExampleResult,
            MediaItems =
    CreateMediaGalleryItems(
        prompt,
        currentVersion,
        includeWorkingVersionMedia),
            Variables = variables,
            Values = values,
            RenderedText = null
        };
    }



    private static IReadOnlyList<PromptMediaGalleryItemViewModel>
    CreateMediaGalleryItems(
        Prompt prompt,
        PromptVersion version,
        bool includeWorkingVersionMedia = false)
    {
        var mediaQuery =
            prompt.MediaItems
                .Where(media =>
                    media.MediaType ==
                        PromptMediaType.Image);

        if (includeWorkingVersionMedia)
        {
            // ---------------------------------------------------------------------
            // EN: Preview contains currently published media plus active media
            //     belonging specifically to the working version.
            //
            // RU: В предпросмотре показываем текущие опубликованные изображения
            //     плюс Draft/Pending-медиа именно рабочей версии.
            // ---------------------------------------------------------------------

            mediaQuery =
                mediaQuery.Where(media =>
                    media.Status ==
                        PromptMediaStatus.Published
                    ||
                    (
                        media.PromptVersionId ==
                            version.Id &&
                        (
                            media.Status ==
                                PromptMediaStatus.Draft ||
                            media.Status ==
                                PromptMediaStatus.PendingModeration
                        )
                    ));
        }
        else
        {
            // ---------------------------------------------------------------------
            // EN: Public pages expose only approved media.
            // RU: Публичная страница показывает только одобренные медиа.
            // ---------------------------------------------------------------------

            mediaQuery =
                mediaQuery.Where(media =>
                    media.Status ==
                        PromptMediaStatus.Published);
        }

        return mediaQuery
            .OrderBy(media =>
                media.SortOrder)
            .ThenBy(media =>
                media.CreatedAtUtc)
            .ThenBy(media =>
                media.Id)
            .Select(media =>
                new PromptMediaGalleryItemViewModel
                {
                    Id =
                        media.Id,

                    Caption =
                        media.Caption,

                    AltText =
                        media.AltText,

                    SortOrder =
                        media.SortOrder,

                    IsCover =
                        media.IsCover,

                    Status =
                        media.Status,

                    StatusName =
                        GetMediaStatusName(
                            media.Status)
                })
            .ToArray();
    }

    private static string GetMediaStatusName(
        PromptMediaStatus status)
    {
        return status switch
        {
            PromptMediaStatus.Draft =>
                "Черновик",

            PromptMediaStatus.PendingModeration =>
                "На модерации",

            PromptMediaStatus.Published =>
                "Опубликовано",

            PromptMediaStatus.Rejected =>
                "Отклонено",

            PromptMediaStatus.Archived =>
                "В архиве",

            _ =>
                status.ToString()
        };
    }

    private static string GetDifficultyName(
        PromptDifficulty difficulty)
    {
        return difficulty switch
        {
            PromptDifficulty.Beginner =>
                "Для новичков",

            PromptDifficulty.Intermediate =>
                "Средний",

            PromptDifficulty.Advanced =>
                "Продвинутый",

            _ => difficulty.ToString()
        };
    }



    // -------------------------------------------------------------------------
    // Rating helpers
    // -------------------------------------------------------------------------

    private async Task<Dictionary<int, PromptRatingSummary>>
    LoadRatingSummariesAsync(
        IReadOnlyCollection<int> promptIds,
        CancellationToken cancellationToken)
    {
        if (promptIds.Count == 0)
        {
            return new Dictionary<int, PromptRatingSummary>();
        }

        return await _dbContext.PromptRatings
            .AsNoTracking()
            .Where(rating =>
                promptIds.Contains(
                    rating.PromptId))
            .GroupBy(rating =>
                rating.PromptId)
            .Select(group => new
            {
                PromptId = group.Key,

                AverageRating =
                    group.Average(rating =>
                        (double)rating.Score),

                RatingCount =
                    group.Count()
            })
            .ToDictionaryAsync(
                item => item.PromptId,
                item => new PromptRatingSummary(
                    item.AverageRating,
                    item.RatingCount),
                cancellationToken);
    }

    private async Task<Dictionary<int, int>>
        LoadCurrentUserRatingsAsync(
            IReadOnlyCollection<int> promptIds,
            string? userId,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            promptIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _dbContext.PromptRatings
            .AsNoTracking()
            .Where(rating =>
                rating.UserId == userId &&
                promptIds.Contains(
                    rating.PromptId))
            .ToDictionaryAsync(
                rating => rating.PromptId,
                rating => rating.Score,
                cancellationToken);
    }

    private async Task<PromptRatingSummary>
        GetPromptRatingSummaryAsync(
            int promptId,
            CancellationToken cancellationToken)
    {
        var result =
            await _dbContext.PromptRatings
                .AsNoTracking()
                .Where(rating =>
                    rating.PromptId == promptId)
                .GroupBy(rating =>
                    rating.PromptId)
                .Select(group => new PromptRatingSummary(
                    group.Average(rating =>
                        (double)rating.Score),
                    group.Count()))
                .SingleOrDefaultAsync(
                    cancellationToken);

        return result
            ?? new PromptRatingSummary(
                0,
                0);
    }

    private async Task ConfigureRatingStateAsync(
        PromptDetailsViewModel model,
        int promptId,
        CancellationToken cancellationToken)
    {
        var summary =
            await GetPromptRatingSummaryAsync(
                promptId,
                cancellationToken);

        model.AverageRating =
            summary.AverageRating;

        model.RatingCount =
            summary.RatingCount;

        model.CurrentUserRating = null;

        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        model.CurrentUserRating =
            await _dbContext.PromptRatings
                .AsNoTracking()
                .Where(rating =>
                    rating.UserId == userId &&
                    rating.PromptId == promptId)
                .Select(rating =>
                    (int?)rating.Score)
                .SingleOrDefaultAsync(
                    cancellationToken);
    }




    // -------------------------------------------------------------------------
    // Prompt status display helpers
    // -------------------------------------------------------------------------

    private static string GetPromptStatusName(
    PromptStatus status)
    {
        return status switch
        {
            PromptStatus.Draft =>
                "Черновик",

            PromptStatus.PendingModeration =>
                "На модерации",

            PromptStatus.Published =>
                "Опубликован",

            PromptStatus.Rejected =>
                "Отклонён",

            PromptStatus.Archived =>
                "Скрыт модератором",

            _ => status.ToString()
        };
    }

    private static string GetPromptStatusBadgeClass(
        PromptStatus status)
    {
        return status switch
        {
            PromptStatus.Draft =>
                "text-bg-secondary",

            PromptStatus.PendingModeration =>
                "text-bg-warning",

            PromptStatus.Published =>
                "text-bg-success",

            PromptStatus.Rejected =>
                "text-bg-danger",

            PromptStatus.Archived =>
                "text-bg-dark",

            _ => "text-bg-secondary"
        };
    }



    // -------------------------------------------------------------------------
    // Prompt template and constructor helpers
    // -------------------------------------------------------------------------

    private static string CreateDisplayTemplate(
    string templateText,
    IEnumerable<PromptVariable> variables)
    {
        var displayTemplate = Regex.Replace(
            templateText,
            @"\[\[if\s+(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\]\](?<content>.*?)\[\[endif\]\]",
            match =>
                match.Groups["content"]
                    .Value
                    .Trim('\r', '\n'),
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.CultureInvariant);

        foreach (var variable in variables
                     .OrderByDescending(variable =>
                         variable.Key.Length))
        {
            var placeholderPattern =
                @"\{\{\s*" +
                Regex.Escape(variable.Key) +
                @"\s*\}\}";

            displayTemplate = Regex.Replace(
                displayTemplate,
                placeholderPattern,
                _ => $"[{variable.Label}]",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);
        }

        return displayTemplate.Trim();
    }

    private static string CreateVisualFieldsJson(
    IEnumerable<PromptVariable> variables)
    {
        var fields = variables
            .OrderBy(variable =>
                variable.SortOrder)
            .Select(variable =>
                new VisualPromptFieldDefinition
                {
                    Id = variable.Key,
                    Label = variable.Label,

                    VariableType =
                        variable.VariableType,

                    IsRequired =
                        variable.IsRequired,

                    Placeholder =
                        variable.Placeholder
                        ?? string.Empty,

                    HelpText =
                        variable.HelpText
                        ?? string.Empty,

                    Options =
                        ReadPromptVariableOptions(
                            variable.OptionsJson)
                            .ToList()
                })
            .ToArray();

        return JsonSerializer.Serialize(
            fields,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase
            });
    }


    // -------------------------------------------------------------------------
    // Prompt report and favorite state helpers
    // -------------------------------------------------------------------------

    private async Task ConfigureReportStateAsync(
    PromptDetailsViewModel model,
    int promptId,
    CancellationToken cancellationToken)
    {
        model.HasCurrentUserOpenReport = false;

        if (model.IsPreview ||
            User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        model.HasCurrentUserOpenReport =
            await _dbContext.PromptReports
                .AsNoTracking()
                .AnyAsync(
                    report =>
                        report.PromptId == promptId &&
                        report.ReporterId == userId &&
                        report.Status == PromptReportStatus.New,
                    cancellationToken);
    }


    private static IReadOnlyList<string>
    ReadPromptVariableOptions(
        string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(
            optionsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document =
                JsonDocument.Parse(
                    optionsJson);

            if (document.RootElement.ValueKind !=
                JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var options =
                new List<string>();

            foreach (var element in
                     document.RootElement
                         .EnumerateArray())
            {
                string? value = null;

                if (element.ValueKind ==
                    JsonValueKind.String)
                {
                    value =
                        element.GetString();
                }
                else if (
                    element.ValueKind ==
                        JsonValueKind.Object &&
                    element.TryGetProperty(
                        "value",
                        out var valueElement))
                {
                    value =
                        valueElement.GetString();
                }

                if (!string.IsNullOrWhiteSpace(
                    value))
                {
                    options.Add(value);
                }
            }

            return options;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }


    private async Task ConfigureFavoriteStateAsync(
    PromptDetailsViewModel model,
    int promptId,
    CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            model.IsFavorite = false;
            return;
        }

        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            model.IsFavorite = false;
            return;
        }

        model.IsFavorite =
            await _dbContext.PromptFavorites
                .AsNoTracking()
                .AnyAsync(
                    favorite =>
                        favorite.UserId == userId &&
                        favorite.PromptId == promptId,
                    cancellationToken);
    }


    // -------------------------------------------------------------------------
    // Prompt option mapping helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<
        PromptSelectOptionViewModel> ReadOptions(
            string optionsJson)
    {
        try
        {
            using var document =
                JsonDocument.Parse(optionsJson);

            if (document.RootElement.ValueKind !=
                JsonValueKind.Array)
            {
                return Array.Empty<
                    PromptSelectOptionViewModel>();
            }

            var options =
                new List<PromptSelectOptionViewModel>();

            foreach (var element in
                     document.RootElement
                         .EnumerateArray())
            {
                if (!element.TryGetProperty(
                        "value",
                        out var valueProperty))
                {
                    continue;
                }

                var value =
                    valueProperty.GetString();

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var label = value;

                if (element.TryGetProperty(
                        "label",
                        out var labelProperty))
                {
                    label =
                        labelProperty.GetString()
                        ?? value;
                }

                options.Add(
                    new PromptSelectOptionViewModel
                    {
                        Value = value,
                        Label = label
                    });
            }

            return options;
        }
        catch (JsonException)
        {
            return Array.Empty<
                PromptSelectOptionViewModel>();
        }
    }

    private static string GetAuthorName(
    ApplicationUser author)
    {
        if (!string.IsNullOrWhiteSpace(author.DisplayName))
        {
            return author.DisplayName;
        }

        return "Автор";
    }

    private static void ConfigurePreviewModel(
    PromptDetailsViewModel model,
    Prompt prompt,
    PromptVersion version)
    {
        model.PromptId =
            prompt.Id;

        model.IsPreview =
            true;

        model.CanEdit =
            version.Status is
                PromptVersionStatus.Draft or
                PromptVersionStatus.Rejected;

        model.CanSubmitForModeration =
            version.Status ==
                PromptVersionStatus.Draft;
    }



    // -------------------------------------------------------------------------
    // Prompt editor lookup helpers
    // -------------------------------------------------------------------------

    private async Task<IReadOnlyList<SelectListItem>>
    LoadCategoryOptionsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Include(category => category.Parent)
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category =>
                new SelectListItem
                {
                    Value = category.Id.ToString(),

                    Text = category.Parent != null
                        ? category.Parent.Name +
                          " → " +
                          category.Name
                        : category.Name
                })
            .ToListAsync(cancellationToken);
    }

    private async Task<string> CreateUniqueSlugAsync(
        string baseSlug,
        CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        var suffix = 2;

        while (await _dbContext.Prompts
                   .AsNoTracking()
                   .AnyAsync(
                       prompt =>
                           prompt.Slug == candidate,
                       cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }




    private const int MaxPromptTagCount = 5;

    private async Task<IReadOnlyList<SelectListItem>> LoadTagOptionsAsync(
        IReadOnlyCollection<int> selectedTagIds,
        CancellationToken cancellationToken)
    {
        var selectedTagSet =
            selectedTagIds.ToHashSet();

        return await _dbContext.Tags
            .AsNoTracking()
            .Where(tag =>
                tag.IsActive ||
                selectedTagSet.Contains(tag.Id))
            .OrderBy(tag =>
                tag.SortOrder)
            .ThenBy(tag =>
                tag.Name)
            .Select(tag =>
                new SelectListItem
                {
                    Value =
                        tag.Id.ToString(),

                    Text =
                        tag.IsActive
                            ? tag.Name
                            : tag.Name + " · скрыт",

                    Selected =
                        selectedTagSet.Contains(tag.Id)
                })
            .ToListAsync(
                cancellationToken);
    }

    private static int[] NormalizeSelectedTagIds(
        IEnumerable<int>? tagIds)
    {
        return tagIds?
            .Where(tagId =>
                tagId > 0)
            .Distinct()
            .ToArray()
            ?? Array.Empty<int>();
    }

    private async Task ValidateSelectedTagsAsync(
        int[] selectedTagIds,
        CancellationToken cancellationToken)
    {
        if (selectedTagIds.Length > MaxPromptTagCount)
        {
            ModelState.AddModelError(
                nameof(CreatePromptViewModel.SelectedTagIds),
                $"Можно выбрать не более {MaxPromptTagCount} тегов.");
        }

        if (selectedTagIds.Length == 0)
        {
            return;
        }

        var activeTagCount =
            await _dbContext.Tags
                .AsNoTracking()
                .CountAsync(
                    tag =>
                        selectedTagIds.Contains(tag.Id) &&
                        tag.IsActive,
                    cancellationToken);

        if (activeTagCount != selectedTagIds.Length)
        {
            ModelState.AddModelError(
                nameof(CreatePromptViewModel.SelectedTagIds),
                "Выберите только активные теги.");
        }
    }

    private static void SyncPromptVersionTags(
    PromptVersion version,
    IReadOnlyCollection<int> selectedTagIds)
    {
        version.VersionTags.Clear();

        foreach (var tagId in selectedTagIds)
        {
            version.VersionTags.Add(
                new PromptVersionTag
                {
                    PromptVersion =
                        version,

                    TagId =
                        tagId
                });
        }
    }


    private static void SyncPromptTags(
        Prompt prompt,
        IReadOnlyCollection<int> selectedTagIds)
    {
        prompt.PromptTags.Clear();

        foreach (var tagId in selectedTagIds)
        {
            prompt.PromptTags.Add(
                new PromptTag
                {
                    Prompt =
                        prompt,

                    TagId =
                        tagId
                });
        }
    }


    private static IReadOnlyList<PromptTagBadgeViewModel> CreateTagBadges(
    Prompt prompt,
    bool activeOnly)
    {
        var query =
            prompt.PromptTags
                .AsEnumerable();

        if (activeOnly)
        {
            query =
                query.Where(promptTag =>
                    promptTag.Tag.IsActive);
        }

        return query
            .OrderBy(promptTag =>
                promptTag.Tag.SortOrder)
            .ThenBy(promptTag =>
                promptTag.Tag.Name)
            .Select(promptTag =>
                new PromptTagBadgeViewModel
                {
                    Name =
                        promptTag.Tag.Name,

                    Slug =
                        promptTag.Tag.Slug
                })
            .ToArray();
    }


    private static IReadOnlyList<PromptTagBadgeViewModel>
    CreateVersionTagBadges(
        PromptVersion version,
        bool activeOnly)
    {
        var query =
            version.VersionTags
                .AsEnumerable();

        if (activeOnly)
        {
            query =
                query.Where(versionTag =>
                    versionTag.Tag.IsActive);
        }

        return query
            .OrderBy(versionTag =>
                versionTag.Tag.SortOrder)
            .ThenBy(versionTag =>
                versionTag.Tag.Name)
            .Select(versionTag =>
                new PromptTagBadgeViewModel
                {
                    Name =
                        versionTag.Tag.Name,

                    Slug =
                        versionTag.Tag.Slug
                })
            .ToArray();
    }


    private static PromptVersion CreateWorkingVersion(
    PromptVersion source,
    int versionNumber,
    DateTime createdAtUtc)
    {
        var version =
            new PromptVersion
            {
                PromptId =
                    source.PromptId,

                VersionNumber =
                    versionNumber,

                Status =
                    PromptVersionStatus.Draft,

                IsCurrent =
                    false,

                Title =
                    source.Title,

                Summary =
                    source.Summary,

                LanguageCode =
                    source.LanguageCode,

                Difficulty =
                    source.Difficulty,

                CategoryId =
                    source.CategoryId,

                ReadyText =
                    source.ReadyText,

                TemplateText =
                    source.TemplateText,

                UsageInstructions =
                    source.UsageInstructions,

                ExampleResult =
                    source.ExampleResult,

                ChangeSummary =
                    "Рабочая копия опубликованной версии",

                CreatedAtUtc =
                    createdAtUtc,

                UpdatedAtUtc =
                    createdAtUtc
            };


        foreach (var sourceVariable in
                 source.Variables
                     .OrderBy(variable =>
                         variable.SortOrder))
        {
            version.Variables.Add(
                new PromptVariable
                {
                    Key =
                        sourceVariable.Key,

                    Label =
                        sourceVariable.Label,

                    VariableType =
                        sourceVariable.VariableType,

                    Placeholder =
                        sourceVariable.Placeholder,

                    HelpText =
                        sourceVariable.HelpText,

                    IsRequired =
                        sourceVariable.IsRequired,

                    DefaultValue =
                        sourceVariable.DefaultValue,

                    OptionsJson =
                        sourceVariable.OptionsJson,

                    SortOrder =
                        sourceVariable.SortOrder
                });
        }


        foreach (var sourceTag in
                 source.VersionTags)
        {
            version.VersionTags.Add(
                new PromptVersionTag
                {
                    TagId =
                        sourceTag.TagId
                });
        }

        return version;
    }



    private static string GetTagAlphabetGroup(
        string tagName)
    {
        var normalizedName =
            tagName.Trim();

        if (normalizedName.Length == 0)
        {
            return "#";
        }

        var firstCharacter =
            normalizedName[0];

        if (char.IsDigit(
                firstCharacter))
        {
            return "0–9";
        }

        if (char.IsLetter(
                firstCharacter))
        {
            return char
                .ToUpperInvariant(
                    firstCharacter)
                .ToString();
        }

        return "#";
    }



    private static PromptVersion? GetPublishedVersion(
        IEnumerable<PromptVersion> versions)
    {
        return versions
            .SingleOrDefault(version =>
                version.IsCurrent &&
                version.Status ==
                    PromptVersionStatus.Published);
    }

    private static PromptVersion? GetWorkingVersion(
        IEnumerable<PromptVersion> versions)
    {
        return versions
            .Where(version =>
                version.Status is
                    PromptVersionStatus.Draft or
                    PromptVersionStatus.PendingModeration or
                    PromptVersionStatus.Rejected)
            .OrderByDescending(version =>
                version.VersionNumber)
            .SingleOrDefault();
    }

    private static PromptVersion? GetEditableVersion(
        IEnumerable<PromptVersion> versions)
    {
        return versions
            .Where(version =>
                version.Status is
                    PromptVersionStatus.Draft or
                    PromptVersionStatus.Rejected)
            .OrderByDescending(version =>
                version.VersionNumber)
            .SingleOrDefault();
    }

    private static string CreateVariableLabel(
        string key)
    {
        var knownLabels =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["topic"] = "Тема",
                ["title"] = "Название",
                ["audience"] = "Целевая аудитория",
                ["style"] = "Стиль",
                ["tone"] = "Тон",
                ["language"] = "Язык",
                ["length"] = "Объём",
                ["requirements"] =
                    "Дополнительные требования",
                ["chapter_count"] =
                    "Количество глав",
                ["work_type"] = "Тип работы",
                ["speciality"] = "Специальность"
            };

        if (knownLabels.TryGetValue(
                key,
                out var label))
        {
            return label;
        }

        var words = key
            .Replace('_', ' ')
            .Trim();

        if (words.Length == 0)
        {
            return key;
        }

        return char.ToUpperInvariant(words[0]) +
               words[1..];
    }





    private async Task ConfigureCommentsAsync(
    PromptDetailsViewModel model,
    int promptId,
    CancellationToken cancellationToken)
    {
        if (model.IsPreview)
        {
            model.Comments =
                Array.Empty<PromptCommentItemViewModel>();

            return;
        }

        var currentUserId =
            _userManager.GetUserId(User);

        var canModerateComments =
            CanModerateComments();

        var openReportedCommentIds =
            new HashSet<int>();

        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            openReportedCommentIds = await _dbContext.PromptCommentReports
                .AsNoTracking()
                .Where(report =>
                    report.ReporterId == currentUserId &&
                    report.Status == PromptCommentReportStatus.Open)
                .Select(report => report.CommentId)
                .ToHashSetAsync(cancellationToken);
        }

        var comments = await _dbContext.PromptComments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Where(comment =>
                comment.PromptId == promptId &&
                comment.ParentCommentId == null &&
                (
                    comment.Status == PromptCommentStatus.Visible ||
                    (
                        canModerateComments &&
                        comment.Status == PromptCommentStatus.HiddenByModerator
                    )
                ))
            .OrderBy(comment => comment.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        model.Comments = comments
            .Select(comment =>
                new PromptCommentItemViewModel
                {
                    CommentId =
                        comment.Id,

                    AuthorId =
                        comment.AuthorId,

                    AuthorName =
                        GetAuthorName(comment.Author),

                    Body =
                        comment.Body,

                    CreatedAtUtc =
                        comment.CreatedAtUtc,

                    UpdatedAtUtc =
                        comment.UpdatedAtUtc,

                    IsHiddenByModerator =
                        comment.Status == PromptCommentStatus.HiddenByModerator,

                    ModerationReason =
                        comment.ModerationReason,

                    CanDelete =
                        comment.Status == PromptCommentStatus.Visible &&
                        !string.IsNullOrWhiteSpace(currentUserId) &&
                        string.Equals(
                            comment.AuthorId,
                            currentUserId,
                            StringComparison.Ordinal),

                    CanHide =
                        comment.Status == PromptCommentStatus.Visible &&
                        canModerateComments,

                    HasOpenReportByCurrentUser =
                        openReportedCommentIds.Contains(comment.Id),

                    CanReport =
                        comment.Status == PromptCommentStatus.Visible &&
                        !canModerateComments &&
                        !string.IsNullOrWhiteSpace(currentUserId) &&
                        !string.Equals(
                            comment.AuthorId,
                            currentUserId,
                            StringComparison.Ordinal) &&
                        !openReportedCommentIds.Contains(comment.Id)
                })
            .ToArray();
    }

    private bool CanModerateComments()
    {
        return User.IsInRole(ApplicationRoles.Moderator) ||
               User.IsInRole(ApplicationRoles.Administrator);
    }


    // -----------------------------------------------------------------------------
    // EN: Delete files created by an unsuccessful prompt creation request.
    // RU: Удаление файлов, созданных при неудачном создании промпта.
    // -----------------------------------------------------------------------------

    private async Task DeleteStoredPromptImagesQuietlyAsync(
        IEnumerable<StoredMediaFile> storedFiles)
    {
        foreach (var storedFile in storedFiles)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(
                        storedFile.ThumbnailStorageKey))
                {
                    await _mediaStorageService
                        .DeleteAsync(
                            storedFile.ThumbnailStorageKey,
                            CancellationToken.None);
                }

                if (!string.IsNullOrWhiteSpace(
                        storedFile.StorageKey))
                {
                    await _mediaStorageService
                        .DeleteAsync(
                            storedFile.StorageKey,
                            CancellationToken.None);
                }
            }
            catch
            {
                // -----------------------------------------------------------------
                // EN: Cleanup must not hide the original creation error.
                // RU: Ошибка очистки не должна скрывать исходную ошибку создания.
                // -----------------------------------------------------------------
            }
        }
    }

}
