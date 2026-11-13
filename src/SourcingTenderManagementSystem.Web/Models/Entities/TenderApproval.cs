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
    public DateTimeOffset DecidedAt { get; set; }
}
