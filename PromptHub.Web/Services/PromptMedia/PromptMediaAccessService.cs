using System.Security.Claims;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Security;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Web.Services.PromptMedia;

public sealed class PromptMediaAccessService
    : IPromptMediaAccessService
{
    // -------------------------------------------------------------------------
    // EN: Public media visibility
    // RU: Публичная видимость медиа
    // -------------------------------------------------------------------------

    public bool IsPubliclyVisible(
        PromptMediaEntity media)
    {
        ArgumentNullException.ThrowIfNull(
            media);

        return
    media.Status ==
        PromptMediaStatus.Published &&
    media.Prompt.Status ==
        PromptStatus.Published &&
    media.Prompt.Category?.IsActive ==
        true;
    }

    // -------------------------------------------------------------------------
    // EN: Public, owner and staff access rules
    // RU: Правила публичного доступа, доступа владельца и сотрудников
    // -------------------------------------------------------------------------

    public bool CanView(
        PromptMediaEntity media,
        ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(
            media);

        ArgumentNullException.ThrowIfNull(
            user);

        if (IsPubliclyVisible(
                media))
        {
            return true;
        }

        if (user.Identity?.IsAuthenticated !=
            true)
        {
            return false;
        }

        var userId =
            user.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(
                userId) &&
            (
                string.Equals(
                    media.UploadedById,
                    userId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    media.Prompt.AuthorId,
                    userId,
                    StringComparison.Ordinal)
            ))
        {
            return true;
        }

        return
            user.IsInRole(
                ApplicationRoles.Moderator) ||
            user.IsInRole(
                ApplicationRoles.Administrator);
    }
}
