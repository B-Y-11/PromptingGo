using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PromptHub.Web.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace PromptHub.Web.Models.ViewModels.Prompts;

public class CreatePromptViewModel
{

    [Required(ErrorMessage = "Введите название промпта.")]
    [StringLength(
        180,
        ErrorMessage =
            "Название не должно превышать 180 символов.")]
    [Display(Name = "Название")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Добавьте готовый промпт.")]
    [Display(Name = "Готовый промпт")]
    public string ReadyText { get; set; } = string.Empty;

    [Display(Name = "Добавить настраиваемую версию")]
    public bool EnableConstructor { get; set; }

    [Required(ErrorMessage = "Добавьте краткое описание.")]
    [StringLength(
        700,
        ErrorMessage =
            "Описание не должно превышать 700 символов.")]
    [Display(Name = "Краткое описание")]
    public string Summary { get; set; } = string.Empty;

    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Выберите категорию.")]
    [Display(Name = "Категория")]
    public int CategoryId { get; set; }

    [Display(Name = "Сложность")]
    public PromptDifficulty Difficulty { get; set; }
        = PromptDifficulty.Beginner;

    [Display(Name = "Настраиваемая версия")]
    public string TemplateText { get; set; } = string.Empty;

    public string ConstructorFieldsJson { get; set; }
    = "[]";

    [Display(Name = "Инструкция по использованию")]
    public string UsageInstructions { get; set; }
        = string.Empty;

    [Display(Name = "Пример результата")]
    public string ExampleResult { get; set; }
        = string.Empty;

    public IReadOnlyList<SelectListItem> Categories { get; set; }
        = Array.Empty<SelectListItem>();

    public int[] SelectedTagIds { get; set; }
    = Array.Empty<int>();

    public IReadOnlyList<SelectListItem> TagOptions { get; set; }
        = Array.Empty<SelectListItem>();


    [Display(Name = "Изображения")]
    public List<IFormFile>? Images { get; set; }


    public int MaxImagesPerPrompt { get; set; }
        = 6;

    public long MaxImageSizeBytes { get; set; }
        = 5 * 1024 * 1024;
}
