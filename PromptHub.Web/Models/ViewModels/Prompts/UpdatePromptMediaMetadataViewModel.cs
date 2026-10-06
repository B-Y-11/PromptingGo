using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Models.ViewModels.Prompts;

public sealed class UpdatePromptMediaMetadataViewModel
{

    [Display(Name = "Подпись")]
    [MaxLength(
        500,
        ErrorMessage =
            "Подпись не должна превышать 500 символов.")]
    public string? Caption { get; set; }

    [Display(Name = "Alt-текст")]
    [MaxLength(
        300,
        ErrorMessage =
            "Alt-текст не должен превышать 300 символов.")]
    public string? AltText { get; set; }
}
