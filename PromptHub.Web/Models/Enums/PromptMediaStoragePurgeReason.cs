namespace PromptHub.Web.Models.Enums;

public enum PromptMediaStoragePurgeReason
{
    // -------------------------------------------------------------------------
    // EN: The archived media retention period expired.
    // RU: Закончился срок хранения архивного медиа.
    // -------------------------------------------------------------------------

    ArchivedRetentionExpired = 1,

    // -------------------------------------------------------------------------
    // EN: The rejected media retention period expired.
    // RU: Закончился срок хранения отклонённого медиа.
    // -------------------------------------------------------------------------

    RejectedRetentionExpired = 2
}
