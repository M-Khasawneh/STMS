namespace SourcingTenderManagementSystem.Web.Models.Entities;

// Records provenance without enforcing an unresolved split/consolidation policy.
public class TenderPurchaseRequisition
{
    public Guid TenderId { get; set; }
    public Guid PurchaseRequisitionId { get; set; }
}
