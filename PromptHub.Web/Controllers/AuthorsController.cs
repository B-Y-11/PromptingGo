using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Authors;
using PromptHub.Web.Models.ViewModels.Prompts;
using PromptHub.Web.Services.PromptStats;
using PromptHub.Web.Services.PromptMedia;
using PromptHub.Web.Services.AuthorSupport;

namespace PromptHub.Web.Controllers;

[Route("authors")]
public sealed class AuthorsController : Controller
{

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPromptStatsService _promptStatsService;
    private readonly IPromptCardCoverService
    _promptCardCoverService;
    private readonly IAuthorSupportService
    _authorSupportService;

    public AuthorsController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IPromptStatsService promptStatsService,
        IPromptCardCoverService promptCardCoverService,
        IAuthorSupportService authorSupportService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _promptStatsService = promptStatsService;
        _promptCardCoverService = promptCardCoverService;
        _authorSupportService = authorSupportService;
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> Details(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var author = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == id,
                cancellationToken);

        if (author is null)
        {
            return NotFound();
        }

        var promptEntities = await LoadPublishedAuthorPromptsAsync(
            id,
            cancellationToken);

        var promptIds = promptEntities
            .Select(prompt => prompt.Id)
            .ToArray();

        var currentUserId =
            _userManager.GetUserId(User);

        var favoritePromptIds = await LoadFavoritePromptIdsAsync(
            currentUserId,
            promptIds,
            cancellationToken);

        var currentUserRatings = await LoadCurrentUserRatingsAsync(
            currentUserId,
            promptIds,
            cancellationToken);

        var visibleCommentCounts = await _promptStatsService.GetVisibleCommentCountsAsync(
            promptIds,
            cancellationToken);

        var promptCovers =
            await _promptCardCoverService
                .GetPublishedCoversAsync(
                    promptIds,
                    cancellationToken);

        var allRatings = promptEntities
            .SelectMany(prompt => prompt.Ratings)
            .ToArray();

        var promptItems = promptEntities
            .Select(prompt => CreatePromptCatalogItem(
                prompt,
                author,
                favoritePromptIds,
                currentUserRatings,
                visibleCommentCounts,
                promptCovers))
            .ToArray();

        var authorName =
    GetAuthorName(
        author);

        var authorSupport =
            await _authorSupportService
                .GetPublicAsync(
                    author.Id,
                    authorName,
                    cancellationToken);

        var model = new AuthorProfileViewModel
        {
            AuthorId =
                author.Id,

            DisplayName =
    authorName,

            AuthorSupport =
    authorSupport,

            Bio =
                author.Bio ?? string.Empty,

            AvatarContentUrl =
                !string.IsNullOrWhiteSpace(
                    author.AvatarStorageKey) &&
                author.AvatarUpdatedAtUtc.HasValue
                    ? Url.RouteUrl(
                        "UserAvatarContent",
                        new
                        {
                            userId =
                                author.Id,

                            version =
                                author.AvatarUpdatedAtUtc
                                    .Value
                                    .Ticks
                        })
                    : null,

            CreatedAtUtc =
                author.CreatedAtUtc,

            PublishedPromptsCount =
                promptEntities.Count,

            TotalViewCount =
                promptEntities.Sum(prompt =>
                    prompt.ViewCount),

            TotalCopyCount =
                promptEntities.Sum(prompt =>
                    prompt.CopyCount),

            AverageRating =
                allRatings.Length > 0
                    ? allRatings.Average(rating =>
                        rating.Score)
                    : 0,

            RatingCount =
                allRatings.Length,

            Prompts =
                promptItems
        };

        return View(model);
    }


    private async Task<List<Prompt>> LoadPublishedAuthorPromptsAsync(
        string authorId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Category)
            .Include(prompt => prompt.PromptTags)
                .ThenInclude(promptTag => promptTag.Tag)
            .Include(prompt => prompt.Versions)
                .ThenInclude(version => version.Variables)
            .Include(prompt => prompt.Ratings)
            .Where(prompt =>
                prompt.AuthorId == authorId &&
                prompt.Status == PromptStatus.Published &&
                prompt.Category.IsActive)
            .OrderByDescending(prompt =>
                prompt.PublishedAtUtc)
            .ToListAsync(cancellationToken);
    }

    private async Task<HashSet<int>> LoadFavoritePromptIdsAsync(
        string? currentUserId,
        int[] promptIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserId) ||
            promptIds.Length == 0)
        {
            return new HashSet<int>();
        }

        var favoritePromptIds = await _dbContext.PromptFavorites
            .AsNoTracking()
            .Where(favorite =>
                favorite.UserId == currentUserId &&
                promptIds.Contains(favorite.PromptId))
            .Select(favorite =>
                favorite.PromptId)
            .ToListAsync(cancellationToken);

        return favoritePromptIds.ToHashSet();
    }

    private async Task<Dictionary<int, int>> LoadCurrentUserRatingsAsync(
        string? currentUserId,
        int[] promptIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserId) ||
            promptIds.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _dbContext.PromptRatings
            .AsNoTracking()
            .Where(rating =>
                rating.UserId == currentUserId &&
                promptIds.Contains(rating.PromptId))
            .ToDictionaryAsync(
                rating => rating.PromptId,
                rating => rating.Score,
                cancellationToken);
    }



    private static PromptCatalogItemViewModel CreatePromptCatalogItem(
    Prompt prompt,
    ApplicationUser author,
    IReadOnlySet<int> favoritePromptIds,
    IReadOnlyDictionary<int, int> currentUserRatings,
    IReadOnlyDictionary<int, int> visibleCommentCounts,
    IReadOnlyDictionary<int, PromptCardCoverViewModel> promptCovers)
    {
        return new PromptCatalogItemViewModel
        {
            PromptId =
                prompt.Id,

            Title =
                prompt.Title,

            Slug =
                prompt.Slug,

            Summary =
                prompt.Summary,

            CategoryName =
                prompt.Category.Name,

            Tags =
                CreateTagBadges(prompt),

            AuthorId =
                author.Id,

            AuthorName =
                GetAuthorName(author),

            DifficultyName =
                GetDifficultyName(prompt.Difficulty),

            HasConstructor =
                HasConstructor(prompt),

            IsFavorite =
                favoritePromptIds.Contains(prompt.Id),

            AverageRating =
                prompt.Ratings.Count > 0
                    ? prompt.Ratings.Average(rating =>
                        rating.Score)
                    : 0,

            RatingCount =
                prompt.Ratings.Count,

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
                prompt.PublishedAtUtc,

            Cover =
                promptCovers.GetValueOrDefault(
                    prompt.Id),
        };
    }

    private static IReadOnlyList<PromptTagBadgeViewModel> CreateTagBadges(
        Prompt prompt)
    {
        return prompt.PromptTags
            .Where(promptTag =>
                promptTag.Tag.IsActive)
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

    private static bool HasConstructor(
        Prompt prompt)
    {
        return prompt.Versions.Any(version =>
            version.IsCurrent &&
            !string.IsNullOrWhiteSpace(version.TemplateText) &&
            version.Variables.Count > 0);
    }


    private static string GetAuthorName(
        ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        return "Автор";
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

            _ =>
                "Не указано"
        };
    }
}
