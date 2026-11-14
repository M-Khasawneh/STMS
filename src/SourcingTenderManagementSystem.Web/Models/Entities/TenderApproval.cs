namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class TenderApproval
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid EvaluationReportId { get; set; }
    public ApprovalStage Stage { get; set; }
    public DecisionStatus Decision { get; set; }
    public string? ActorUserId { get; set; }
    public Guid? CommitteeAssignmentId { get; set; }
    public string Comments { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public DateTimeOffset? RoundOpenedAt { get; set; }
    public DateTimeOffset? RoundClosedAt { get; set; }
    // Server-written immutable JSON array of the four CommitteeMember IDs for this round.
    public string? RoundMemberSnapshot { get; set; }
    public string? RoundCloseReason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
