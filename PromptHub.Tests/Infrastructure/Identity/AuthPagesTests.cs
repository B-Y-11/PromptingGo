using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PromptHub.Tests.Infrastructure;

namespace PromptHub.Tests.Integration.Identity;

[Collection(PostgreSqlCollection.Name)]
public sealed class AuthPagesTests
    : IAsyncLifetime
{
    private readonly PostgreSqlFixture
        _postgresSql;

    private AuthWebApplicationFactory
        _factory =
            null!;

    private HttpClient
        _client =
            null!;

    public AuthPagesTests(
        PostgreSqlFixture postgresSql)
    {
        _postgresSql =
            postgresSql;
    }

    public async Task InitializeAsync()
    {
        await _postgresSql
            .ResetDatabaseAsync();

        _factory =
            new AuthWebApplicationFactory(
                _postgresSql.ConnectionString);

        _client =
            _factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect =
                        false,

                    HandleCookies =
                        true,

                    BaseAddress =
                        new Uri(
                            "https://localhost")
                });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();

        _factory.Dispose();

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // EN: Public authentication pages must be available anonymously.
    // RU: Публичные страницы аутентификации доступны без авторизации.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    public async Task PublicAuthPage_ReturnsOk(
        string path)
    {
        var response =
            await _client.GetAsync(
                path);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // EN: Phone authentication is unavailable while the feature flag is disabled.
    // RU: Телефонная аутентификация недоступна, пока feature flag выключен.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PhoneAuthPage_WhenFeatureDisabled_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync(
                "/Identity/Account/Phone");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // EN: Anonymous users cannot access account management.
    // RU: Неавторизованный пользователь не имеет доступа к управлению аккаунтом.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SignInMethods_AnonymousUser_RedirectsToLogin()
    {
        var response =
            await _client.GetAsync(
                "/Identity/Account/Manage/SignInMethods");

        Assert.Equal(
            HttpStatusCode.Redirect,
            response.StatusCode);

        Assert.NotNull(
            response.Headers.Location);

        Assert.Contains(
            "/Identity/Account/Login",
            response.Headers.Location!.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }
}