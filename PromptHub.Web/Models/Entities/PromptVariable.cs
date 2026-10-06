using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Models.Entities;

public sealed class PromptVariable
{
    public int Id { get; set; }

    public int PromptVersionId { get; set; }

    public PromptVersion PromptVersion { get; set; } = null!;

    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public PromptVariableType VariableType { get; set; }

    public string Placeholder { get; set; } = string.Empty;

    public string HelpText { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    public string DefaultValue { get; set; } = string.Empty;

    public string OptionsJson { get; set; } = "[]";

    public int SortOrder { get; set; }
}
