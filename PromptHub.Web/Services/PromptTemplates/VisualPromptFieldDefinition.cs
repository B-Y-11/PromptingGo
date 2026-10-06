using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.PromptTemplates;

public sealed class VisualPromptFieldDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public PromptVariableType VariableType { get; set; }
        = PromptVariableType.Text;

    public bool IsRequired { get; set; } = true;

    public string Placeholder { get; set; } = string.Empty;

    public string HelpText { get; set; } = string.Empty;

    public List<string> Options { get; set; } = new();
}

public sealed record CompiledPromptVariable(
    string Key,
    string Label,
    PromptVariableType VariableType,
    string Placeholder,
    string HelpText,
    bool IsRequired,
    string DefaultValue,
    string OptionsJson,
    int SortOrder);

public sealed class VisualPromptTemplateCompileResult
{
    public string TemplateText { get; init; } = string.Empty;

    public IReadOnlyList<CompiledPromptVariable> Variables
    {
        get;
        init;
    } = Array.Empty<CompiledPromptVariable>();

    public IReadOnlyList<string> Errors
    {
        get;
        init;
    } = Array.Empty<string>();

    public bool IsSuccess => Errors.Count == 0;
}
