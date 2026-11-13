namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class ClarificationRequest
{
    public Guid Id { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? TenderId { get; set; }
    public Guid? BidId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ClarificationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
