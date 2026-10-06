namespace PromptHub.Web.Models.Entities;

public sealed class PromptFavorite
{
    public string UserId { get; set; }
        = string.Empty;

    public ApplicationUser User { get; set; }
        = null!;

    public int PromptId { get; set; }

    public Prompt Prompt { get; set; }
        = null!;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}
