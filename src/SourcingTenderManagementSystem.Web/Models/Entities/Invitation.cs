namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid TenderSupplierId { get; set; }
    public string SentByUserId { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
