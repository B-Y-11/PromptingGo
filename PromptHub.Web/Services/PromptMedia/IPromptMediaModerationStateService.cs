using PromptMediaEntity =
    PromptHub.Web.Models.Entities.PromptMedia;

namespace PromptHub.Web.Services.PromptMedia;

public interface IPromptMediaModerationStateService
{

    void ApproveNewMedia(
        PromptMediaEntity media,
        string moderatorId,
        DateTime moderatedAtUtc);

    void RejectNewMedia(
        PromptMediaEntity media,
        string moderatorId,
        string moderationComment,
        DateTime moderatedAtUtc);


    void ApproveMetadata(
        PromptMediaEntity media,
        string moderatorId,
        DateTime moderatedAtUtc);

    void RejectMetadata(
        PromptMediaEntity media,
        string moderatorId,
        string moderationComment,
        DateTime moderatedAtUtc);
}
