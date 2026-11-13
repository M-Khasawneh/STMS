namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class ComplianceReview
{
    public Guid Id { get; set; }
    public Guid BidId { get; set; }
    public string ReviewerUserId { get; set; } = string.Empty;
    public ComplianceStatus Outcome { get; set; }
    public string? Reasons { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
}
