using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.AuthorSupport;

namespace PromptHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class AuthorSupportModel
    : PageModel
{

    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly ApplicationDbContext
        _dbContext;

    public AuthorSupportModel(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _userManager =
            userManager;

        _dbContext =
            dbContext;
    }



    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }
        = new();



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

        var methods =
            await _dbContext
                .AuthorSupportMethods
                .AsNoTracking()
                .Where(method =>
                    method.UserId == user.Id)
                .ToListAsync(
                    cancellationToken);

        var cloudTips =
            methods.SingleOrDefault(method =>
                method.Provider ==
                AuthorSupportProvider.CloudTips);

        var yooMoney =
            methods.SingleOrDefault(method =>
                method.Provider ==
                AuthorSupportProvider.YooMoney);

        if (cloudTips is not null)
        {
            Input.CloudTipsUrl =
                AuthorSupportReferenceParser
                    .BuildCloudTipsUrl(
                        cloudTips.ExternalReference);

            Input.CloudTipsEnabled =
                cloudTips.IsEnabled;
        }

        if (yooMoney is not null)
        {
            Input.YooMoneyWallet =
                yooMoney.ExternalReference;

            Input.YooMoneyEnabled =
                yooMoney.IsEnabled;
        }

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

        var hasCloudTips =
            !string.IsNullOrWhiteSpace(
                Input.CloudTipsUrl);

        var hasYooMoney =
            !string.IsNullOrWhiteSpace(
                Input.YooMoneyWallet);

        var cloudTipsLayoutId =
            string.Empty;

        var yooMoneyWallet =
            string.Empty;

        if (hasCloudTips &&
            !AuthorSupportReferenceParser
                .TryNormalizeCloudTips(
                    Input.CloudTipsUrl,
                    out cloudTipsLayoutId,
                    out var cloudTipsError))
        {
            ModelState.AddModelError(
                "Input.CloudTipsUrl",
                cloudTipsError);
        }

        if (hasYooMoney &&
            !AuthorSupportReferenceParser
                .TryNormalizeYooMoney(
                    Input.YooMoneyWallet,
                    out yooMoneyWallet,
                    out var yooMoneyError))
        {
            ModelState.AddModelError(
                "Input.YooMoneyWallet",
                yooMoneyError);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var methods =
            await _dbContext
                .AuthorSupportMethods
                .Where(method =>
                    method.UserId == user.Id)
                .ToListAsync(
                    cancellationToken);

        ApplyMethod(
            methods,
            user.Id,
            AuthorSupportProvider.CloudTips,
            hasCloudTips
                ? cloudTipsLayoutId
                : null,
            hasCloudTips &&
            Input.CloudTipsEnabled);

        ApplyMethod(
            methods,
            user.Id,
            AuthorSupportProvider.YooMoney,
            hasYooMoney
                ? yooMoneyWallet
                : null,
            hasYooMoney &&
            Input.YooMoneyEnabled);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        StatusMessage =
            "Настройки поддержки сохранены.";

        return RedirectToPage();
    }



    private void ApplyMethod(
        IReadOnlyCollection<AuthorSupportMethod> methods,
        string userId,
        AuthorSupportProvider provider,
        string? externalReference,
        bool isEnabled)
    {
        var existing =
            methods.SingleOrDefault(method =>
                method.Provider ==
                provider);

        if (string.IsNullOrWhiteSpace(
                externalReference))
        {
            if (existing is not null)
            {
                _dbContext
                    .AuthorSupportMethods
                    .Remove(existing);
            }

            return;
        }

        var now =
            DateTime.UtcNow;

        if (existing is null)
        {
            _dbContext
                .AuthorSupportMethods
                .Add(
                    new AuthorSupportMethod
                    {
                        UserId =
                            userId,

                        Provider =
                            provider,

                        ExternalReference =
                            externalReference,

                        IsEnabled =
                            isEnabled,

                        CreatedAtUtc =
                            now,

                        UpdatedAtUtc =
                            now
                    });

            return;
        }

        existing.ExternalReference =
            externalReference;

        existing.IsEnabled =
            isEnabled;

        existing.UpdatedAtUtc =
            now;
    }



    public sealed class InputModel
    {
        [Display(
            Name = "Ссылка CloudTips")]
        [MaxLength(300)]
        public string? CloudTipsUrl { get; set; }

        public bool CloudTipsEnabled { get; set; }

        [Display(
            Name = "Номер кошелька ЮMoney")]
        [MaxLength(32)]
        public string? YooMoneyWallet { get; set; }

        public bool YooMoneyEnabled { get; set; }
    }
}
