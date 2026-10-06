using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PromptMetricHit
{
    public long Id { get; set; }

    public int PromptId { get; set; }

    public Prompt Prompt { get; set; }
        = null!;

    public PromptMetricKind Kind { get; set; }

    // -------------------------------------------------------------------------
    // EN: SHA-256 hash of the authenticated user or anonymous visitor key.
    // RU: SHA-256-хеш пользователя или анонимного идентификатора посетителя.
    // -------------------------------------------------------------------------

    public string ActorKeyHash { get; set; }
        = string.Empty;

    // -------------------------------------------------------------------------
    // EN: Start of the deduplication window.
    //     View -> UTC day.
    //     Copy -> UTC hour.
    //
    // RU: Начало окна дедупликации.
    //     Просмотр -> сутки UTC.
    //     Копирование -> час UTC.
    // -------------------------------------------------------------------------

    public DateTime BucketStartUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}
