using System.Globalization;
using System.Security.Claims;
using System.Text;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;

namespace SourcingTenderManagementSystem.Web.Data;

// Supplier-only composition of existing application services. Posted display/ownership fields are never bound.
public sealed class SupplierBidPages(IBidData data, BidManagement bids, TimeProvider clock)
{
    public async Task RequireSupplierAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var account = await data.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(account?.Supplier == true, 403, "An approved supplier account is required.");
    }
    public async Task<IReadOnlyList<SupplierOfferRow>> ListAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        await RequireSupplierAsync(user, ct);
        var result = new List<SupplierOfferRow>();
        foreach (var bid in await bids.ListAsync(user, ct))
        {
            var snapshot = await bids.Read(user, bid.Id, ct);
            var context = await bids.Context(bid.TenderId, ct);
            result.Add(new(bid, context.Tender, await EditReason(user, snapshot, context, ct) is null));
        }
        return result.OrderByDescending(x => x.Bid.SubmittedAt).ThenBy(x => x.Tender.Title).ThenBy(x => x.Bid.Id).ToArray();
    }
    public async Task<SupplierBidDetails> DetailsAsync(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        await RequireSupplierAsync(user, ct);
        var details = await bids.DetailsAsync(user, id, ct);
        var context = await bids.Context(details.Snapshot.Bid.TenderId, ct);
        var reason = await EditReason(user, details.Snapshot, context, ct);
        return new(details, context.Tender, reason is null, reason);
    }
    public async Task<SupplierBidEditor> CreateAsync(ClaimsPrincipal user, Guid tenderId, BidDraftForm? posted, CancellationToken ct)
    {
        await RequireSupplierAsync(user, ct);
        var context = await bids.Context(tenderId, ct);
        var account = (await data.AccountAsync(user, clock.GetUtcNow(), ct))!;
        var participation = await bids.Participate(account, context, ct);
        BidRules.Require(!await data.ExistsAsync(participation.Link.Id, null, ct), 409, "An offer already exists. Open My Offers to continue your existing bid.");
        return Editor(null, context, posted ?? new(), []);
    }
    public async Task<SupplierBidEditor> EditAsync(ClaimsPrincipal user, Guid id, BidDraftForm? posted, CancellationToken ct)
    {
        await RequireSupplierAsync(user, ct);
        var details = await bids.DetailsAsync(user, id, ct);
        var context = await bids.Context(details.Snapshot.Bid.TenderId, ct);
        await bids.OwnDraft(user, details.Snapshot, context, ct);
        var s = details.Snapshot;
        var form = posted ?? new BidDraftForm { Version = details.Version, DeliveryTimeframe = s.Bid.DeliveryTimeframe,
            TechnicalDetails = s.Bid.TechnicalDetails, PaymentTerms = s.Bid.PaymentTerms, OfferValidUntil = s.Bid.OfferValidUntil,
            Items = s.Items.Select(x => new BidLineForm { ItemId = x.PurchaseRequisitionItemId ?? Guid.Empty, UnitPrice = x.UnitPrice }).ToList(),
            Charges = s.Charges.Select(x => new BidChargeForm { CostType = x.CostType, Amount = x.Amount, Notes = x.Notes }).ToList() };
        return Editor(id, context, form, s.Documents);
    }
    private static SupplierBidEditor Editor(Guid? id, BidContext context, BidDraftForm form, IReadOnlyList<BidDocument> documents)
    {
        var items = context.Items.OrderBy(x => x.Id).ToArray();
        // Only redisplay after the posted form has already been validated/rejected; never repair identifiers before saving.
        form.Items = items.Select(x => new BidLineForm { ItemId = x.Id,
            UnitPrice = form.Items?.FirstOrDefault(i => i is not null && i.ItemId == x.Id)?.UnitPrice ?? 0 }).ToList();
        return new() { Id = id, Tender = context.Tender, RequestedItems = items, Form = form, Documents = documents };
    }
    private async Task<string?> EditReason(ClaimsPrincipal user, BidSnapshot snapshot, BidContext context, CancellationToken ct)
    {
        try { await bids.OwnDraft(user, snapshot, context, ct); return null; }
        catch (BidException e) when (e.Status is 403 or 409) { return e.Message; }
    }
    public async Task<SupplierBidConfirmation> ConfirmationAsync(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var offer = await DetailsAsync(user, id, ct);
        if (!offer.CanEdit) return new(offer, offer.ReadOnlyReason);
        try { await bids.PreviewSubmissionAsync(user, id, offer.Details.Version, ct); return new(offer, null); }
        catch (BidException e) when (e.Status is 400 or 409 or 503) { return new(offer, e.Message); }
    }
    public async Task<byte[]> SummaryAsync(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var page = await DetailsAsync(user, id, ct); var s = page.Details.Snapshot;
        static string Number(decimal number) => number.ToString("0.############", CultureInfo.InvariantCulture);
        var text = new StringBuilder("STMS — Personal Offer Summary\n");
        text.AppendLine($"Offer: {id:D}").AppendLine($"Tender: {page.Tender.Title}").AppendLine($"Tender ID: {page.Tender.Id:D}")
            .AppendLine($"Status: {s.Bid.Status}").AppendLine($"Submitted: {(s.Bid.SubmittedAt.HasValue ? TenderTime.Display(s.Bid.SubmittedAt.Value) : "Not submitted")}")
            .AppendLine($"Deadline: {TenderTime.Display(page.Tender.SubmissionDeadline)}").AppendLine($"Generated: {TenderTime.Display(clock.GetUtcNow())}")
            .AppendLine("Currency: JOD").AppendLine($"Payment terms: {s.Bid.PaymentTerms}").AppendLine($"Offer valid until: {s.Bid.OfferValidUntil:yyyy-MM-dd}")
            .AppendLine($"Delivery timeframe: {s.Bid.DeliveryTimeframe}").AppendLine($"Technical details: {s.Bid.TechnicalDetails}").AppendLine().AppendLine("ITEMS");
        foreach (var item in s.Items.OrderBy(x => x.Id))
            text.AppendLine(item.Description).AppendLine($"  Specifications: {item.Specifications}").AppendLine($"  Quantity: {Number(item.OfferedQuantity)}; unit price: {Number(item.UnitPrice)} JOD; total: {Number(item.OfferedQuantity * item.UnitPrice)} JOD");
        text.AppendLine().AppendLine("ADDITIONAL COSTS");
        foreach (var charge in s.Charges) text.AppendLine($"{charge.CostType}: {Number(charge.Amount)} JOD; {charge.Notes}");
        text.AppendLine($"Overall total: {Number(page.Details.Total)} JOD").AppendLine().AppendLine("DOCUMENTS (contents not embedded)");
        foreach (var doc in s.Documents.OrderBy(x => x.DocumentType)) text.AppendLine($"{doc.DocumentType}: {doc.OriginalName} ({doc.Size} bytes)");
        if (s.Bid.Status == BidStatus.Draft) text.AppendLine("DRAFT — this report is not evidence of submission.");
        return Encoding.UTF8.GetBytes(text.ToString());
    }
}
