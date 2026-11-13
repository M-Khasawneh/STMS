namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class CommitteeAssignment
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public CommitteeKind Kind { get; set; }
}
