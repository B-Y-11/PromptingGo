namespace PromptHub.Web.Models.PromptRendering;

public sealed record PromptRenderResult(
    string? RenderedText,
    IReadOnlyList<PromptRenderError> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public static PromptRenderResult Success(string renderedText)
    {
        return new PromptRenderResult(
            renderedText,
            Array.Empty<PromptRenderError>());
    }

    public static PromptRenderResult Failure(
        IEnumerable<PromptRenderError> errors)
    {
        return new PromptRenderResult(
            null,
            errors.ToArray());
    }
}
