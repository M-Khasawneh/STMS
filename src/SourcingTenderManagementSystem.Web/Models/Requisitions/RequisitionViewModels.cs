using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Models.Requisitions;

public sealed class RequisitionItemForm : IValidatableObject
{
    public Guid? Id { get; set; }
    [Required] public string Description { get; set; } = "";
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Quantity { get; set; }
    [Required] public string Specifications { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Quantity, 6) != Quantity)
            yield return new("Quantity supports at most six decimal places.", new[] { nameof(Quantity) });
    }
}

public sealed class RequisitionForm
{
    [Required, StringLength(256)] public string Title { get; set; } = "";
    [Required] public string Reason { get; set; } = "";
    [Required, RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Select Low, Medium, High or Urgent.")]
    public string Priority { get; set; } = "";
    [Required, DataType(DataType.Date), Display(Name = "Target date")] public DateOnly? TargetDate { get; set; }
    public List<RequisitionItemForm> Items { get; set; } = [];
    public string? Version { get; set; }
    [BindNever] public Guid? Id { get; set; }
    [BindNever] public string DepartmentName { get; set; } = "";
    [BindNever] public DateOnly Today { get; set; }
}

public sealed class RequisitionActionForm
{
    [Required] public string Version { get; set; } = "";
    [Required] public PurchaseRequestStatus? Decision { get; set; }
    public string? Comments { get; set; }
}

public sealed record RequisitionDetails(PurchaseRequisition Request, IReadOnlyList<PurchaseRequisitionItem> Items,
    IReadOnlyList<PurchaseRequisitionReview> Reviews, IReadOnlyList<AuditLog> History,
    string DepartmentName, string CreatorName, string Version, RequisitionActor Actor);

public sealed record RequisitionList(IReadOnlyList<PurchaseRequisition> Requests, RequisitionActor Actor);
