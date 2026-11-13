namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class TenderAward
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid BidId { get; set; }
    public Guid TenderApprovalId { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTimeOffset AwardedAt { get; set; }
    public string? ContractReference { get; set; }
}
