namespace PromptHub.Web.Models.PromptRendering;

public sealed record PromptRenderError(
    string Code,
    string Message,
    string? VariableKey = null);
