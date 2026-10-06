namespace PromptHub.Web.Services.Moderation;

public sealed class PromptDuplicateMatch
{
    public int PromptId { get; init; }

    public int PromptVersionId { get; init; }

    public string Title { get; init; }
        = string.Empty;

    public string Slug { get; init; }
        = string.Empty;

    public string AuthorName { get; init; }
        = string.Empty;

    public DateTime? PublishedAtUtc { get; init; }

    public double Similarity { get; init; }

    public double ReadyTextSimilarity { get; init; }

    public double TemplateTextSimilarity { get; init; }

    public int SimilarityPercent =>
        (int)Math.Round(
            Similarity * 100,
            MidpointRounding.AwayFromZero);

    public int ReadyTextSimilarityPercent =>
        (int)Math.Round(
            ReadyTextSimilarity * 100,
            MidpointRounding.AwayFromZero);

    public int TemplateTextSimilarityPercent =>
        (int)Math.Round(
            TemplateTextSimilarity * 100,
            MidpointRounding.AwayFromZero);
}
