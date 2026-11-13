using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;
public sealed class BidException(int status, string message) : Exception(message) { public int Status { get; } = status; }

public static class BidRules
{
    public const decimal MaximumDecimal = 999999999999.999999m; // SQL decimal(18,6).
    public static readonly TenderStatus[] ClosedStages = [TenderStatus.Closed, TenderStatus.UnderEvaluation,
        TenderStatus.AwaitingManagerReview, TenderStatus.AwaitingCommitteeDecision, TenderStatus.Awarded];
    public static bool CanRead(BidAccount a, Bid bid, Tender tender, Guid supplierId) =>
        bid.TenderId == tender.Id && (a.Supplier ? a.SupplierId == supplierId : a.Internal && bid.Status != BidStatus.Draft && bid.SubmittedAt.HasValue &&
        ClosedStages.Contains(tender.Status) && (a.Roles.Contains(ApplicationRoles.SourcingOfficer) ||
        a.Roles.Contains(ApplicationRoles.SourcingManager) || a.Roles.Contains(ApplicationRoles.EvaluationCommittee) && a.EvaluationTenders.Contains(tender.Id) ||
        a.Roles.Contains(ApplicationRoles.TopCommittee) && tender.Status is (TenderStatus.AwaitingCommitteeDecision or TenderStatus.Awarded) && a.TopTenders.Contains(tender.Id)));
    public static void Require([DoesNotReturnIf(false)] bool valid, int status, string message) { if (!valid) throw new BidException(status, message); }
    public static void Window(Tender tender, DateTimeOffset now) => Require(tender.Status == TenderStatus.Open &&
        now >= tender.SubmissionStart && now < tender.SubmissionDeadline, 409, "An Open tender within its submission period is required.");
    public static void Money(decimal value, bool positive) => Require((positive ? value > 0 : value >= 0) && value <= MaximumDecimal &&
        decimal.Round(value, 6) == value, 400, "Use a valid JOD amount with at most six decimal places; unit prices must be positive.");
    public static void Validate(BidDraftForm form, BidContext context)
    {
        Require(form is not null, 400, "Bid data is required.");
        Require(!string.IsNullOrWhiteSpace(form.DeliveryTimeframe) && !string.IsNullOrWhiteSpace(form.TechnicalDetails) &&
            !string.IsNullOrWhiteSpace(form.PaymentTerms) && form.OfferValidUntil.HasValue, 400, "Delivery, technical details, payment terms and offer validity are required.");
        Require(form.Items is not null && form.Charges is not null && form.Items.All(x => x is not null) && form.Charges.All(x => x is not null), 400, "Invalid bid items or charges.");
        var ids = form.Items.Select(x => x.ItemId).ToArray();
        Require(context.Items.Count > 0 && context.Items.All(x => x.Quantity > 0) && ids.Length == context.Items.Count &&
            ids.Distinct().Count() == ids.Length && ids.Order().SequenceEqual(context.Items.Select(x => x.Id).Order()), 400,
            "Include every requested item exactly once; foreign, missing and duplicate identifiers are rejected.");
        foreach (var line in form.Items) Money(line.UnitPrice, true);
        foreach (var charge in form.Charges) { Money(charge.Amount, false); Require(!string.IsNullOrWhiteSpace(charge.CostType) && charge.CostType.Trim().Length <= 128, 400, "Each additional cost requires a type of at most 128 characters."); }
    }
    public static void Ready(BidSnapshot snapshot, BidContext context, DateTimeOffset now)
    {
        var bid = snapshot.Bid;
        Require(bid.Status == BidStatus.Draft && bid.SubmittedAt is null, 409, "Only a Draft may be submitted once.");
        Require(bid.Currency == "JOD" && snapshot.Charges.All(x => x.Currency == "JOD"), 400, "JOD is the only permitted currency.");
        Validate(new BidDraftForm { DeliveryTimeframe = bid.DeliveryTimeframe, TechnicalDetails = bid.TechnicalDetails, PaymentTerms = bid.PaymentTerms,
            OfferValidUntil = bid.OfferValidUntil, Items = snapshot.Items.Select(x => new BidLineForm { ItemId = x.PurchaseRequisitionItemId ?? Guid.Empty, UnitPrice = x.UnitPrice }).ToList(),
            Charges = snapshot.Charges.Select(x => new BidChargeForm { CostType = x.CostType, Amount = x.Amount }).ToList() }, context);
        Require(snapshot.Items.All(x => x.BidId == bid.Id && context.Items.Any(i => i.Id == x.PurchaseRequisitionItemId && i.Quantity == x.OfferedQuantity)), 400, "Stored bid quantities do not match the requested items.");
        Require(snapshot.Charges.All(x => x.BidId == bid.Id), 400, "Invalid additional cost ownership.");
        // Metadata prerequisites complement actual private-file verification during submission.
        Require(snapshot.Documents.Count is > 0 and <= 10 && snapshot.Documents.Any(x => x.DocumentType == "Technical Proposal"), 400, "At least one Technical Proposal document and at most ten attachments are required.");
        Require(snapshot.Documents.All(x => x.BidId == bid.Id && x.DocumentType is "Technical Proposal" or "Financial Proposal" or "Supporting Document" &&
            x.Size is > 0 and <= SupplierUploadValidation.MaximumBytes && x.ContentType is "application/pdf" or "image/jpeg" or "image/png" && !string.IsNullOrWhiteSpace(x.StorageReference)), 400, "Bid document records are invalid.");
        Window(context.Tender, now);
        Require(TenderManagement.Approved(context.Criteria), 409, "Approved evaluation criteria are required.");
        _ = Total(snapshot);
    }
    public static decimal Total(BidSnapshot snapshot)
    {
        try { return checked(snapshot.Items.Aggregate(0m, (sum, x) => checked(sum + checked(x.OfferedQuantity * x.UnitPrice))) + snapshot.Charges.Aggregate(0m, (sum, x) => checked(sum + x.Amount))); }
        catch (OverflowException) { throw new BidException(400, "Bid totals exceed supported decimal arithmetic."); }
    }
    public static string Version(BidSnapshot snapshot) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        snapshot.Bid, Items = snapshot.Items.OrderBy(x => x.Id), Charges = snapshot.Charges.OrderBy(x => x.Id), Documents = snapshot.Documents.OrderBy(x => x.Id) })));
}
