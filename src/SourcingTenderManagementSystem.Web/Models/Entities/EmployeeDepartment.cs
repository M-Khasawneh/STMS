namespace SourcingTenderManagementSystem.Web.Models.Entities;

// One row per employee; non-employee Identity accounts do not require a row.
public sealed class EmployeeDepartment
{
    public string UserId { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
}
