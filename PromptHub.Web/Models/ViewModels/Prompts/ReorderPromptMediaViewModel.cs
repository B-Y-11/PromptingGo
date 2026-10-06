using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class ReorderPromptMediaViewModel
{

    [MinLength(
        2,
        ErrorMessage =
            "Для изменения порядка необходимо минимум два изображения.")]
    public List<int> OrderedMediaIds { get; set; }
        = [];
}
