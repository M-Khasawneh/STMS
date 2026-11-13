namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class CommitteeMember
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid CommitteeAssignmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
}
