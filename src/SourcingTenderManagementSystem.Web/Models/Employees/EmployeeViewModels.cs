using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Employees;

public record EmployeeDetails(string Id, string UserName, string? Email, string? PhoneNumber,
    IReadOnlyList<string> Roles, bool Disabled, bool TemporarilyLocked, bool IsProtected,
    Guid? DepartmentId, string? DepartmentName)
{
    public string Status => Disabled ? "Disabled" : TemporarilyLocked ? "Temporarily locked" : "Enabled";
}

public class EmployeeContactForm
{
    [Required(ErrorMessage = "Select a department."), Display(Name = "Department")]
    public Guid? DepartmentId { get; set; }
    [BindNever]
    public IReadOnlyList<Department> Departments { get; set; } = [];
    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }
    [Phone, Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }
}

public sealed class EmployeeCreateViewModel : EmployeeContactForm
{
    [Required, StringLength(256), Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;
    [MinLength(1, ErrorMessage = "Select at least one employee role."), Display(Name = "Employee roles")]
    public string[] Roles { get; set; } = [];
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class EmployeeEditViewModel : EmployeeContactForm
{
    [Display(Name = "Employee roles")]
    public string[] Roles { get; set; } = [];
    [Display(Name = "Change role assignments")]
    public bool UpdateRoles { get; set; }
    [BindNever]
    public EmployeeDetails? Employee { get; set; }
}
