using System.ComponentModel.DataAnnotations;

namespace SourcingTenderManagementSystem.Web.Models.Departments;

public sealed class DepartmentFormViewModel
{
    private string name = string.Empty;

    [Required(ErrorMessage = "Enter a department name.")]
    [StringLength(256, ErrorMessage = "Department name must be 256 characters or fewer.")]
    [Display(Name = "Department name")]
    public string Name
    {
        get => name;
        set => name = value?.Trim() ?? string.Empty;
    }
}
