namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class CommitteeVote
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid TenderApprovalId { get; set; }
    public Guid CommitteeMemberId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public DateTimeOffset VotedAt { get; set; }
}
