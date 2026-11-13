namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class BidItem
{
    public Guid Id { get; set; }
    public Guid BidId { get; set; }
    public Guid? PurchaseRequisitionItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal OfferedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Specifications { get; set; } = string.Empty;
}
