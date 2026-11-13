namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public Guid? PurchaseRequisitionId { get; set; }
    public Guid? TenderId { get; set; }
    public Guid? BidId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
