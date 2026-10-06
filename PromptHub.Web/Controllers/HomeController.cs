using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Home;
using PromptHub.Web.Models.ViewModels.Prompts;
using PromptHub.Web.Services.PromptStats;
using PromptHub.Web.Services.PromptMedia;
using System.Diagnostics;
using PromptHub.Web.Models;

namespace PromptHub.Web.Controllers;

public sealed class HomeController : Controller
{

    private readonly ApplicationDbContext _dbContext;

    private readonly IPromptStatsService _promptStatsService;

    private readonly IPromptCardCoverService
    _promptCardCoverService;

    public HomeController(
        ApplicationDbContext dbContext,
        IPromptStatsService promptStatsService,
        IPromptCardCoverService promptCardCoverService)
    {
        _dbContext = dbContext;
        _promptStatsService = promptStatsService;
        _promptCardCoverService = promptCardCoverService;
    }


    [ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id
                ?? HttpContext.TraceIdentifier
        });
    }


    [HttpGet("/")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var topPrompts =
            await LoadTopPromptsAsync(
                cancellationToken);

        var promptIds = topPrompts
            .Select(prompt => prompt.Id)
            .ToArray();

        var promptCovers =
            await _promptCardCoverService
                .GetPublishedCoversAsync(
                    promptIds,
                    cancellationToken);

        var visibleCommentCounts =
            await _promptStatsService.GetVisibleCommentCountsAsync(
                promptIds,
                cancellationToken);

        var currentUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var favoritePromptIds =
            new HashSet<int>();

        if (!string.IsNullOrWhiteSpace(
                currentUserId) &&
            promptIds.Length > 0)
        {
            favoritePromptIds =
                (await _dbContext.PromptFavorites
                    .AsNoTracking()
                    .Where(favorite =>
                        favorite.UserId == currentUserId &&
                        promptIds.Contains(
                            favorite.PromptId))
                    .Select(favorite =>
                        favorite.PromptId)
                    .ToListAsync(
                        cancellationToken))
                .ToHashSet();
        }



        var model =
            new HomeIndexViewModel
            {
                TopPrompts = topPrompts
                    .Select(prompt =>
                        MapHomePromptCard(
                            prompt,
                            favoritePromptIds,
                            visibleCommentCounts,
                            promptCovers))
                    .ToArray()
            };

        return View(model);
    }


    private async Task<List<Prompt>> LoadTopPromptsAsync(
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
                prompt.Ratings)
            .Where(prompt =>
                prompt.Status == PromptStatus.Published &&
                prompt.Category.IsActive)
            .OrderByDescending(prompt =>
                prompt.Ratings.Any()
                    ? prompt.Ratings.Average(rating =>
                        (double)rating.Score)
                    : 0)
            .ThenByDescending(prompt =>
                prompt.Ratings.Count)
            .ThenByDescending(prompt =>
                prompt.ViewCount)
            .ThenByDescending(prompt =>
                prompt.PublishedAtUtc)
            .Take(3)
            .ToListAsync(
                cancellationToken);
    }



    private static HomePromptCardViewModel MapHomePromptCard(
        Prompt prompt,
        IReadOnlySet<int> favoritePromptIds,
        IReadOnlyDictionary<int, int> visibleCommentCounts,
        IReadOnlyDictionary<int, PromptCardCoverViewModel> promptCovers)
    {
        return new HomePromptCardViewModel
        {
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
                prompt.AuthorId,

            AuthorName =
                GetAuthorName(
                    prompt.Author),

            HasConstructor =
                HasCurrentConstructor(prompt),

            IsFavorite =
                favoritePromptIds.Contains(
                    prompt.Id),

            AverageRating =
                prompt.Ratings.Count > 0
                    ? prompt.Ratings.Average(rating =>
                        rating.Score)
                    : 0,

            RatingCount =
                prompt.Ratings.Count,

            ViewCount =
                prompt.ViewCount,

            VisibleCommentCount =
                visibleCommentCounts.GetValueOrDefault(prompt.Id),

            Cover =
                promptCovers.GetValueOrDefault(
                    prompt.Id),

            CopyCount =
                prompt.CopyCount
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

    private static bool HasCurrentConstructor(
        Prompt prompt)
    {
        return prompt.Versions.Any(version =>
            version.IsCurrent &&
            !string.IsNullOrWhiteSpace(
                version.TemplateText) &&
            version.Variables.Count > 0);
    }


    private static string GetAuthorName(
        ApplicationUser? user)
    {
        if (user is null)
        {
            return "Неизвестный автор";
        }

        if (!string.IsNullOrWhiteSpace(
            user.DisplayName))
        {
            return user.DisplayName;
        }

        return "Автор";
    }
}
