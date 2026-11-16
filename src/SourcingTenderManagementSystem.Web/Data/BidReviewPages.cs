using System.Security.Claims;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed class BidReviewPages(IBidData data, BidManagement bids, TimeProvider clock)
{
    public async Task RequireReviewerAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var account = await data.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(account is { Internal: true } && account.Roles.Any(role => role is
            ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or
            ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee), 403, "Authorized bid review access is required.");
    }
    public async Task<IReadOnlyList<BidReviewRow>> ListAsync(ClaimsPrincipal user, Guid? tenderId, CancellationToken ct, int? page = null, string? search = null, string? status = null)
    {
        await RequireReviewerAsync(user, ct);
        if (data is BidData listing)
        {
            var account = (await data.AccountAsync(user, clock.GetUtcNow(), ct))!;
            var rows = await listing.ReviewRowsAsync(account, clock.GetUtcNow(), tenderId, page, search, status, ct);
            await RequireReviewerAsync(user, ct);
            var fresh = (await data.AccountAsync(user, clock.GetUtcNow(), ct))!;
            var visible = listing;
            var ids = rows.Select(x => x.Bid.Id).ToArray();
            BidRules.Require(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(visible.VisibleQuery(fresh, clock.GetUtcNow()).Where(b => ids.Contains(b.Id)), ct) == ids.Length, 403, "Offer access changed. Reload the list.");
            return rows;
        }
        var result = new List<BidReviewRow>();
        foreach (var bid in await bids.ListAsync(user, ct))
        {
            if (tenderId.HasValue && tenderId != bid.TenderId) continue;
            var current = await bids.Read(user, bid.Id, ct);
            var context = await bids.Context(current.Bid.TenderId, ct);
            var account = await data.AccountAsync(user, clock.GetUtcNow(), ct);
            BidRules.Require(account is not null, 403, "Authorized bid review access is required.");
            var supplier = await data.SupplierNameAsync(bid.Id, account, clock.GetUtcNow(), ct);
            await RequireReviewerAsync(user, ct);
            await bids.Read(user, bid.Id, ct);
            result.Add(new(current.Bid, context.Tender, supplier));
        }
        return result.OrderByDescending(x => x.Bid.SubmittedAt).ThenBy(x => x.Bid.Id).ToArray();
    }
    public async Task<SupplierBidDetails> DetailsAsync(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        await RequireReviewerAsync(user, ct);
        var details = await bids.DetailsAsync(user, id, ct);
        var context = await bids.Context(details.Snapshot.Bid.TenderId, ct);
        var account = await data.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(account is not null, 403, "Authorized bid review access is required.");
        var supplier = await data.SupplierNameAsync(id, account, clock.GetUtcNow(), ct);
        await RequireReviewerAsync(user, ct);
        await bids.Read(user, id, ct);
        return new(details, context.Tender, false, "Read-only submitted offer.", supplier);
    }
    public async Task<BidReviewComparison> CompareAsync(ClaimsPrincipal user, Guid tenderId, CancellationToken ct)
    {
        var rows = await ListAsync(user, tenderId, ct);
        BidRules.Require(rows.Count > 0, 404, "No submitted offers are available for this tender.");
        var offers = new List<SupplierBidDetails>();
        foreach (var row in rows) offers.Add(await DetailsAsync(user, row.Bid.Id, ct));
        var context = await bids.Context(tenderId, ct);
        return new(context.Tender, context.Items, offers);
    }
}
