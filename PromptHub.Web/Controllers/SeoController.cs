using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Controllers;

[Route("")]
public sealed class SeoController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public SeoController(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // -------------------------------------------------------------------------
    // EN: Dynamic XML sitemap containing only public canonical pages.
    // RU: Динамический XML sitemap только с публичными canonical-страницами.
    // -------------------------------------------------------------------------

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(
        CancellationToken cancellationToken)
    {
        var publicPrompts =
            await _dbContext.Prompts
                .AsNoTracking()
                .Where(prompt =>
                    prompt.Status ==
                        PromptStatus.Published &&
                    prompt.Category.IsActive)
                .Select(prompt =>
                    new
                    {
                        prompt.Id,
                        prompt.Slug,
                        prompt.CategoryId,
                        prompt.AuthorId,

                        LastModifiedAtUtc =
                            prompt.UpdatedAtUtc ??
                            prompt.PublishedAtUtc ??
                            prompt.CreatedAtUtc
                    })
                .OrderBy(prompt =>
                    prompt.Id)
                .ToListAsync(
                    cancellationToken);

        var latestPublicChange =
            publicPrompts.Count > 0
                ? publicPrompts.Max(prompt =>
                    prompt.LastModifiedAtUtc)
                : (DateTime?)null;

        var categoryIds =
            publicPrompts
                .Select(prompt =>
                    prompt.CategoryId)
                .Distinct()
                .ToArray();

        var categories =
            categoryIds.Length == 0
                ? []
                : await _dbContext.Categories
                    .AsNoTracking()
                    .Where(category =>
                        category.IsActive &&
                        categoryIds.Contains(
                            category.Id))
                    .Select(category =>
                        new
                        {
                            category.Id,
                            category.Slug
                        })
                    .OrderBy(category =>
                        category.Id)
                    .ToListAsync(
                        cancellationToken);

        var tags =
            await _dbContext.Tags
                .AsNoTracking()
                .Where(tag =>
                    tag.IsActive &&
                    tag.PromptTags.Any(promptTag =>
                        promptTag.Prompt.Status ==
                            PromptStatus.Published &&
                        promptTag.Prompt.Category.IsActive))
                .Select(tag =>
                    new
                    {
                        tag.Slug,

                        LastModifiedAtUtc =
                            tag.PromptTags
                                .Where(promptTag =>
                                    promptTag.Prompt.Status ==
                                        PromptStatus.Published &&
                                    promptTag.Prompt.Category.IsActive)
                                .Max(promptTag =>
                                    promptTag.Prompt.UpdatedAtUtc ??
                                    promptTag.Prompt.PublishedAtUtc ??
                                    promptTag.Prompt.CreatedAtUtc)
                    })
                .OrderBy(tag =>
                    tag.Slug)
                .ToListAsync(
                    cancellationToken);

        var authors =
            publicPrompts
                .GroupBy(prompt =>
                    prompt.AuthorId)
                .Select(group =>
                    new
                    {
                        AuthorId =
                            group.Key,

                        LastModifiedAtUtc =
                            group.Max(prompt =>
                                prompt.LastModifiedAtUtc)
                    })
                .OrderBy(author =>
                    author.AuthorId)
                .ToArray();

        var categoryLastModified =
            publicPrompts
                .GroupBy(prompt =>
                    prompt.CategoryId)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.Max(prompt =>
                            prompt.LastModifiedAtUtc));

        XNamespace sitemapNamespace =
            "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urlSet =
            new XElement(
                sitemapNamespace +
                "urlset");


        AddUrl(
            urlSet,
            sitemapNamespace,
            Url.Action(
                "Index",
                "Home",
                values: null,
                protocol:
                    Request.Scheme,
                host:
                    Request.Host.Value),
            latestPublicChange);


        AddUrl(
            urlSet,
            sitemapNamespace,
            Url.RouteUrl(
                "PromptsCatalog",
                values: null,
                protocol:
                    Request.Scheme,
                host:
                    Request.Host.Value),
            latestPublicChange);


        AddUrl(
            urlSet,
            sitemapNamespace,
            Url.RouteUrl(
                "PromptsTags",
                values: null,
                protocol:
                    Request.Scheme,
                host:
                    Request.Host.Value),
            latestPublicChange);

        // ---------------------------------------------------------------------
        // EN: Category landing pages containing published prompts.
        // RU: Страницы категорий, в которых есть опубликованные промпты.
        // ---------------------------------------------------------------------

        foreach (var category in
                 categories)
        {
            categoryLastModified.TryGetValue(
                category.Id,
                out var lastModifiedAtUtc);

            AddUrl(
                urlSet,
                sitemapNamespace,
                Url.RouteUrl(
                    "PromptsCategory",
                    new
                    {
                        category =
                            category.Slug
                    },
                    protocol:
                        Request.Scheme,
                    host:
                        Request.Host.Value),
                lastModifiedAtUtc);
        }

        // ---------------------------------------------------------------------
        // EN: Tag landing pages containing published prompts.
        // RU: Страницы тегов, в которых есть опубликованные промпты.
        // ---------------------------------------------------------------------

        foreach (var tag in
                 tags)
        {
            AddUrl(
                urlSet,
                sitemapNamespace,
                Url.RouteUrl(
                    "PromptsTag",
                    new
                    {
                        tag =
                            tag.Slug
                    },
                    protocol:
                        Request.Scheme,
                    host:
                        Request.Host.Value),
                tag.LastModifiedAtUtc);
        }

        // ---------------------------------------------------------------------
        // EN: Public author profiles that actually have published prompts.
        // RU: Публичные профили авторов, у которых есть публикации.
        // ---------------------------------------------------------------------

        foreach (var author in
                 authors)
        {
            AddUrl(
                urlSet,
                sitemapNamespace,
                Url.Action(
                    "Details",
                    "Authors",
                    new
                    {
                        id =
                            author.AuthorId
                    },
                    protocol:
                        Request.Scheme,
                    host:
                        Request.Host.Value),
                author.LastModifiedAtUtc);
        }


        foreach (var prompt in
                 publicPrompts)
        {
            AddUrl(
                urlSet,
                sitemapNamespace,
                Url.Action(
                    "Details",
                    "Prompts",
                    new
                    {
                        slug =
                            prompt.Slug
                    },
                    protocol:
                        Request.Scheme,
                    host:
                        Request.Host.Value),
                prompt.LastModifiedAtUtc);
        }

        var document =
            new XDocument(
                new XDeclaration(
                    "1.0",
                    "UTF-8",
                    null),
                urlSet);

        var xml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            document.Root!
                .ToString(
                    SaveOptions.DisableFormatting);

        return Content(
            xml,
            "application/xml; charset=utf-8");
    }

    // -------------------------------------------------------------------------
    // EN: robots.txt points crawlers to the sitemap.
    // RU: robots.txt сообщает поисковым роботам адрес sitemap.
    // -------------------------------------------------------------------------

    [HttpGet("robots.txt")]
    public IActionResult Robots()
    {
        var sitemapUrl =
            Url.Action(
                nameof(Sitemap),
                "Seo",
                values: null,
                protocol:
                    Request.Scheme,
                host:
                    Request.Host.Value);

        var content =
            $"""
             User-agent: *
             Allow: /

             Sitemap: {sitemapUrl}
             """;

        return Content(
            content,
            "text/plain; charset=utf-8");
    }


    private static void AddUrl(
        XElement urlSet,
        XNamespace sitemapNamespace,
        string? url,
        DateTime? lastModifiedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(
                url))
        {
            return;
        }

        var element =
            new XElement(
                sitemapNamespace +
                "url",
                new XElement(
                    sitemapNamespace +
                    "loc",
                    url));

        if (lastModifiedAtUtc.HasValue)
        {
            element.Add(
                new XElement(
                    sitemapNamespace +
                    "lastmod",
                    lastModifiedAtUtc.Value
                        .ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture)));
        }

        urlSet.Add(
            element);
    }
}
