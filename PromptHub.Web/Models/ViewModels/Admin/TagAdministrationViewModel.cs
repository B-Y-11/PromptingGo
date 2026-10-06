using System.ComponentModel.DataAnnotations;

namespace PromptHub.Web.Models.ViewModels.Admin;

public sealed class TagAdministrationViewModel
{
    public IReadOnlyList<TagAdministrationItemViewModel> Tags { get; set; }
        = Array.Empty<TagAdministrationItemViewModel>();
}

public sealed class TagAdministrationItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int TotalPromptsCount { get; set; }

    public int PublishedPromptsCount { get; set; }
}

public sealed class AdminTagEditorViewModel
{
    public int? TagId { get; set; }

    public bool IsEditMode { get; set; }

    public string PageTitle { get; set; }
        = string.Empty;

    public string SubmitButtonText { get; set; }
        = string.Empty;

    public string? CurrentSlug { get; set; }

    public AdminTagFormViewModel Input { get; set; }
        = new();
}

public sealed class AdminTagFormViewModel
{
    [Required(ErrorMessage = "Введите название тега.")]
    [StringLength(
        80,
        MinimumLength = 2,
        ErrorMessage = "Название тега должно содержать от 2 до 80 символов.")]
    [Display(Name = "Название")]
    public string Name { get; set; }
        = string.Empty;

    [StringLength(
        400,
        ErrorMessage = "Описание не должно превышать 400 символов.")]
    [Display(Name = "Описание")]
    public string Description { get; set; }
        = string.Empty;

    [Range(
        -10000,
        10000,
        ErrorMessage = "Порядок должен быть числом от -10000 до 10000.")]
    [Display(Name = "Порядок сортировки")]
    public int SortOrder { get; set; }

    [Display(Name = "Активен")]
    public bool IsActive { get; set; } = true;
}
