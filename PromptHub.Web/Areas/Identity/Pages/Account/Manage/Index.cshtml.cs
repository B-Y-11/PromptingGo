using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.MediaStorage;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class IndexModel : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly SignInManager<ApplicationUser>
        _signInManager;

    private readonly ApplicationDbContext
        _dbContext;

    private readonly IMediaStorageService
        _mediaStorageService;

    private readonly ILogger<IndexModel>
        _logger;

    public bool EmailConfirmed { get; private set; }

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext dbContext,
        IMediaStorageService mediaStorageService,
        ILogger<IndexModel> logger)
    {
        _userManager =
            userManager;

        _signInManager =
            signInManager;

        _dbContext =
            dbContext;

        _mediaStorageService =
            mediaStorageService;

        _logger =
            logger;
    }


    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }
        = new();

    [BindProperty]
    [Display(
        Name = "Файл аватара")]
    public IFormFile? AvatarFile { get; set; }

    public string Email { get; private set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public string? AvatarContentUrl { get; private set; }

    public bool HasStoredAvatar { get; private set; }

    public int TotalPromptsCount { get; private set; }

    public int DraftPromptsCount { get; private set; }

    public int PendingPromptsCount { get; private set; }

    public int PublishedPromptsCount { get; private set; }


    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        LoadInputFromUser(
            user);

        await LoadPageDataAsync(
            user,
            cancellationToken);

        return Page();
    }


    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        await LoadPageDataAsync(
            user,
            cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        user.DisplayName =
            Input.DisplayName.Trim();

        user.Bio =
            Input.Bio?.Trim()
            ?? string.Empty;

        user.UpdatedAtUtc =
            DateTime.UtcNow;

        var result =
            await _userManager.UpdateAsync(
                user);

        if (!result.Succeeded)
        {
            AddIdentityErrors(
                result);

            return Page();
        }

        await _signInManager.RefreshSignInAsync(
            user);

        StatusMessage =
            "Профиль успешно обновлён.";

        return RedirectToPage();
    }


    public async Task<IActionResult> OnPostUploadAvatarAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        LoadInputFromUser(
            user);

        await LoadPageDataAsync(
            user,
            cancellationToken);

        if (AvatarFile is null ||
            AvatarFile.Length <= 0)
        {
            ModelState.AddModelError(
                nameof(AvatarFile),
                "Выберите изображение для загрузки.");

            return Page();
        }

        StoredProfileAvatarFile storedAvatar;

        try
        {
            storedAvatar =
                await _mediaStorageService
                    .SaveProfileAvatarAsync(
                        AvatarFile,
                        cancellationToken);
        }
        catch (MediaStorageException exception)
        {
            ModelState.AddModelError(
                nameof(AvatarFile),
                exception.Message);

            return Page();
        }

        var previousStorageKey =
            user.AvatarStorageKey;

        var now =
            DateTime.UtcNow;

        user.AvatarStorageKey =
            storedAvatar.StorageKey;

        user.AvatarContentType =
            storedAvatar.ContentType;

        user.AvatarFileSizeBytes =
            storedAvatar.FileSizeBytes;

        user.AvatarWidth =
            storedAvatar.Width;

        user.AvatarHeight =
            storedAvatar.Height;

        user.AvatarUpdatedAtUtc =
            now;

        user.UpdatedAtUtc =
            now;

        var result =
            await _userManager.UpdateAsync(
                user);

        if (!result.Succeeded)
        {
            await DeleteAvatarQuietlyAsync(
                storedAvatar.StorageKey);

            AddIdentityErrors(
                result);

            await LoadPageDataAsync(
                user,
                cancellationToken);

            return Page();
        }

        if (!string.IsNullOrWhiteSpace(
                previousStorageKey) &&
            !string.Equals(
                previousStorageKey,
                storedAvatar.StorageKey,
                StringComparison.Ordinal))
        {
            await DeleteAvatarQuietlyAsync(
                previousStorageKey);
        }

        await _signInManager.RefreshSignInAsync(
            user);

        StatusMessage =
            "Изображение профиля обновлено.";

        return RedirectToPage();
    }


    public async Task<IActionResult> OnPostDeleteAvatarAsync(
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.GetUserAsync(
                User);

        if (user is null)
        {
            return NotFound(
                "Не удалось загрузить аккаунт.");
        }

        var previousStorageKey =
            user.AvatarStorageKey;

        if (string.IsNullOrWhiteSpace(
                previousStorageKey))
        {
            StatusMessage =
                "Изображение профиля уже удалено.";

            return RedirectToPage();
        }

        var now =
            DateTime.UtcNow;

        user.AvatarStorageKey =
            null;

        user.AvatarContentType =
            null;

        user.AvatarFileSizeBytes =
            null;

        user.AvatarWidth =
            null;

        user.AvatarHeight =
            null;

        user.AvatarUpdatedAtUtc =
            null;


        user.UpdatedAtUtc =
            now;

        var result =
            await _userManager.UpdateAsync(
                user);

        if (!result.Succeeded)
        {
            LoadInputFromUser(
                user);

            await LoadPageDataAsync(
                user,
                cancellationToken);

            AddIdentityErrors(
                result);

            return Page();
        }

        await DeleteAvatarQuietlyAsync(
            previousStorageKey);

        await _signInManager.RefreshSignInAsync(
            user);

        StatusMessage =
            "Изображение профиля удалено.";

        return RedirectToPage();
    }


    private void LoadInputFromUser(
        ApplicationUser user)
    {
        Input =
            new InputModel
            {
                DisplayName =
                    user.DisplayName,

                Bio =
                    user.Bio
            };
    }

    private async Task LoadPageDataAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        Email =
            await _userManager.GetEmailAsync(
                user)
            ?? user.Email
            ?? string.Empty;

        EmailConfirmed =
    !string.IsNullOrWhiteSpace(
        Email) &&
    await _userManager.IsEmailConfirmedAsync(
        user);

        CreatedAtUtc =
            user.CreatedAtUtc;

        HasStoredAvatar =
            !string.IsNullOrWhiteSpace(
                user.AvatarStorageKey) &&
            user.AvatarUpdatedAtUtc.HasValue;

        AvatarContentUrl =
            HasStoredAvatar
                ? Url.RouteUrl(
                    "UserAvatarContent",
                    new
                    {
                        userId =
                            user.Id,

                        version =
                            user.AvatarUpdatedAtUtc!
                                .Value
                                .Ticks
                    })
                : null;

        var promptsQuery =
            _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    prompt.AuthorId ==
                    user.Id);

        TotalPromptsCount =
            await promptsQuery.CountAsync(
                cancellationToken);

        DraftPromptsCount =
            await promptsQuery.CountAsync(
                prompt =>
                    prompt.Status ==
                        PromptStatus.Draft ||
                    prompt.Status ==
                        PromptStatus.Rejected,
                cancellationToken);

        PendingPromptsCount =
            await promptsQuery.CountAsync(
                prompt =>
                    prompt.Status ==
                        PromptStatus.PendingModeration,
                cancellationToken);

        PublishedPromptsCount =
            await promptsQuery.CountAsync(
                prompt =>
                    prompt.Status ==
                        PromptStatus.Published,
                cancellationToken);
    }

    // -------------------------------------------------------------------------
    // EN: Safe physical avatar cleanup
    // RU: Безопасная физическая очистка аватара
    // -------------------------------------------------------------------------

    private async Task DeleteAvatarQuietlyAsync(
        string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(
                storageKey))
        {
            return;
        }

        try
        {
            await _mediaStorageService
                .DeleteProfileAvatarAsync(
                    storageKey,
                    CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to delete profile avatar {StorageKey}.",
                storageKey);
        }
    }

    private void AddIdentityErrors(
        IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(
                string.Empty,
                error.Description);
        }
    }


    public sealed class InputModel
    {
        [Required(
            ErrorMessage =
                "Введите имя на сайте или псевдоним.")]
        [StringLength(
            80,
            MinimumLength = 2,
            ErrorMessage =
                "Имя должно содержать от 2 до 80 символов.")]
        [Display(
            Name = "Имя на сайте / псевдоним")]
        public string DisplayName { get; set; }
            = string.Empty;

        [StringLength(
            500,
            ErrorMessage =
                "Описание не должно превышать 500 символов.")]
        [Display(
            Name = "О себе")]
        public string? Bio { get; set; }
    }
}
