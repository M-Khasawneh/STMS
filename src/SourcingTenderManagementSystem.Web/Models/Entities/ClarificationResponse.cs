namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class ClarificationResponse
{
    public Guid Id { get; set; }
    public Guid ClarificationRequestId { get; set; }
    public string ResponderUserId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTimeOffset RespondedAt { get; set; }
}
