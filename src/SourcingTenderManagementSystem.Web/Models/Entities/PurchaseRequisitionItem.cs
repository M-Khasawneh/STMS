namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class PurchaseRequisitionItem
{
    public Guid Id { get; set; }
    public Guid PurchaseRequisitionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Specifications { get; set; } = string.Empty;
}
