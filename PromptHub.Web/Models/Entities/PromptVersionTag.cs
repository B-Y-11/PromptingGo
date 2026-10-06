namespace PromptHub.Web.Models.Entities;

public sealed class PromptVersionTag
{
    public int PromptVersionId { get; set; }

    public PromptVersion PromptVersion { get; set; }
        = null!;

    public int TagId { get; set; }

    public Tag Tag { get; set; }
        = null!;
}
