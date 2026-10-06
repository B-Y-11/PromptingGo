using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PromptHub.Tests.Infrastructure;

namespace PromptHub.Tests.Seo;

public sealed class SeoIntegrationTests
    : IClassFixture<
        PromptHubWebApplicationFactory>,
      IAsyncLifetime
{
    private readonly PromptHubWebApplicationFactory
        _factory;

    private readonly HttpClient
        _client;

    public SeoIntegrationTests(
        PromptHubWebApplicationFactory factory)
    {
        _factory =
            factory;

        _client =
            factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress =
                        new Uri(
                            "https://localhost"),

                    AllowAutoRedirect =
                        false
                });
    }

    public Task InitializeAsync()
    {
        return _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // EN: robots.txt must expose the sitemap.
    // RU: robots.txt должен сообщать адрес sitemap.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Robots_ReturnsSitemapUrl()
    {
        var response =
            await _client.GetAsync(
                "/robots.txt");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "User-agent: *",
            body);

        Assert.Contains(
            "Allow: /",
            body);

        Assert.Contains(
            "Sitemap: https://localhost/sitemap.xml",
            body);
    }

    // -------------------------------------------------------------------------
    // EN: Sitemap contains public prompts but never drafts.
    // RU: Sitemap содержит публикации, но не черновики.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Sitemap_ContainsPublishedPrompt_ButNotDraft()
    {
        var response =
            await _client.GetAsync(
                "/sitemap.xml");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            $"/prompts/{PromptHubWebApplicationFactory.PublishedPromptSlug}",
            body);

        Assert.DoesNotContain(
            PromptHubWebApplicationFactory.DraftPromptSlug,
            body);
    }

    // -------------------------------------------------------------------------
    // EN: Public prompt must remain indexable and self-canonical.
    // RU: Публичный промпт должен индексироваться и иметь self-canonical.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PublishedPrompt_IsIndexable_AndSelfCanonical()
    {
        var response =
            await _client.GetAsync(
                $"/prompts/{PromptHubWebApplicationFactory.PublishedPromptSlug}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
    "content=\"PromptingGo\"",
    body);

        Assert.DoesNotContain(
            "content=\"PromptHub\"",
            body);

        Assert.Contains(
            "content=\"index,follow\"",
            body);

        Assert.Contains(
            $"href=\"https://localhost/prompts/{PromptHubWebApplicationFactory.PublishedPromptSlug}\"",
            body);

        Assert.Contains(
            "\"@type\":\"CreativeWork\"",
            body);
    }

    // -------------------------------------------------------------------------
    // EN: Author preview must never become an indexable public page.
    // RU: Предпросмотр автора никогда не должен индексироваться.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Preview_IsNoIndex()
    {
        using var request =
            PromptHubWebApplicationFactory
                .CreateAuthenticatedRequest(
                    HttpMethod.Get,
                    $"/prompts/{PromptHubWebApplicationFactory.PublishedPromptId}/preview");

        var response =
            await _client.SendAsync(
                request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var body =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "content=\"noindex,follow\"",
            body);

        Assert.DoesNotContain(
            "\"@type\":\"CreativeWork\"",
            body);
    }

    // -------------------------------------------------------------------------
    // EN: Unknown prompt slugs must return a real 404.
    // RU: Несуществующий slug промпта должен возвращать настоящий 404.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MissingPrompt_Returns404()
    {
        var response =
            await _client.GetAsync(
                "/prompts/seo-prompt-that-does-not-exist");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // EN: Garbage filter values must not create indexable 200 pages.
    // RU: Мусорные фильтры не должны создавать страницы с HTTP 200.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(
        "/prompts?difficulty=abrakadabra")]
    [InlineData(
        "/prompts?sort=abrakadabra")]
    public async Task InvalidCatalogFilter_Returns404(
        string url)
    {
        var response =
            await _client.GetAsync(
                url);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
