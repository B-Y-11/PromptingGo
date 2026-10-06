using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Services.MediaStorage;

namespace PromptHub.Web.Controllers;

[AllowAnonymous]
[Route("users")]
public sealed class UserAvatarController
    : Controller
{
    private readonly ApplicationDbContext
        _dbContext;

    private readonly IMediaStorageService
        _mediaStorageService;

    public UserAvatarController(
        ApplicationDbContext dbContext,
        IMediaStorageService mediaStorageService)
    {
        _dbContext =
            dbContext;

        _mediaStorageService =
            mediaStorageService;
    }

    // -------------------------------------------------------------------------
    // EN: Public immutable profile avatar content
    // RU: Публичное неизменяемое содержимое аватара профиля
    // -------------------------------------------------------------------------

    [HttpGet(
        "{userId}/avatar/{version:long}",
        Name = "UserAvatarContent")]
    public async Task<IActionResult> Content(
        string userId,
        long version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                userId))
        {
            return NotFound();
        }

        var avatar =
            await _dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    user.Id == userId)
                .Select(user =>
                    new
                    {
                        user.AvatarStorageKey,
                        user.AvatarContentType,
                        user.AvatarUpdatedAtUtc
                    })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (avatar is null ||
            string.IsNullOrWhiteSpace(
                avatar.AvatarStorageKey) ||
            !avatar.AvatarUpdatedAtUtc.HasValue ||
            avatar.AvatarUpdatedAtUtc.Value.Ticks !=
                version)
        {
            return NotFound();
        }

        try
        {
            var stream =
                await _mediaStorageService
                    .OpenProfileAvatarReadAsync(
                        avatar.AvatarStorageKey,
                        cancellationToken);

            Response.Headers.CacheControl =
                "public,max-age=31536000,immutable";

            Response.Headers[
                "X-Content-Type-Options"] =
                    "nosniff";

            return File(
                stream,
                avatar.AvatarContentType
                ?? "image/webp");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (MediaStorageException)
        {
            return NotFound();
        }
    }
}
