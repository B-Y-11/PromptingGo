using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PromptHub.Web.Models.ViewModels.Admin;

public sealed class CategoryAdministrationViewModel
{
    public IReadOnlyList<CategoryAdministrationItemViewModel> Categories { get; set; }
        = Array.Empty<CategoryAdministrationItemViewModel>();
}

public sealed class CategoryAdministrationItemViewModel
{
    public int Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string Slug { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public string ParentName { get; set; }
        = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int TotalPromptsCount { get; set; }

    public int PublishedPromptsCount { get; set; }

    public int ArchivedPromptsCount { get; set; }
}

public sealed class AdminCategoryEditorViewModel
{
    public int? CategoryId { get; set; }

    public bool IsEditMode { get; set; }

    public string PageTitle { get; set; }
        = string.Empty;

    public string SubmitButtonText { get; set; }
        = string.Empty;

    public string? CurrentSlug { get; set; }

    public AdminCategoryFormViewModel Input { get; set; }
        = new();

    public IReadOnlyList<SelectListItem> ParentCategoryOptions { get; set; }
        = Array.Empty<SelectListItem>();
}

public sealed class AdminCategoryFormViewModel
{
    [Required(ErrorMessage = "Введите название категории.")]
    [StringLength(
        100,
        MinimumLength = 2,
        ErrorMessage = "Название должно содержать от 2 до 100 символов.")]
    [Display(Name = "Название")]
    public string Name { get; set; }
        = string.Empty;

    [StringLength(
        500,
        ErrorMessage = "Описание не должно превышать 500 символов.")]
    [Display(Name = "Описание")]
    public string Description { get; set; }
        = string.Empty;

    [Display(Name = "Родительская категория")]
    public int? ParentId { get; set; }

    [Range(
        -10000,
        10000,
        ErrorMessage = "Порядок должен быть числом от -10000 до 10000.")]
    [Display(Name = "Порядок сортировки")]
    public int SortOrder { get; set; }

    [Display(Name = "Активна")]
    public bool IsActive { get; set; } = true;
}
