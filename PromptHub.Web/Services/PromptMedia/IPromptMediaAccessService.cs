using System.Security.Claims;
using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Web.Services.PromptMedia;

public interface IPromptMediaAccessService
{
    // -------------------------------------------------------------------------
    // EN: Public media visibility
    // RU: Публичная видимость медиа
    // -------------------------------------------------------------------------

    bool IsPubliclyVisible(
        PromptMediaEntity media);

    // -------------------------------------------------------------------------
    // EN: Access for the current application user
    // RU: Доступ для текущего пользователя приложения
    // -------------------------------------------------------------------------

    bool CanView(
        PromptMediaEntity media,
        ClaimsPrincipal user);
}
