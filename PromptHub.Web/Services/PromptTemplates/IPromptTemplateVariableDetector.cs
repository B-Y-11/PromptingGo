namespace PromptHub.Web.Services.PromptTemplates;

public interface IPromptTemplateVariableDetector
{
    IReadOnlyList<DetectedPromptVariable> Detect(
        string templateText);
}
