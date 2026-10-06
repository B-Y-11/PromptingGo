namespace PromptHub.Web.Services.PromptTemplates;

public sealed record DetectedPromptVariable(
    string Key,
    bool IsRequired,
    int SortOrder);
