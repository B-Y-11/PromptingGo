using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class ResetPasswordConfirmationModel
    : PageModel
{
    public void OnGet()
    {
    }
}
