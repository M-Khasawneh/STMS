using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

public sealed record SupplierOfferRow(Bid Bid, Tender Tender, bool CanEdit);
public sealed record SupplierBidDetails(BidDetails Details, Tender Tender, bool CanEdit, string? ReadOnlyReason);
public sealed record SupplierBidConfirmation(SupplierBidDetails Offer, string? Error);
public sealed class SupplierBidEditor
{
    public Guid? Id { get; init; }
    public required Tender Tender { get; init; }
    public required IReadOnlyList<PurchaseRequisitionItem> RequestedItems { get; init; }
    public required BidDraftForm Form { get; init; }
    public IReadOnlyList<BidDocument> Documents { get; init; } = [];
}
public sealed class BidDocumentForm
{
    [Required] public string Version { get; set; } = "";
    [Required] public string DocumentType { get; set; } = "";
    [Required] public IFormFile? File { get; set; }
}
public sealed class BidSubmitForm
{
    [Required] public string Version { get; set; } = "";
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm that you understand this submission is final.")]
    public bool Confirmed { get; set; }
}
