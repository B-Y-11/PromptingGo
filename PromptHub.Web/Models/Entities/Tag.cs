namespace PromptHub.Web.Models.Entities;

public sealed class Tag
{
    public int Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<PromptTag> PromptTags { get; set; }
        = new List<PromptTag>();
}
