namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class EvaluationReport
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string PreparedByUserId { get; set; } = string.Empty;
    public Guid? RecommendedBidId { get; set; }
    public string TechnicalSummary { get; set; } = string.Empty;
    public string FinancialSummary { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string NonCompliance { get; set; } = string.Empty;
    public string Comparison { get; set; } = string.Empty;
    public string FinalResult { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public DateTimeOffset? SubmittedAt { get; set; }
}
