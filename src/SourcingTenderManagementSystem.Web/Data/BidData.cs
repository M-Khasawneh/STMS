using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IBidTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
// A narrow persistence boundary permits service verification without connecting to SQL Server.
public interface IBidData
{
    Task<BidAccount?> AccountAsync(ClaimsPrincipal principal, DateTimeOffset now, CancellationToken ct);
    Task<IBidTransaction> BeginAsync(Guid tenderId, CancellationToken ct);
    Task<BidContext?> ContextAsync(Guid tenderId, CancellationToken ct);
    Task<BidParticipation?> ParticipationAsync(Guid tenderId, Guid supplierId, CancellationToken ct);
    Task<BidSnapshot?> ReadAsync(Guid id, BidAccount account, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<Bid>> ListAsync(BidAccount account, DateTimeOffset now, CancellationToken ct);
    Task<bool> ExistsAsync(Guid participationId, Guid? except, CancellationToken ct);
    Task PersistAsync(BidSnapshot snapshot, bool create, CancellationToken ct);
    Task RecordActivityAsync(AuditLog activity, Notification? receipt, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> HistoryAsync(Guid bidId, CancellationToken ct);
    Task<string?> SupplierNameAsync(Guid bidId, BidAccount account, DateTimeOffset now, CancellationToken ct);
}
public interface IBidDocumentData : IBidData
{
    Task SaveDocumentsAsync(Guid bidId, IReadOnlyList<BidDocument> documents, CancellationToken ct);
    Task<bool> ReferenceExistsAsync(string reference, CancellationToken ct);
}
public sealed class BidData(ApplicationDbContext db, UserManager<IdentityUser> users) : IBidDocumentData
{
    public async Task<BidAccount?> AccountAsync(ClaimsPrincipal principal, DateTimeOffset now, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        // Never reuse Identity's previously tracked user when rechecking a transactional write.
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return null;
        var profile = await db.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id, ct);
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var members = await (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId } equals new { CommitteeAssignmentId = a.Id, a.TenderId }
            where m.UserId == user.Id && (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo)
            select new { m.TenderId, a.Kind }).ToListAsync(ct);
        return new(user.Id, roles, profile?.Id, profile?.Status == SupplierStatus.Approved, !await users.IsLockedOutAsync(user),
            await db.EmployeeDepartments.AnyAsync(x => x.UserId == user.Id, ct), members.Where(x => x.Kind == CommitteeKind.Evaluation).Select(x => x.TenderId).ToArray(), members.Where(x => x.Kind == CommitteeKind.TopCommittee).Select(x => x.TenderId).ToArray());
    }
    public async Task<IBidTransaction> BeginAsync(Guid tenderId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try {
            if (!await db.Tenders.FromSqlInterpolated($"SELECT * FROM dbo.Tenders WITH (UPDLOCK,HOLDLOCK) WHERE Id={tenderId}").AnyAsync(ct)) throw new BidException(404, "Tender not found.");
            return new Transaction(transaction, db);
        } catch { await transaction.DisposeAsync(); db.ChangeTracker.Clear(); throw; }
    }
    public async Task<BidContext?> ContextAsync(Guid tenderId, CancellationToken ct)
    {
        var tender = await db.Tenders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenderId, ct);
        if (tender is null) return null;
        var source = await db.TenderPurchaseRequisitions.AsNoTracking().Where(x => x.TenderId == tenderId).Select(x => (Guid?)x.PurchaseRequisitionId).SingleOrDefaultAsync(ct);
        if (source is null) throw new BidException(409, "Tender source is unavailable.");
        return new(tender, await db.PurchaseRequisitionItems.AsNoTracking().Where(x => x.PurchaseRequisitionId == source).ToListAsync(ct), await db.EvaluationCriteria.AsNoTracking().Where(x => x.TenderId == tenderId).ToListAsync(ct));
    }
    public async Task<BidParticipation?> ParticipationAsync(Guid tenderId, Guid supplierId, CancellationToken ct)
    {
        return await (from l in db.TenderSuppliers.AsNoTracking() join i in db.Invitations.AsNoTracking() on l.Id equals i.TenderSupplierId
            where l.TenderId == tenderId && l.SupplierProfileId == supplierId select new BidParticipation(l, i)).SingleOrDefaultAsync(ct);
    }
    public IQueryable<Bid> VisibleQuery(BidAccount a, DateTimeOffset now)
    {
        var query = db.Bids.AsNoTracking();
        if (a.Supplier) return query.Where(b => db.TenderSuppliers.Any(l => l.Id == b.TenderSupplierId && l.TenderId == b.TenderId && l.SupplierProfileId == a.SupplierId));
        if (!a.Internal) return query.Where(b => false);
        var officer = a.Roles.Contains(ApplicationRoles.SourcingOfficer); var manager = a.Roles.Contains(ApplicationRoles.SourcingManager);
        var evaluator = a.Roles.Contains(ApplicationRoles.EvaluationCommittee); var top = a.Roles.Contains(ApplicationRoles.TopCommittee);
        return query.Where(b => b.Status != BidStatus.Draft && b.SubmittedAt != null && db.Tenders.Any(t => t.Id == b.TenderId && BidRules.ClosedStages.Contains(t.Status) &&
            (officer || manager || evaluator && db.CommitteeMembers.Any(m => m.TenderId == t.Id && m.UserId == a.UserId &&
                (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo) &&
                db.CommitteeAssignments.Any(c => c.Id == m.CommitteeAssignmentId && c.TenderId == t.Id && c.Kind == CommitteeKind.Evaluation)) ||
            top && (t.Status == TenderStatus.AwaitingCommitteeDecision || t.Status == TenderStatus.Awarded) && db.CommitteeMembers.Any(m => m.TenderId == t.Id && m.UserId == a.UserId &&
                (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo) &&
                db.CommitteeAssignments.Any(c => c.Id == m.CommitteeAssignmentId && c.TenderId == t.Id && c.Kind == CommitteeKind.TopCommittee)))));
    }
    public async Task<BidSnapshot?> ReadAsync(Guid id, BidAccount account, DateTimeOffset now, CancellationToken ct)
    {
        var bid = await VisibleQuery(account, now).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (bid is null) return null;
        return new(bid, await db.BidItems.AsNoTracking().Where(x => x.BidId == id).ToListAsync(ct),
            await db.BidCostComponents.AsNoTracking().Where(x => x.BidId == id).ToListAsync(ct), await db.BidDocuments.AsNoTracking().Where(x => x.BidId == id).ToListAsync(ct));
    }
    public async Task<IReadOnlyList<Bid>> ListAsync(BidAccount account, DateTimeOffset now, CancellationToken ct) => await VisibleQuery(account, now).OrderBy(x => x.Id).ToListAsync(ct);
    public Task<bool> ExistsAsync(Guid participationId, Guid? except, CancellationToken ct) => db.Bids.AnyAsync(x => x.TenderSupplierId == participationId && x.Id != except, ct);
    public async Task PersistAsync(BidSnapshot snapshot, bool create, CancellationToken ct)
    {
        if (create) db.Bids.Add(snapshot.Bid);
        else {
            var existing = await db.Bids.SingleAsync(x => x.Id == snapshot.Bid.Id, ct);
            db.Entry(existing).CurrentValues.SetValues(snapshot.Bid);
            db.BidItems.RemoveRange(await db.BidItems.Where(x => x.BidId == existing.Id).ToListAsync(ct));
            db.BidCostComponents.RemoveRange(await db.BidCostComponents.Where(x => x.BidId == existing.Id).ToListAsync(ct));
            // Flush deletes inside this transaction before adding replacement draft rows with retained keys.
            await db.SaveChangesAsync(ct);
        }
        db.BidItems.AddRange(snapshot.Items); db.BidCostComponents.AddRange(snapshot.Charges);
        await db.SaveChangesAsync(ct);
    }
    public async Task SaveDocumentsAsync(Guid bidId, IReadOnlyList<BidDocument> documents, CancellationToken ct)
    {
        BidRules.Require(documents.All(x => x.BidId == bidId) && documents.Select(x => x.Id).Distinct().Count() == documents.Count, 400, "Invalid document ownership.");
        var rows = await db.BidDocuments.Where(x => x.BidId == bidId).ToListAsync(ct);
        foreach (var row in rows.Where(x => !documents.Any(d => d.Id == x.Id))) db.BidDocuments.Remove(row);
        foreach (var document in documents)
        {
            var existing = rows.SingleOrDefault(x => x.Id == document.Id);
            if (existing is null) db.BidDocuments.Add(document);
            else db.Entry(existing).CurrentValues.SetValues(document);
        }
        await db.SaveChangesAsync(ct);
    }
    public Task<bool> ReferenceExistsAsync(string reference, CancellationToken ct) => db.BidDocuments.AsNoTracking().AnyAsync(x => x.StorageReference == reference, ct);
    public Task<string?> SupplierNameAsync(Guid bidId, BidAccount account, DateTimeOffset now, CancellationToken ct) =>
        (from bid in VisibleQuery(account, now)
         join link in db.TenderSuppliers on new { Id = bid.TenderSupplierId, bid.TenderId } equals new { link.Id, link.TenderId }
         join supplier in db.SupplierProfiles on link.SupplierProfileId equals supplier.Id
         where bid.Id == bidId
         select (string?)supplier.CompanyName).SingleOrDefaultAsync(ct);

    public async Task RecordActivityAsync(AuditLog activity, Notification? receipt, CancellationToken ct)
    {
        db.AuditLogs.Add(activity);
        if (receipt is not null) db.Notifications.Add(receipt);
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<AuditLog>> HistoryAsync(Guid bidId, CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking()
            .Where(x => x.RecordType == "Bid" && x.RecordId == bidId.ToString())
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);

    private sealed class Transaction(IDbContextTransaction transaction, ApplicationDbContext context) : IBidTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public async ValueTask DisposeAsync() { await transaction.DisposeAsync(); context.ChangeTracker.Clear(); }
    }
}
