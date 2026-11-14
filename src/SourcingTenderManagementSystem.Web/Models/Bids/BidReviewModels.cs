using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

public sealed record BidActivity(string Description, DateTimeOffset At)
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string> {
        ["DraftCreated"] = "Draft created", ["DraftUpdated"] = "Draft updated",
        ["BidSubmitted"] = "Offer submitted", ["DocumentUploaded"] = "Document uploaded",
        ["ComplianceReviewed"] = "Compliance reviewed", ["ClarificationRequested"] = "Clarification requested",
        ["ClarificationAnswered"] = "Clarification answered", ["ClarificationResolved"] = "Clarification resolved",
        ["DocumentRemoved"] = "Document removed", ["DocumentReplaced"] = "Document replaced"
    };
}
public sealed record BidReviewRow(Bid Bid, Tender Tender, string? SupplierName);
public sealed record BidReviewComparison(Tender Tender, IReadOnlyList<PurchaseRequisitionItem> RequestedItems, IReadOnlyList<SupplierBidDetails> Offers);
