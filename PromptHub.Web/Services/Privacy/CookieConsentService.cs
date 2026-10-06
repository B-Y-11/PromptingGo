namespace PromptHub.Web.Services.Privacy;

public enum CookieConsentLevel
{
    Unset = 0,
    NecessaryOnly = 1,
    Analytics = 2
}

public interface ICookieConsentService
{
    CookieConsentLevel GetLevel(
        HttpContext httpContext);

    bool HasChoice(
        HttpContext httpContext);

    bool AllowsAnalytics(
        HttpContext httpContext);

    void SetLevel(
        HttpContext httpContext,
        CookieConsentLevel level);
}

public sealed class CookieConsentService
    : ICookieConsentService
{
    public const string ConsentCookieName =
        "__Host-PromptingGoCookieConsent";

    public const string MetricVisitorCookieName =
        "__Host-PromptHubMetricVisitor";

    private const string NecessaryOnlyValue =
        "v1.necessary";

    private const string AnalyticsValue =
        "v1.analytics";

    private static readonly TimeSpan
        ConsentLifetime =
            TimeSpan.FromDays(
                365);

    public CookieConsentLevel GetLevel(
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        if (!httpContext.Request.Cookies.TryGetValue(
                ConsentCookieName,
                out var value))
        {
            return CookieConsentLevel.Unset;
        }

        return value switch
        {
            NecessaryOnlyValue =>
                CookieConsentLevel.NecessaryOnly,

            AnalyticsValue =>
                CookieConsentLevel.Analytics,

            _ =>
                CookieConsentLevel.Unset
        };
    }

    public bool HasChoice(
        HttpContext httpContext)
    {
        return GetLevel(
            httpContext) !=
            CookieConsentLevel.Unset;
    }

    public bool AllowsAnalytics(
        HttpContext httpContext)
    {
        return GetLevel(
            httpContext) ==
            CookieConsentLevel.Analytics;
    }

    public void SetLevel(
        HttpContext httpContext,
        CookieConsentLevel level)
    {
        ArgumentNullException.ThrowIfNull(
            httpContext);

        var value =
            level switch
            {
                CookieConsentLevel.NecessaryOnly =>
                    NecessaryOnlyValue,

                CookieConsentLevel.Analytics =>
                    AnalyticsValue,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(level))
            };

        httpContext.Response.Cookies.Append(
            ConsentCookieName,
            value,
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
                    ConsentLifetime,

                IsEssential =
                    true
            });

        if (level ==
            CookieConsentLevel.NecessaryOnly)
        {
            httpContext.Response.Cookies.Delete(
                MetricVisitorCookieName,
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
    }
}
