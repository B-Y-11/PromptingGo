using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PromptHub.Web.Controllers;

[AllowAnonymous]
public sealed class HelpController : Controller
{

    [HttpGet("/help", Name = "HelpIndex")]
    public IActionResult Index()
    {
        return View();
    }
}
