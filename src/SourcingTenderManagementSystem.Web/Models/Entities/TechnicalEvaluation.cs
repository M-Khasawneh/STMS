namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class TechnicalEvaluation
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid BidId { get; set; }
    public Guid EvaluationCriterionId { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public string? EvaluatorUserId { get; set; }
    public decimal Score { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? NonCompliance { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}
