using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.ViewModels.Admin;
using PromptHub.Web.Security;
using PromptHub.Web.Services.Slugs;

namespace PromptHub.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Administrator)]
[Route("admin")]
public sealed class AdminController : Controller
{

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ISlugGenerator _slugGenerator;

    public AdminController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ISlugGenerator slugGenerator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _slugGenerator = slugGenerator;
    }


    [HttpGet("users")]
    public async Task<IActionResult> Users(
        CancellationToken cancellationToken)
    {
        var users =
            await _dbContext.Users
                .AsNoTracking()
                .OrderByDescending(user =>
                    user.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        var userIds =
            users
                .Select(user => user.Id)
                .ToArray();

        var promptStats =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    userIds.Contains(
                        prompt.AuthorId))
                .GroupBy(prompt =>
                    prompt.AuthorId)
                .Select(group => new UserPromptStats
                {
                    UserId = group.Key,

                    TotalPromptsCount =
                        group.Count(),

                    DraftPromptsCount =
                        group.Count(prompt =>
                            prompt.Status == PromptStatus.Draft ||
                            prompt.Status == PromptStatus.Rejected),

                    PendingPromptsCount =
                        group.Count(prompt =>
                            prompt.Status ==
                            PromptStatus.PendingModeration),

                    PublishedPromptsCount =
                        group.Count(prompt =>
                            prompt.Status ==
                            PromptStatus.Published),

                    ArchivedPromptsCount =
                        group.Count(prompt =>
                            prompt.Status ==
                            PromptStatus.Archived),

                    TotalViewCount =
                        group.Sum(prompt =>
                            prompt.ViewCount),

                    TotalCopyCount =
                        group.Sum(prompt =>
                            prompt.CopyCount)
                })
                .ToDictionaryAsync(
                    item => item.UserId,
                    item => item,
                    cancellationToken);

        var reportStats =
            await _dbContext.PromptReports
                .AsNoTracking()
                .Where(report =>
                    userIds.Contains(
                        report.ReporterId))
                .GroupBy(report =>
                    report.ReporterId)
                .Select(group => new UserReportStats
                {
                    UserId = group.Key,

                    SubmittedReportsCount =
                        group.Count(),

                    OpenReportsCount =
                        group.Count(report =>
                            report.Status ==
                            PromptReportStatus.New)
                })
                .ToDictionaryAsync(
                    item => item.UserId,
                    item => item,
                    cancellationToken);

        var currentUserId =
            _userManager.GetUserId(User);

        var items =
            new List<UserAdministrationItemViewModel>();

        foreach (var user in users)
        {
            var roles =
                await _userManager.GetRolesAsync(
                    user);

            promptStats.TryGetValue(
                user.Id,
                out var userPromptStats);

            reportStats.TryGetValue(
                user.Id,
                out var userReportStats);

            items.Add(
                new UserAdministrationItemViewModel
                {
                    Id = user.Id,

                    DisplayName =
                        GetDisplayName(user),

                    Email =
                        user.Email
                        ?? string.Empty,

                    CreatedAtUtc =
                        user.CreatedAtUtc,

                    IsCurrentUser =
                        user.Id == currentUserId,

                    IsAdministrator =
                        roles.Contains(
                            ApplicationRoles.Administrator),

                    IsModerator =
                        roles.Contains(
                            ApplicationRoles.Moderator),

                    TotalPromptsCount =
                        userPromptStats
                            ?.TotalPromptsCount
                        ?? 0,

                    DraftPromptsCount =
                        userPromptStats
                            ?.DraftPromptsCount
                        ?? 0,

                    PendingPromptsCount =
                        userPromptStats
                            ?.PendingPromptsCount
                        ?? 0,

                    PublishedPromptsCount =
                        userPromptStats
                            ?.PublishedPromptsCount
                        ?? 0,

                    ArchivedPromptsCount =
                        userPromptStats
                            ?.ArchivedPromptsCount
                        ?? 0,

                    TotalViewCount =
                        userPromptStats
                            ?.TotalViewCount
                        ?? 0,

                    TotalCopyCount =
                        userPromptStats
                            ?.TotalCopyCount
                        ?? 0,

                    SubmittedReportsCount =
                        userReportStats
                            ?.SubmittedReportsCount
                        ?? 0,

                    OpenReportsCount =
                        userReportStats
                            ?.OpenReportsCount
                        ?? 0
                });
        }

        var model =
            new UserAdministrationViewModel
            {
                Users = items
            };

        return View(model);
    }

    [HttpPost("users/{id}/make-moderator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeModerator(
        string id)
    {
        var user =
            await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            TempData["ErrorMessage"] =
                "Пользователь не найден.";

            return RedirectToAction(
                nameof(Users));
        }

        await EnsureRoleExistsAsync(
            ApplicationRoles.Moderator);

        if (await _userManager.IsInRoleAsync(
                user,
                ApplicationRoles.Moderator))
        {
            TempData["SuccessMessage"] =
                "Пользователь уже является модератором.";

            return RedirectToAction(
                nameof(Users));
        }

        var result =
            await _userManager.AddToRoleAsync(
                user,
                ApplicationRoles.Moderator);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                "Не удалось назначить роль модератора.";

            return RedirectToAction(
                nameof(Users));
        }

        TempData["SuccessMessage"] =
            "Пользователь назначен модератором.";

        return RedirectToAction(
            nameof(Users));
    }

    [HttpPost("users/{id}/remove-moderator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveModerator(
        string id)
    {
        var user =
            await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            TempData["ErrorMessage"] =
                "Пользователь не найден.";

            return RedirectToAction(
                nameof(Users));
        }

        if (!await _userManager.IsInRoleAsync(
                user,
                ApplicationRoles.Moderator))
        {
            TempData["SuccessMessage"] =
                "У пользователя нет роли модератора.";

            return RedirectToAction(
                nameof(Users));
        }

        var result =
            await _userManager.RemoveFromRoleAsync(
                user,
                ApplicationRoles.Moderator);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                "Не удалось снять роль модератора.";

            return RedirectToAction(
                nameof(Users));
        }

        TempData["SuccessMessage"] =
            "Роль модератора снята.";

        return RedirectToAction(
            nameof(Users));
    }


    [HttpGet("categories")]
    public async Task<IActionResult> Categories(
        CancellationToken cancellationToken)
    {
        var categories =
            await _dbContext.Categories
                .AsNoTracking()
                .Include(category => category.Parent)
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .ToListAsync(cancellationToken);

        var categoryIds =
            categories
                .Select(category => category.Id)
                .ToArray();

        var promptStats =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    categoryIds.Contains(prompt.CategoryId))
                .GroupBy(prompt => prompt.CategoryId)
                .Select(group => new CategoryPromptStats
                {
                    CategoryId = group.Key,

                    TotalPromptsCount =
                        group.Count(),

                    PublishedPromptsCount =
                        group.Count(prompt =>
                            prompt.Status == PromptStatus.Published),

                    ArchivedPromptsCount =
                        group.Count(prompt =>
                            prompt.Status == PromptStatus.Archived)
                })
                .ToDictionaryAsync(
                    item => item.CategoryId,
                    item => item,
                    cancellationToken);

        var model =
            new CategoryAdministrationViewModel
            {
                Categories =
                    categories
                        .Select(category =>
                        {
                            promptStats.TryGetValue(
                                category.Id,
                                out var stats);

                            return new CategoryAdministrationItemViewModel
                            {
                                Id = category.Id,
                                Name = category.Name,
                                Slug = category.Slug,
                                Description = category.Description,

                                ParentName =
                                    category.Parent?.Name
                                    ?? string.Empty,

                                SortOrder =
                                    category.SortOrder,

                                IsActive =
                                    category.IsActive,

                                CreatedAtUtc =
                                    category.CreatedAtUtc,

                                UpdatedAtUtc =
                                    category.UpdatedAtUtc,

                                TotalPromptsCount =
                                    stats?.TotalPromptsCount
                                    ?? 0,

                                PublishedPromptsCount =
                                    stats?.PublishedPromptsCount
                                    ?? 0,

                                ArchivedPromptsCount =
                                    stats?.ArchivedPromptsCount
                                    ?? 0
                            };
                        })
                        .ToArray()
            };

        return View(model);
    }

    [HttpGet("categories/create")]
    public async Task<IActionResult> CreateCategory(
        CancellationToken cancellationToken)
    {
        var model =
            await BuildCategoryEditorModelAsync(
                input: new AdminCategoryFormViewModel
                {
                    IsActive = true
                },
                isEditMode: false,
                categoryId: null,
                currentSlug: null,
                cancellationToken);

        return View(model);
    }

    [HttpPost("categories/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(
        [Bind(Prefix = "Input")] AdminCategoryFormViewModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel =
                await BuildCategoryEditorModelAsync(
                    input,
                    isEditMode: false,
                    categoryId: null,
                    currentSlug: null,
                    cancellationToken);

            return View(invalidModel);
        }

        var slug =
            await CreateUniqueCategorySlugAsync(
                input.Name,
                cancellationToken);

        var category =
            new Category
            {
                Name = input.Name.Trim(),
                Slug = slug,
                Description = input.Description.Trim(),
                ParentId = input.ParentId,
                SortOrder = input.SortOrder,
                IsActive = input.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            };

        _dbContext.Categories.Add(category);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Категория создана.";

        return RedirectToAction(
            nameof(Categories));
    }

    [HttpGet("categories/{id:int}/edit")]
    public async Task<IActionResult> EditCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var category =
            await _dbContext.Categories
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    categoryEntity =>
                        categoryEntity.Id == id,
                    cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        var model =
            await BuildCategoryEditorModelAsync(
                input: new AdminCategoryFormViewModel
                {
                    Name = category.Name,
                    Description = category.Description,
                    ParentId = category.ParentId,
                    SortOrder = category.SortOrder,
                    IsActive = category.IsActive
                },
                isEditMode: true,
                categoryId: category.Id,
                currentSlug: category.Slug,
                cancellationToken);

        return View(model);
    }

    [HttpPost("categories/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(
        int id,
        [Bind(Prefix = "Input")] AdminCategoryFormViewModel input,
        CancellationToken cancellationToken)
    {
        var category =
            await _dbContext.Categories
                .SingleOrDefaultAsync(
                    categoryEntity =>
                        categoryEntity.Id == id,
                    cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        if (input.ParentId == id)
        {
            ModelState.AddModelError(
                "Input.ParentId",
                "Категория не может быть родителем самой себя.");
        }

        if (input.ParentId.HasValue &&
            await IsDescendantCategoryAsync(
                candidateParentId: input.ParentId.Value,
                categoryId: id,
                cancellationToken))
        {
            ModelState.AddModelError(
                "Input.ParentId",
                "Нельзя выбрать дочернюю категорию как родительскую.");
        }

        if (!ModelState.IsValid)
        {
            var invalidModel =
                await BuildCategoryEditorModelAsync(
                    input,
                    isEditMode: true,
                    categoryId: id,
                    currentSlug: category.Slug,
                    cancellationToken);

            return View(invalidModel);
        }

        category.Name = input.Name.Trim();
        category.Description = input.Description.Trim();
        category.ParentId = input.ParentId;
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Категория обновлена.";

        return RedirectToAction(
            nameof(Categories));
    }

    [HttpPost("categories/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var category =
            await _dbContext.Categories
                .SingleOrDefaultAsync(
                    categoryEntity =>
                        categoryEntity.Id == id,
                    cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        category.IsActive =
            !category.IsActive;

        category.UpdatedAtUtc =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            category.IsActive
                ? "Категория снова активна."
                : "Категория скрыта.";

        return RedirectToAction(
            nameof(Categories));
    }


    [HttpGet("tags")]
    public async Task<IActionResult> Tags(
        CancellationToken cancellationToken)
    {
        var tags =
            await _dbContext.Tags
                .AsNoTracking()
                .OrderBy(tag =>
                    tag.SortOrder)
                .ThenBy(tag =>
                    tag.Name)
                .ToListAsync(
                    cancellationToken);

        var tagIds =
            tags
                .Select(tag =>
                    tag.Id)
                .ToArray();

        var promptStats =
            await _dbContext.PromptTags
                .AsNoTracking()
                .Where(promptTag =>
                    tagIds.Contains(
                        promptTag.TagId))
                .GroupBy(promptTag =>
                    promptTag.TagId)
                .Select(group =>
                    new TagPromptStats
                    {
                        TagId =
                            group.Key,

                        TotalPromptsCount =
                            group.Count(),

                        PublishedPromptsCount =
                            group.Count(promptTag =>
                                promptTag.Prompt.Status ==
                                PromptStatus.Published)
                    })
                .ToDictionaryAsync(
                    item => item.TagId,
                    item => item,
                    cancellationToken);

        var model =
            new TagAdministrationViewModel
            {
                Tags =
                    tags
                        .Select(tag =>
                        {
                            promptStats.TryGetValue(
                                tag.Id,
                                out var stats);

                            return new TagAdministrationItemViewModel
                            {
                                Id =
                                    tag.Id,

                                Name =
                                    tag.Name,

                                Slug =
                                    tag.Slug,

                                Description =
                                    tag.Description,

                                IsActive =
                                    tag.IsActive,

                                SortOrder =
                                    tag.SortOrder,

                                CreatedAtUtc =
                                    tag.CreatedAtUtc,

                                UpdatedAtUtc =
                                    tag.UpdatedAtUtc,

                                TotalPromptsCount =
                                    stats?.TotalPromptsCount
                                    ?? 0,

                                PublishedPromptsCount =
                                    stats?.PublishedPromptsCount
                                    ?? 0
                            };
                        })
                        .ToArray()
            };

        return View(model);
    }

    [HttpGet("tags/create")]
    public IActionResult CreateTag()
    {
        var model =
            BuildTagEditorModel(
                input: new AdminTagFormViewModel
                {
                    IsActive = true
                },
                isEditMode: false,
                tagId: null,
                currentSlug: null);

        return View(model);
    }

    [HttpPost("tags/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTag(
        [Bind(Prefix = "Input")] AdminTagFormViewModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel =
                BuildTagEditorModel(
                    input,
                    isEditMode: false,
                    tagId: null,
                    currentSlug: null);

            return View(invalidModel);
        }

        var slug =
            await CreateUniqueTagSlugAsync(
                input.Name,
                cancellationToken);

        var tag =
            new Tag
            {
                Name =
                    input.Name.Trim(),

                Slug =
                    slug,

                Description =
                    input.Description.Trim(),

                SortOrder =
                    input.SortOrder,

                IsActive =
                    input.IsActive,

                CreatedAtUtc =
                    DateTime.UtcNow
            };

        _dbContext.Tags.Add(tag);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Тег создан.";

        return RedirectToAction(
            nameof(Tags));
    }

    [HttpGet("tags/{id:int}/edit")]
    public async Task<IActionResult> EditTag(
        int id,
        CancellationToken cancellationToken)
    {
        var tag =
            await _dbContext.Tags
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    tagEntity =>
                        tagEntity.Id == id,
                    cancellationToken);

        if (tag is null)
        {
            return NotFound();
        }

        var model =
            BuildTagEditorModel(
                input: new AdminTagFormViewModel
                {
                    Name =
                        tag.Name,

                    Description =
                        tag.Description,

                    SortOrder =
                        tag.SortOrder,

                    IsActive =
                        tag.IsActive
                },
                isEditMode: true,
                tagId: tag.Id,
                currentSlug: tag.Slug);

        return View(model);
    }

    [HttpPost("tags/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTag(
        int id,
        [Bind(Prefix = "Input")] AdminTagFormViewModel input,
        CancellationToken cancellationToken)
    {
        var tag =
            await _dbContext.Tags
                .SingleOrDefaultAsync(
                    tagEntity =>
                        tagEntity.Id == id,
                    cancellationToken);

        if (tag is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            var invalidModel =
                BuildTagEditorModel(
                    input,
                    isEditMode: true,
                    tagId: id,
                    currentSlug: tag.Slug);

            return View(invalidModel);
        }

        tag.Name =
            input.Name.Trim();

        tag.Description =
            input.Description.Trim();

        tag.SortOrder =
            input.SortOrder;

        tag.IsActive =
            input.IsActive;

        tag.UpdatedAtUtc =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            "Тег обновлён.";

        return RedirectToAction(
            nameof(Tags));
    }

    [HttpPost("tags/{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTag(
        int id,
        CancellationToken cancellationToken)
    {
        var tag =
            await _dbContext.Tags
                .SingleOrDefaultAsync(
                    tagEntity =>
                        tagEntity.Id == id,
                    cancellationToken);

        if (tag is null)
        {
            return NotFound();
        }

        tag.IsActive =
            !tag.IsActive;

        tag.UpdatedAtUtc =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        TempData["SuccessMessage"] =
            tag.IsActive
                ? "Тег снова активен."
                : "Тег скрыт.";

        return RedirectToAction(
            nameof(Tags));
    }


    private async Task EnsureRoleExistsAsync(
        string roleName)
    {
        if (await _roleManager.RoleExistsAsync(
                roleName))
        {
            return;
        }

        await _roleManager.CreateAsync(
            new IdentityRole(roleName));
    }


    private async Task<AdminCategoryEditorViewModel>
        BuildCategoryEditorModelAsync(
            AdminCategoryFormViewModel input,
            bool isEditMode,
            int? categoryId,
            string? currentSlug,
            CancellationToken cancellationToken)
    {
        return new AdminCategoryEditorViewModel
        {
            CategoryId = categoryId,
            IsEditMode = isEditMode,

            PageTitle =
                isEditMode
                    ? "Редактировать категорию"
                    : "Создать категорию",

            SubmitButtonText =
                isEditMode
                    ? "Сохранить изменения"
                    : "Создать категорию",

            CurrentSlug =
                currentSlug,

            Input =
                input,

            ParentCategoryOptions =
                await LoadParentCategoryOptionsAsync(
                    categoryId,
                    cancellationToken)
        };
    }

    private async Task<IReadOnlyList<SelectListItem>>
        LoadParentCategoryOptionsAsync(
            int? excludedCategoryId,
            CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                !excludedCategoryId.HasValue ||
                category.Id != excludedCategoryId.Value)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category =>
                new SelectListItem
                {
                    Value = category.Id.ToString(),

                    Text =
                        category.IsActive
                            ? category.Name
                            : category.Name + " · скрыта"
                })
            .ToListAsync(cancellationToken);
    }

    private async Task<string> CreateUniqueCategorySlugAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var baseSlug =
            _slugGenerator.Generate(name);

        if (baseSlug.Length > 110)
        {
            baseSlug =
                baseSlug[..110].TrimEnd('-');
        }

        var slug =
            baseSlug;

        var counter = 2;

        while (await _dbContext.Categories
                   .AsNoTracking()
                   .AnyAsync(
                       category =>
                           category.Slug == slug,
                       cancellationToken))
        {
            var suffix =
                "-" + counter;

            var maxBaseLength =
                120 - suffix.Length;

            var shortenedBase =
                baseSlug.Length > maxBaseLength
                    ? baseSlug[..maxBaseLength].TrimEnd('-')
                    : baseSlug;

            slug =
                shortenedBase + suffix;

            counter++;
        }

        return slug;
    }

    private async Task<bool> IsDescendantCategoryAsync(
        int candidateParentId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        var currentParentId =
            await _dbContext.Categories
                .AsNoTracking()
                .Where(category =>
                    category.Id == candidateParentId)
                .Select(category =>
                    category.ParentId)
                .SingleOrDefaultAsync(
                    cancellationToken);

        var safetyCounter = 0;

        while (currentParentId.HasValue &&
               safetyCounter < 50)
        {
            if (currentParentId.Value == categoryId)
            {
                return true;
            }

            currentParentId =
                await _dbContext.Categories
                    .AsNoTracking()
                    .Where(category =>
                        category.Id == currentParentId.Value)
                    .Select(category =>
                        category.ParentId)
                    .SingleOrDefaultAsync(
                        cancellationToken);

            safetyCounter++;
        }

        return false;
    }

    private sealed class CategoryPromptStats
    {
        public int CategoryId { get; set; }

        public int TotalPromptsCount { get; set; }

        public int PublishedPromptsCount { get; set; }

        public int ArchivedPromptsCount { get; set; }
    }


    private static AdminTagEditorViewModel BuildTagEditorModel(
        AdminTagFormViewModel input,
        bool isEditMode,
        int? tagId,
        string? currentSlug)
    {
        return new AdminTagEditorViewModel
        {
            TagId =
                tagId,

            IsEditMode =
                isEditMode,

            PageTitle =
                isEditMode
                    ? "Редактировать тег"
                    : "Создать тег",

            SubmitButtonText =
                isEditMode
                    ? "Сохранить изменения"
                    : "Создать тег",

            CurrentSlug =
                currentSlug,

            Input =
                input
        };
    }

    private async Task<string> CreateUniqueTagSlugAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var baseSlug =
            _slugGenerator.Generate(name);

        if (baseSlug.Length > 110)
        {
            baseSlug =
                baseSlug[..110].TrimEnd('-');
        }

        var slug =
            baseSlug;

        var counter = 2;

        while (await _dbContext.Tags
                   .AsNoTracking()
                   .AnyAsync(
                       tag =>
                           tag.Slug == slug,
                       cancellationToken))
        {
            var suffix =
                "-" + counter;

            var maxBaseLength =
                120 - suffix.Length;

            var shortenedBase =
                baseSlug.Length > maxBaseLength
                    ? baseSlug[..maxBaseLength].TrimEnd('-')
                    : baseSlug;

            slug =
                shortenedBase + suffix;

            counter++;
        }

        return slug;
    }

    private sealed class TagPromptStats
    {
        public int TagId { get; set; }

        public int TotalPromptsCount { get; set; }

        public int PublishedPromptsCount { get; set; }
    }


    private static string GetDisplayName(
        ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(
            user.DisplayName))
        {
            return user.DisplayName;
        }

        return user.UserName
            ?? user.Email
            ?? "Пользователь";
    }

    private sealed class UserPromptStats
    {
        public string UserId { get; set; }
            = string.Empty;

        public int TotalPromptsCount { get; set; }

        public int DraftPromptsCount { get; set; }

        public int PendingPromptsCount { get; set; }

        public int PublishedPromptsCount { get; set; }

        public int ArchivedPromptsCount { get; set; }

        public long TotalViewCount { get; set; }

        public long TotalCopyCount { get; set; }
    }

    private sealed class UserReportStats
    {
        public string UserId { get; set; }
            = string.Empty;

        public int SubmittedReportsCount { get; set; }

        public int OpenReportsCount { get; set; }
    }
}
