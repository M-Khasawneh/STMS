namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class PurchaseRequisitionReview
{
    public Guid Id { get; set; }
    public Guid PurchaseRequisitionId { get; set; }
    public string ReviewerUserId { get; set; } = string.Empty;
    public PurchaseRequestStatus Outcome { get; set; }
    public string Comments { get; set; } = string.Empty;
    public DateTimeOffset ReviewedAt { get; set; }
}
