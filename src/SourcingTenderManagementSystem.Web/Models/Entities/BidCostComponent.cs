namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class BidCostComponent
{
    public Guid Id { get; set; }
    public Guid BidId { get; set; }
    public string CostType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
