using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Navigation;

namespace PromptHub.Web.ViewComponents;

public sealed class CatalogNavigationViewComponent : ViewComponent
{
    private const int PopularTagLimit = 10;

    private readonly ApplicationDbContext _dbContext;

    public CatalogNavigationViewComponent(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var cancellationToken =
            HttpContext.RequestAborted;

        var categories =
            await _dbContext.Categories
                .AsNoTracking()
                .Where(category =>
                    category.IsActive &&
                    category.Prompts.Any(prompt =>
                        prompt.Status ==
                            PromptStatus.Published))
                .OrderBy(category =>
                    category.SortOrder)
                .ThenBy(category =>
                    category.Name)
                .Select(category =>
                    new CatalogNavigationItemViewModel
                    {
                        Name = category.Name,
                        Slug = category.Slug,
                        PromptCount =
                            category.Prompts.Count(prompt =>
                                prompt.Status ==
                                    PromptStatus.Published)
                    })
                .ToListAsync(
                    cancellationToken);

        var popularTags =
            await _dbContext.Tags
                .AsNoTracking()
                .Where(tag =>
                    tag.IsActive)
                .Select(tag =>
                    new CatalogNavigationItemViewModel
                    {
                        Name = tag.Name,
                        Slug = tag.Slug,
                        PromptCount =
                            tag.PromptTags.Count(promptTag =>
                                promptTag.Prompt.Status ==
                                    PromptStatus.Published)
                    })
                .Where(tag =>
                    tag.PromptCount > 0)
                .OrderByDescending(tag =>
                    tag.PromptCount)
                .ThenBy(tag =>
                    tag.Name)
                .Take(
                    PopularTagLimit)
                .ToListAsync(
                    cancellationToken);

        return View(
            new CatalogNavigationViewModel
            {
                Categories = categories,
                PopularTags = popularTags
            });
    }
}
