using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Web.Services.PromptMedia;

public interface IPromptMediaArrangementService
{
    // -------------------------------------------------------------------------
    // EN: Normalize and select exactly one prompt cover.
    // RU: Нормализация и выбор единственной обложки промпта.
    // -------------------------------------------------------------------------

    PromptMediaCoverUpdateResult SetCover(
        IEnumerable<PromptMediaEntity> mediaItems,
        int selectedMediaId,
        DateTime updatedAtUtc);

    // -------------------------------------------------------------------------
    // EN: Validate and apply the complete active media order.
    // RU: Проверка и применение полного порядка активных медиа.
    // -------------------------------------------------------------------------

    PromptMediaOrderUpdateResult ApplyOrder(
        IEnumerable<PromptMediaEntity> mediaItems,
        IReadOnlyList<int> orderedMediaIds,
        DateTime updatedAtUtc);
}
