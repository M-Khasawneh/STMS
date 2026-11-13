using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;
// Submission accepts only the injected private-file verification boundary.
public interface IBidSubmissionDocuments { Task VerifyAsync(BidSnapshot snapshot, CancellationToken ct); }
public sealed class BidSubmissionDocumentsPending : IBidSubmissionDocuments
{
    public Task VerifyAsync(BidSnapshot snapshot, CancellationToken ct) => throw new BidException(409, "Protected bid document verification is not available until Phase 2 is approved and implemented.");
}
public sealed class BidManagement(IBidData data, TimeProvider clock, IBidSubmissionDocuments documents)
{
    public async Task<IReadOnlyList<Bid>> ListAsync(ClaimsPrincipal user, CancellationToken ct = default) => await data.ListAsync(await Account(user, ct), clock.GetUtcNow(), ct);
    public async Task<BidDetails> DetailsAsync(ClaimsPrincipal user, Guid id, CancellationToken ct = default) => Details(await Read(user, id, ct));
    public async Task<Guid> CreateDraftAsync(ClaimsPrincipal user, Guid tenderId, BidDraftForm form, CancellationToken ct = default) =>
        await Mutate(async () => {
            await using var tx = await data.BeginAsync(tenderId, ct);
            var account = await Account(user, ct); var context = await Context(tenderId, ct);
            var participation = await Participate(account, context, ct);
            BidRules.Require(!await data.ExistsAsync(participation.Link.Id, null, ct), 409, "A bid already exists for this supplier and tender.");
            var snapshot = Apply(new(new Bid { Id = Guid.NewGuid(), TenderId = tenderId, TenderSupplierId = participation.Link.Id, Status = BidStatus.Draft }, [], [], []), form, context);
            await data.PersistAsync(snapshot, true, ct);
            await Participate(await Account(user, ct), context, ct); // Includes final deadline/account recheck.
            await tx.CommitAsync(ct); return snapshot.Bid.Id;
        });
    public async Task SaveDraftAsync(ClaimsPrincipal user, Guid id, BidDraftForm form, CancellationToken ct = default) =>
        await Mutate(async () => {
            var preliminary = await Read(user, id, ct);
            await using var tx = await data.BeginAsync(preliminary.Bid.TenderId, ct);
            var snapshot = await Read(user, id, ct); var context = await Context(snapshot.Bid.TenderId, ct);
            await OwnDraft(user, snapshot, context, ct); Version(snapshot, form.Version);
            await data.PersistAsync(Apply(snapshot, form, context), false, ct);
            await Participate(await Account(user, ct), context, ct);
            await tx.CommitAsync(ct); return true;
        });
    public async Task SubmitAsync(ClaimsPrincipal user, Guid id, string version, CancellationToken ct = default) =>
        await Mutate(async () => {
            var preliminary = await Read(user, id, ct);
            await using var tx = await data.BeginAsync(preliminary.Bid.TenderId, ct);
            var snapshot = await Read(user, id, ct); var context = await Context(snapshot.Bid.TenderId, ct);
            await CheckSubmission(user, snapshot, context, version, ct);
            snapshot.Bid.Status = BidStatus.Submitted; snapshot.Bid.SubmittedAt = clock.GetUtcNow().ToUniversalTime();
            await data.PersistAsync(snapshot, false, ct);
            await Participate(await Account(user, ct), context, ct);
            await tx.CommitAsync(ct); return true;
        });
    // Read-only confirmation uses the same validation and real-file verifier as final submission.
    // Final submission repeats everything in its own transaction; a preview never reserves eligibility.
    public async Task PreviewSubmissionAsync(ClaimsPrincipal user, Guid id, string version, CancellationToken ct = default) =>
        await Mutate(async () => {
            var preliminary = await Read(user, id, ct);
            await using var tx = await data.BeginAsync(preliminary.Bid.TenderId, ct);
            var snapshot = await Read(user, id, ct); var context = await Context(snapshot.Bid.TenderId, ct);
            await CheckSubmission(user, snapshot, context, version, ct);
            await Participate(await Account(user, ct), context, ct);
            await tx.CommitAsync(ct); return true;
        });
    private async Task CheckSubmission(ClaimsPrincipal user, BidSnapshot snapshot, BidContext context, string version, CancellationToken ct)
    {
        await OwnDraft(user, snapshot, context, ct); Version(snapshot, version);
        BidRules.Ready(snapshot, context, clock.GetUtcNow());
        await documents.VerifyAsync(snapshot, ct);
    }
    private async Task<BidAccount> Account(ClaimsPrincipal principal, CancellationToken ct)
    {
        var account = await data.AccountAsync(principal, clock.GetUtcNow(), ct);
        BidRules.Require(account is not null && (account.Supplier || account.Internal), 403, "An eligible account with bid access is required.");
        return account;
    }
    internal async Task<BidSnapshot> Read(ClaimsPrincipal user, Guid id, CancellationToken ct) =>
        await data.ReadAsync(id, await Account(user, ct), clock.GetUtcNow(), ct) ?? throw new BidException(404, "Bid not found.");
    internal async Task<BidContext> Context(Guid id, CancellationToken ct) => await data.ContextAsync(id, ct) ?? throw new BidException(404, "Tender not found.");
    internal async Task<BidParticipation> Participate(BidAccount account, BidContext context, CancellationToken ct)
    {
        BidRules.Require(account.Supplier, 403, "Only the owning approved supplier may participate.");
        BidRules.Window(context.Tender, clock.GetUtcNow());
        BidRules.Require(TenderManagement.Approved(context.Criteria), 409, "Approved evaluation criteria are required.");
        var participation = await data.ParticipationAsync(context.Tender.Id, account.SupplierId!.Value, ct);
        BidRules.Require(participation is not null && participation.Invitation.Status == InvitationStatus.Accepted, 403, "An accepted invitation is required.");
        return participation;
    }
    internal async Task OwnDraft(ClaimsPrincipal user, BidSnapshot snapshot, BidContext context, CancellationToken ct)
    {
        var participation = await Participate(await Account(user, ct), context, ct);
        BidRules.Require(participation.Link.Id == snapshot.Bid.TenderSupplierId, 404, "Bid not found.");
        BidRules.Require(snapshot.Bid.Status == BidStatus.Draft && snapshot.Bid.SubmittedAt is null, 409, "Submitted bids and documents are immutable.");
        BidRules.Require(!await data.ExistsAsync(participation.Link.Id, snapshot.Bid.Id, ct), 409, "Conflicting bid records require review.");
    }
    private static BidSnapshot Apply(BidSnapshot snapshot, BidDraftForm form, BidContext context)
    {
        BidRules.Validate(form, context);
        var bid = snapshot.Bid;
        bid.DeliveryTimeframe = form.DeliveryTimeframe.Trim(); bid.TechnicalDetails = form.TechnicalDetails.Trim(); bid.PaymentTerms = form.PaymentTerms.Trim(); bid.OfferValidUntil = form.OfferValidUntil; bid.Currency = "JOD";
        var items = context.Items.Select(item => new BidItem { Id = snapshot.Items.FirstOrDefault(x => x.PurchaseRequisitionItemId == item.Id)?.Id ?? Guid.NewGuid(), BidId = bid.Id,
            PurchaseRequisitionItemId = item.Id, Description = item.Description, Specifications = item.Specifications, OfferedQuantity = item.Quantity, UnitPrice = form.Items.Single(x => x.ItemId == item.Id).UnitPrice }).ToArray();
        var charges = form.Charges.Select(x => new BidCostComponent { Id = Guid.NewGuid(), BidId = bid.Id, CostType = x.CostType.Trim(), Amount = x.Amount, Currency = "JOD", Notes = x.Notes?.Trim() }).ToArray();
        var result = new BidSnapshot(bid, items, charges, snapshot.Documents); _ = BidRules.Total(result); return result;
    }
    internal static void Version(BidSnapshot snapshot, string? version) => BidRules.Require(!string.IsNullOrEmpty(version) && version == BidRules.Version(snapshot), 409, "This bid has changed. Reload before saving or submitting.");
    private static BidDetails Details(BidSnapshot snapshot) => new(snapshot, BidRules.Version(snapshot), BidRules.Total(snapshot));
    internal static async Task<T> Mutate<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception e) when ((e is SqlException sql && sql.Number is 1205 or 2601 or 2627) || e is DbUpdateConcurrencyException || e is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
        { throw new BidException(409, "A concurrent bid operation was rejected. Reload before trying again."); }
    }
}
