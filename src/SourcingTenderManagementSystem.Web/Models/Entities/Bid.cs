namespace SourcingTenderManagementSystem.Web.Models.Entities;

public class Bid
{
    public Guid Id { get; set; }
    public Guid TenderSupplierId { get; set; }
    public Guid TenderId { get; set; }
    public BidStatus Status { get; set; }
    public string DeliveryTimeframe { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public DateOnly? OfferValidUntil { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}
