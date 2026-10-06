using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromptHub.Web.Services.Privacy;

namespace PromptHub.Web.Controllers;

[AllowAnonymous]
[Route("legal")]
public sealed class LegalController : Controller
{

    private readonly ICookieConsentService
    _cookieConsentService;

    public LegalController(
        ICookieConsentService cookieConsentService)
    {
        _cookieConsentService =
            cookieConsentService;
    }

    [HttpGet("terms", Name = "LegalTerms")]
    public IActionResult Terms()
    {
        return View();
    }

    [HttpGet("privacy", Name = "LegalPrivacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("content-rules", Name = "LegalContentRules")]
    public IActionResult ContentRules()
    {
        return View();
    }

    [HttpGet(
        "personal-data-consent",
        Name = "LegalPersonalDataConsent")]
    public IActionResult PersonalDataConsent()
    {
        return View();
    }


    [HttpGet(
    "cookies",
    Name = "LegalCookies")]
    public IActionResult Cookies()
    {
        return View();
    }

    [HttpPost(
        "cookies/preference",
        Name = "LegalSetCookiePreference")]
    [ValidateAntiForgeryToken]
    public IActionResult SetCookiePreference(
        string preference,
        string? returnUrl)
    {
        var level =
            preference switch
            {
                "necessary" =>
                    CookieConsentLevel.NecessaryOnly,

                "analytics" =>
                    CookieConsentLevel.Analytics,

                _ =>
                    CookieConsentLevel.Unset
            };

        if (level ==
            CookieConsentLevel.Unset)
        {
            return BadRequest();
        }

        _cookieConsentService.SetLevel(
            HttpContext,
            level);

        if (!string.IsNullOrWhiteSpace(
                returnUrl) &&
            Url.IsLocalUrl(
                returnUrl))
        {
            return LocalRedirect(
                returnUrl);
        }

        return RedirectToRoute(
            "LegalCookies");
    }

}
