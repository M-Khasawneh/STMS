namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class AiBidAnalysis
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string InputReference { get; set; } = string.Empty;
    public string AnalysisJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
