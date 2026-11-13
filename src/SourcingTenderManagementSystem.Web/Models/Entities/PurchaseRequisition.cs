namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class PurchaseRequisition
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
    public PurchaseRequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}
