namespace PromptHub.Web.Services.PromptTemplates;

public interface IVisualPromptTemplateCompiler
{
    VisualPromptTemplateCompileResult Compile(
        string displayTemplate,
        IReadOnlyList<VisualPromptFieldDefinition> fields);
}
