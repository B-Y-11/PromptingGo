using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using PromptHub.Web.Services.Privacy;

namespace PromptHub.Web.Services.PromptStats;

public interface IPromptMetricActorKeyProvider
{
    string? GetActorKeyHash(
        HttpContext httpContext);
}

public sealed class PromptMetricActorKeyProvider
    : IPromptMetricActorKeyProvider
{

    private static readonly TimeSpan
        VisitorCookieLifetime =
            TimeSpan.FromDays(
                180);

    private readonly ICookieConsentService
    _cookieConsentService;

    public PromptMetricActorKeyProvider(
    ICookieConsentService cookieConsentService)
    {
        _cookieConsentService =
            cookieConsentService;
    }

    public string? GetActorKeyHash(
    HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        // ---------------------------------------------------------------------
        // EN: Authenticated users are deduplicated by their account ID.
        //     No analytics cookie is required for this.
        // RU: Авторизованные пользователи дедуплицируются по ID аккаунта.
        //     Статистический cookie для этого не требуется.
        // ---------------------------------------------------------------------

        var userId =
            httpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(
                userId))
        {
            return Hash(
                $"user:{userId}");
        }

        // ---------------------------------------------------------------------
        // EN: Anonymous analytics requires explicit cookie consent.
        // RU: Для статистики анонимного пользователя требуется согласие
        //     на статистические cookie.
        // ---------------------------------------------------------------------

        if (!_cookieConsentService.AllowsAnalytics(
                httpContext))
        {
            if (httpContext.Request.Cookies.ContainsKey(
                    CookieConsentService
                        .MetricVisitorCookieName))
            {
                httpContext.Response.Cookies.Delete(
                    CookieConsentService
                        .MetricVisitorCookieName,
                    new CookieOptions
                    {
                        Secure =
                            true,

                        SameSite =
                            SameSiteMode.Lax,

                        Path =
                            "/"
                    });
            }

            return null;
        }

        if (!httpContext.Request.Cookies.TryGetValue(
                CookieConsentService
                    .MetricVisitorCookieName,
                out var visitorKey) ||
            !IsValidVisitorKey(
                visitorKey))
        {
            visitorKey =
                WebEncoders.Base64UrlEncode(
                    RandomNumberGenerator.GetBytes(
                        32));

            httpContext.Response.Cookies.Append(
                CookieConsentService
                    .MetricVisitorCookieName,
                visitorKey,
                new CookieOptions
                {
                    HttpOnly =
                        true,

                    Secure =
                        true,

                    SameSite =
                        SameSiteMode.Lax,

                    Path =
                        "/",

                    MaxAge =
                        VisitorCookieLifetime,

                    IsEssential =
                        false
                });
        }

        return Hash(
            $"anonymous:{visitorKey}");
    }

    private static bool IsValidVisitorKey(
        string? visitorKey)
    {
        return
            !string.IsNullOrWhiteSpace(
                visitorKey) &&
            visitorKey.Length is >= 32 and <= 128;
    }

    private static string Hash(
        string value)
    {
        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    value));

        return Convert.ToHexString(
                bytes);
    }
}
