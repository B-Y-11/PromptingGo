using System.Security.Claims;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Security;
using PromptHub.Web.Services.PromptMedia;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Tests.Services.PromptMedia;

public sealed class PromptMediaAccessServiceTests
{
    private const string AuthorId =
        "author-user-id";

    private const string UploaderId =
        "uploader-user-id";

    private const string OtherUserId =
        "other-user-id";

    private readonly PromptMediaAccessService
        _service =
            new();

    // -------------------------------------------------------------------------
    // EN: Public visibility tests
    // RU: Тесты публичной видимости
    // -------------------------------------------------------------------------

    [Fact]
    public void IsPubliclyVisible_ReturnsTrue_WhenMediaAndPromptArePublished()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Published);

        var result =
            _service.IsPubliclyVisible(
                media);

        Assert.True(
            result);
    }

    [Fact]
    public void IsPubliclyVisible_ReturnsFalse_WhenPromptIsNotPublished()
    {
        var media =
            CreateMedia(
                PromptStatus.Draft,
                PromptMediaStatus.Published);

        var result =
            _service.IsPubliclyVisible(
                media);

        Assert.False(
            result);
    }

    [Fact]
    public void IsPubliclyVisible_ReturnsFalse_WhenCategoryIsInactive()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Published,
                categoryIsActive:
                    false);

        var result =
            _service.IsPubliclyVisible(
                media);

        Assert.False(
            result);
    }

    [Fact]
    public void CanView_DeniesAnonymousUser_WhenCategoryIsInactive()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Published,
                categoryIsActive:
                    false);

        var user =
            CreateAnonymousUser();

        var result =
            _service.CanView(
                media,
                user);

        Assert.False(
            result);
    }

    [Fact]
    public void IsPubliclyVisible_ReturnsFalse_WhenMediaIsPending()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.PendingModeration);

        var result =
            _service.IsPubliclyVisible(
                media);

        Assert.False(
            result);
    }

    [Fact]
    public void IsPubliclyVisible_ReturnsFalse_WhenMediaIsArchived()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Archived);

        var result =
            _service.IsPubliclyVisible(
                media);

        Assert.False(
            result);
    }

    // -------------------------------------------------------------------------
    // EN: Anonymous access tests
    // RU: Тесты анонимного доступа
    // -------------------------------------------------------------------------

    [Fact]
    public void CanView_AllowsAnonymousUser_WhenMediaIsPublic()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Published);

        var user =
            CreateAnonymousUser();

        var result =
            _service.CanView(
                media,
                user);

        Assert.True(
            result);
    }

    [Fact]
    public void CanView_DeniesAnonymousUser_WhenMediaIsPending()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.PendingModeration);

        var user =
            CreateAnonymousUser();

        var result =
            _service.CanView(
                media,
                user);

        Assert.False(
            result);
    }

    [Fact]
    public void CanView_DeniesAnonymousUser_WhenPromptIsNotPublished()
    {
        var media =
            CreateMedia(
                PromptStatus.Draft,
                PromptMediaStatus.Published);

        var user =
            CreateAnonymousUser();

        var result =
            _service.CanView(
                media,
                user);

        Assert.False(
            result);
    }

    // -------------------------------------------------------------------------
    // EN: Author and uploader access tests
    // RU: Тесты доступа автора и загрузившего пользователя
    // -------------------------------------------------------------------------

    [Fact]
    public void CanView_AllowsPromptAuthor_WhenMediaIsPending()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.PendingModeration);

        var user =
            CreateAuthenticatedUser(
                AuthorId);

        var result =
            _service.CanView(
                media,
                user);

        Assert.True(
            result);
    }

    [Fact]
    public void CanView_AllowsUploader_WhenUploaderIsNotPromptAuthor()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.PendingModeration);

        var user =
            CreateAuthenticatedUser(
                UploaderId);

        var result =
            _service.CanView(
                media,
                user);

        Assert.True(
            result);
    }

    [Fact]
    public void CanView_AllowsPromptAuthor_ToViewArchivedMedia()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Archived);

        var user =
            CreateAuthenticatedUser(
                AuthorId);

        var result =
            _service.CanView(
                media,
                user);

        Assert.True(
            result);
    }

    [Fact]
    public void CanView_DeniesUnrelatedAuthenticatedUser()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.PendingModeration);

        var user =
            CreateAuthenticatedUser(
                OtherUserId);

        var result =
            _service.CanView(
                media,
                user);

        Assert.False(
            result);
    }

    // -------------------------------------------------------------------------
    // EN: Staff access tests
    // RU: Тесты доступа сотрудников
    // -------------------------------------------------------------------------

    [Fact]
    public void CanView_AllowsModerator_WhenMediaIsPending()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.PendingModeration);

        var user =
            CreateAuthenticatedUser(
                OtherUserId,
                ApplicationRoles.Moderator);

        var result =
            _service.CanView(
                media,
                user);

        Assert.True(
            result);
    }

    [Fact]
    public void CanView_AllowsAdministrator_WhenMediaIsRejected()
    {
        var media =
            CreateMedia(
                PromptStatus.Published,
                PromptMediaStatus.Rejected);

        var user =
            CreateAuthenticatedUser(
                OtherUserId,
                ApplicationRoles.Administrator);

        var result =
            _service.CanView(
                media,
                user);

        Assert.True(
            result);
    }


    private static PromptMediaEntity CreateMedia(
    PromptStatus promptStatus,
    PromptMediaStatus mediaStatus,
    bool categoryIsActive = true)
    {
        var author =
            new ApplicationUser
            {
                Id =
                    AuthorId,

                UserName =
                    "author",

                DisplayName =
                    "Автор"
            };

        var uploader =
            new ApplicationUser
            {
                Id =
                    UploaderId,

                UserName =
                    "uploader",

                DisplayName =
                    "Загрузивший пользователь"
            };

        var category =
    new Category
    {
        Id =
            5,

        Name =
            "Тестовая категория",

        Slug =
            "test-category",

        IsActive =
            categoryIsActive
    };

        var prompt =
    new Prompt
    {
        Id =
            10,

        Title =
            "Тестовый промпт",

        Slug =
            "test-prompt",

        AuthorId =
            AuthorId,

        Author =
            author,

        CategoryId =
            category.Id,

        Category =
            category,

        Status =
            promptStatus
    };

        return new PromptMediaEntity
        {
            Id =
                20,

            PromptId =
                prompt.Id,

            Prompt =
                prompt,

            UploadedById =
                UploaderId,

            UploadedBy =
                uploader,

            MediaType =
                PromptMediaType.Image,

            Status =
                mediaStatus,

            StorageProvider =
                MediaStorageProvider.Local,

            StorageKey =
                "2026/08/test-image.jpg",

            ThumbnailStorageKey =
                "2026/08/test-image.thumb.webp"
        };
    }

    private static ClaimsPrincipal CreateAnonymousUser()
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity());
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(
        string userId,
        string? role = null)
    {
        var claims =
            new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    userId)
            };

        if (!string.IsNullOrWhiteSpace(
                role))
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    role));
        }

        var identity =
            new ClaimsIdentity(
                claims,
                authenticationType:
                    "PromptHub.Tests");

        return new ClaimsPrincipal(
            identity);
    }
}