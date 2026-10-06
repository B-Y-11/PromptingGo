using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Navigation;

namespace PromptHub.Web.ViewComponents;

public sealed class CatalogToolbarViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _dbContext;

    public CatalogToolbarViewComponent(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var cancellationToken =
            HttpContext.RequestAborted;

        var controller =
            ViewContext.RouteData.Values["controller"]
                ?.ToString();

        var action =
            ViewContext.RouteData.Values["action"]
                ?.ToString();

        var isCatalogRequest =
            string.Equals(
                controller,
                "Prompts",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                action,
                "Index",
                StringComparison.OrdinalIgnoreCase);

        string CurrentValue(
            string name)
        {
            if (!isCatalogRequest)
            {
                return string.Empty;
            }

            var routeValue =
                ViewContext.RouteData.Values[name]
                    ?.ToString();

            if (!string.IsNullOrWhiteSpace(routeValue))
            {
                return routeValue.Trim();
            }

            return HttpContext.Request.Query[name]
                .ToString()
                .Trim();
        }

        var search =
            isCatalogRequest
                ? HttpContext.Request.Query["search"]
                    .ToString()
                    .Trim()
                : string.Empty;

        var category =
            CurrentValue("category");

        var difficulty =
            CurrentValue("difficulty")
                .ToLowerInvariant();

        var tag =
            CurrentValue("tag");

        var requestedSort =
            isCatalogRequest
                ? HttpContext.Request.Query["sort"]
                    .ToString()
                    .Trim()
                    .ToLowerInvariant()
                : string.Empty;

        var defaultSort =
            string.IsNullOrWhiteSpace(search)
                ? "newest"
                : "relevance";

        var sort =
            string.IsNullOrWhiteSpace(requestedSort)
                ? defaultSort
                : requestedSort;

        var categories =
            await _dbContext.Categories
                .AsNoTracking()
                .Where(categoryItem =>
                    categoryItem.IsActive &&
                    categoryItem.Prompts.Any(prompt =>
                        prompt.Status ==
                            PromptStatus.Published))
                .OrderBy(categoryItem =>
                    categoryItem.SortOrder)
                .ThenBy(categoryItem =>
                    categoryItem.Name)
                .Select(categoryItem =>
                    new CatalogToolbarCategoryViewModel
                    {
                        Name = categoryItem.Name,
                        Slug = categoryItem.Slug
                    })
                .ToListAsync(
                    cancellationToken);

        var tagName =
            string.Empty;

        if (!string.IsNullOrWhiteSpace(tag))
        {
            tagName =
                await _dbContext.Tags
                    .AsNoTracking()
                    .Where(tagItem =>
                        tagItem.IsActive &&
                        tagItem.Slug == tag)
                    .Select(tagItem =>
                        tagItem.Name)
                    .FirstOrDefaultAsync(
                        cancellationToken)
                ?? tag;
        }

        var activeFilterCount =
            0;

        if (!string.IsNullOrWhiteSpace(category))
        {
            activeFilterCount++;
        }

        if (!string.IsNullOrWhiteSpace(difficulty))
        {
            activeFilterCount++;
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            activeFilterCount++;
        }

        if (!string.Equals(
                sort,
                defaultSort,
                StringComparison.OrdinalIgnoreCase))
        {
            activeFilterCount++;
        }

        return View(
            new CatalogToolbarViewModel
            {
                Categories = categories,
                Search = search,
                Category = category,
                Difficulty = difficulty,
                Tag = tag,
                TagName = tagName,
                Sort = sort,
                DefaultSort = defaultSort,
                ActiveFilterCount = activeFilterCount
            });
    }
}
