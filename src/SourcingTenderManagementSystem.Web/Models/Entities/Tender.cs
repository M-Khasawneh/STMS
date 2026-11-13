namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class Tender
{
    public Guid Id { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Terms { get; set; } = string.Empty;
    public DateTimeOffset SubmissionStart { get; set; }
    public DateTimeOffset SubmissionDeadline { get; set; }
    public TenderStatus Status { get; set; }
}
