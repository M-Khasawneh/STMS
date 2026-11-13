namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class EvaluationCriterion
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EvaluationKind Kind { get; set; }
    public decimal Weight { get; set; }
    public decimal? MaxScore { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
