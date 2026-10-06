using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.PromptRendering;

namespace PromptHub.Web.Services.PromptRendering;

public interface IPromptTemplateRenderer
{
    PromptRenderResult Render(
        string templateText,
        IReadOnlyCollection<PromptVariable> variables,
        IReadOnlyDictionary<string, string?> values);
}
