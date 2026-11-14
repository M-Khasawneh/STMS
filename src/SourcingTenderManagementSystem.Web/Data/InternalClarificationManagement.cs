using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Clarifications;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.Web.Data;

public interface IInternalClarifications
{
    Task<bool> AvailableAsync(ClaimsPrincipal user, bool requisition, Guid id, CancellationToken ct);
    Task<InternalInbox> InboxAsync(ClaimsPrincipal user, int page, CancellationToken ct);
    Task<InternalClarificationPage> PageAsync(ClaimsPrincipal user, bool requisition, Guid id, CancellationToken ct);
    Task AskAsync(ClaimsPrincipal user, bool requisition, Guid id, InternalQuestionForm form, CancellationToken ct);
    Task ReplyAsync(ClaimsPrincipal user, bool requisition, Guid id, Guid requestId, InternalReplyForm form, CancellationToken ct);
    Task ResolveAsync(ClaimsPrincipal user, bool requisition, Guid id, Guid requestId, string version, CancellationToken ct);
}
// Internal conversations never use BidId: supplier compliance and its evaluation gate stay unchanged.
public sealed class InternalClarificationManagement(ApplicationDbContext db, IBidData bids, TimeProvider clock) : IInternalClarifications
{
    private async Task<List<ClarificationPerson>> People(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await (from u in db.Users.AsNoTracking()
                          join e in db.EmployeeDepartments on u.Id equals e.UserId
                          where (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) && !db.SupplierProfiles.Any(s => s.UserId == u.Id)
                          orderby u.Id
                          select new { u.Id, Name = u.UserName ?? u.Id, e.DepartmentId }).ToListAsync(ct);
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id orderby r.Name select new { ur.UserId, r.Name }).ToListAsync(ct);
        return rows.Select(u => new ClarificationPerson(u.Id, u.Name, u.DepartmentId, roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).ToArray())).Where(InternalClarificationRules.Employee).ToList();
    }
    private static ClarificationPerson Actor(ClaimsPrincipal user, IReadOnlyList<ClarificationPerson> people)
    {
        var actor = people.SingleOrDefault(p => user.Identity?.IsAuthenticated == true && p.Id == user.FindFirstValue(ClaimTypes.NameIdentifier));
        return actor ?? throw new BidException(403, "An eligible internal employee account is required.");
    }
    private async Task<InternalClarificationContext> Context(bool requisition, Guid id, CancellationToken ct)
    {
        if (requisition)
        {
            var q = await db.PurchaseRequisitions.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id, ct) ?? throw new BidException(404, "Requisition not found.");
            var reviewer = await db.PurchaseRequisitionReviews.AsNoTracking().Where(r => r.PurchaseRequisitionId == id).OrderByDescending(r => r.ReviewedAt).ThenByDescending(r => r.Id).Select(r => r.ReviewerUserId).FirstOrDefaultAsync(ct);
            return new(id, true, q.Title, q, null, q.DepartmentId, reviewer, [], []);
        }
        var t = await db.Tenders.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new BidException(404, "Tender not found.");
        var department = await (from l in db.TenderPurchaseRequisitions join q in db.PurchaseRequisitions on l.PurchaseRequisitionId equals q.Id where l.TenderId == id select (Guid?)q.DepartmentId).SingleOrDefaultAsync(ct);
        BidRules.Require(department.HasValue, 409, "Tender source is unavailable.");
        var assignments = await db.CommitteeAssignments.AsNoTracking().Where(a => a.TenderId == id).ToListAsync(ct);
        BidRules.Require(assignments.GroupBy(a => a.Kind).All(g => g.Count() == 1), 409, "Multiple committees of the same kind exist for this tender. Resolve the conflict first.");
        var members = await (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId } equals new { CommitteeAssignmentId = a.Id, a.TenderId } where m.TenderId == id select new { Member = m, a.Kind }).ToListAsync(ct);
        var evaluation = members.Where(m => m.Kind == CommitteeKind.Evaluation).Select(m => m.Member).OrderBy(m => m.Id).ToArray();
        var top = members.Where(m => m.Kind == CommitteeKind.TopCommittee).Select(m => m.Member).OrderBy(m => m.Id).ToArray();
        foreach (var (group, evaluationKind) in new[] { (evaluation, true), (top, false) })
        {
            var active = group.Where(m => CommitteeRules.Effective(m, clock.GetUtcNow())).ToArray();
            BidRules.Require(active.Select(m => m.UserId).Distinct().Count() == active.Length && (!evaluationKind || active.Select(m => m.Position).Distinct().Count() == active.Length), 409, "Committee membership records conflict. Resolve the conflict before using clarifications.");
        }
        return new(id, false, t.Title, null, t, department!.Value, null, evaluation, top);
    }
    private IQueryable<ClarificationRequest> Questions(bool req, Guid id) => db.ClarificationRequests.AsNoTracking().Where(q => q.BidId == null && (req ? q.PurchaseRequisitionId == id && q.TenderId == null : q.TenderId == id && q.PurchaseRequisitionId == null));
    private async Task<(InternalClarificationContext Context, List<ClarificationPerson> People, ClarificationPerson Actor, List<ClarificationRequest> Questions, List<ClarificationResponse> Responses)> Load(ClaimsPrincipal user, bool req, Guid id, CancellationToken ct)
    {
        var people = await People(ct); var actor = Actor(user, people); var context = await Context(req, id, ct);
        var qs = await Questions(req, id).Where(q => q.SenderUserId == actor.Id || q.RecipientUserId == actor.Id).OrderBy(q => q.CreatedAt).ThenBy(q => q.Id).ToListAsync(ct);
        var ids = qs.Select(q => q.Id).ToArray(); var rs = await db.ClarificationResponses.AsNoTracking().Where(r => ids.Contains(r.ClarificationRequestId)).ToListAsync(ct);
        BidRules.Require(rs.GroupBy(r => r.ClarificationRequestId).All(g => g.Count() == 1), 409, "Multiple responses exist for an internal question. Resolve the conflict first.");
        BidRules.Require(qs.All(q => q.Status == ClarificationStatus.Pending ? !rs.Any(r => r.ClarificationRequestId == q.Id) :
            q.Status is ClarificationStatus.Answered or ClarificationStatus.Resolved && rs.Any(r => r.ClarificationRequestId == q.Id && r.ResponderUserId == q.RecipientUserId)), 409, "Question and response states conflict. Resolve the conflict first.");
        return (context, people, actor, qs, rs);
    }
    private bool Pair(InternalClarificationContext c, IReadOnlyList<ClarificationPerson> people, string a, string b, bool opening)
    {
        var x = people.SingleOrDefault(p => p.Id == a); var y = people.SingleOrDefault(p => p.Id == b);
        return x != null && y != null && InternalClarificationRules.Allowed(c, x, y, clock.GetUtcNow(), opening);
    }
    public async Task<bool> AvailableAsync(ClaimsPrincipal user, bool requisition, Guid id, CancellationToken ct)
    {
        try { await PageAsync(user, requisition, id, ct); return true; }
        catch (BidException e) when (e.Status == 403) { return false; }
    }
    public async Task<InternalClarificationPage> PageAsync(ClaimsPrincipal user, bool requisition, Guid id, CancellationToken ct)
    {
        var s = await Load(user, requisition, id, ct);
        var recipients = s.People.Where(p => Pair(s.Context, s.People, s.Actor.Id, p.Id, true)).OrderBy(p => p.Name).ToArray();
        var threads = s.Questions.Where(q => Pair(s.Context, s.People, q.SenderUserId, q.RecipientUserId, false)).ToArray();
        BidRules.Require(recipients.Length > 0 || threads.Length > 0, 403, "No authorized clarification participants are available for this record.");
        var version = InternalClarificationRules.Version(s.Context, s.People, s.Questions, s.Responses);
        return new(s.Context, recipients, threads.Select(q => new InternalThread(q, s.Responses.SingleOrDefault(r => r.ClarificationRequestId == q.Id), s.People.Single(p => p.Id == q.SenderUserId).Name, s.People.Single(p => p.Id == q.RecipientUserId).Name, version, q.RecipientUserId == s.Actor.Id && q.Status == ClarificationStatus.Pending, q.SenderUserId == s.Actor.Id && q.Status == ClarificationStatus.Answered)).ToArray(), version);
    }
    public async Task<InternalInbox> InboxAsync(ClaimsPrincipal user, int page, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 100000); var people = await People(ct); var actor = Actor(user, people);
        var qs = await db.ClarificationRequests.AsNoTracking().Where(q => q.BidId == null && (q.PurchaseRequisitionId != null || q.TenderId != null) && (q.SenderUserId == actor.Id || q.RecipientUserId == actor.Id)).OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id).Skip((page - 1) * 25).Take(26).ToListAsync(ct);
        var items = new List<InternalInboxItem>();
        foreach (var q in qs.Take(25))
        {
            var c = await Context(q.PurchaseRequisitionId.HasValue, q.PurchaseRequisitionId ?? q.TenderId!.Value, ct);
            if (Pair(c, people, q.SenderUserId, q.RecipientUserId, false)) items.Add(new(q, c.Title));
        }
        return new(items, page, qs.Count > 25);
    }
    public Task AskAsync(ClaimsPrincipal user, bool requisition, Guid id, InternalQuestionForm form, CancellationToken ct)
    {
        ComplianceRules.Text(form.Subject, 256, "Subject"); ComplianceRules.Text(form.Description, 8000, "Question");
        return Change(user, requisition, id, form.Version, async (c, people, actor) =>
        {
            BidRules.Require(Pair(c, people, actor.Id, form.RecipientId, true), 403, "The selected recipient is not eligible for this record and stage.");
            var q = new ClarificationRequest { Id = Guid.NewGuid(), SenderUserId = actor.Id, RecipientUserId = form.RecipientId, PurchaseRequisitionId = requisition ? id : null, TenderId = requisition ? null : id, Subject = form.Subject.Trim(), Description = form.Description.Trim(), Status = ClarificationStatus.Pending, CreatedAt = clock.GetUtcNow() };
            db.ClarificationRequests.Add(q); Record(q, actor.Id, "Internal clarification requested", q.RecipientUserId); await Task.CompletedTask;
        }, ct);
    }
    public Task ReplyAsync(ClaimsPrincipal user, bool requisition, Guid id, Guid requestId, InternalReplyForm form, CancellationToken ct)
    {
        ComplianceRules.Text(form.Response, 8000, "Response");
        return Change(user, requisition, id, form.Version, async (c, people, actor) =>
        {
            var q = await Thread(requisition, id, requestId, actor.Id, c, people, ct); var has = await db.ClarificationResponses.AnyAsync(r => r.ClarificationRequestId == requestId, ct);
            InternalClarificationRules.Reply(q, actor.Id, has);
            db.ClarificationResponses.Add(new() { Id = Guid.NewGuid(), ClarificationRequestId = q.Id, ResponderUserId = actor.Id, Response = form.Response.Trim(), RespondedAt = clock.GetUtcNow() });
            db.Attach(q); q.Status = ClarificationStatus.Answered; Record(q, actor.Id, "Internal clarification answered", q.SenderUserId);
        }, ct);
    }
    public Task ResolveAsync(ClaimsPrincipal user, bool requisition, Guid id, Guid requestId, string version, CancellationToken ct) => Change(user, requisition, id, version, async (c, people, actor) =>
    {
        var q = await Thread(requisition, id, requestId, actor.Id, c, people, ct);
        InternalClarificationRules.Resolve(q, actor.Id, await db.ClarificationResponses.AnyAsync(r => r.ClarificationRequestId == q.Id, ct));
        db.Attach(q); q.Status = ClarificationStatus.Resolved; Record(q, actor.Id, "Internal clarification resolved", q.RecipientUserId);
    }, ct);
    private async Task<ClarificationRequest> Thread(bool req, Guid id, Guid thread, string actor, InternalClarificationContext c, List<ClarificationPerson> people, CancellationToken ct)
    {
        var q = await Questions(req, id).SingleOrDefaultAsync(q => q.Id == thread && (q.SenderUserId == actor || q.RecipientUserId == actor), ct) ?? throw new BidException(404, "Clarification not found.");
        BidRules.Require(Pair(c, people, q.SenderUserId, q.RecipientUserId, false), 403, "Clarification participants are no longer eligible."); return q;
    }
    private async Task Change(ClaimsPrincipal user, bool req, Guid id, string version, Func<InternalClarificationContext, List<ClarificationPerson>, ClarificationPerson, Task> action, CancellationToken ct)
    {
        await BidManagement.Mutate(async () =>
        {
            await using var tx = req ? await BeginRequest(id, ct) : await bids.BeginAsync(id, ct);
            var s = await Load(user, req, id, ct);
            ComplianceRules.Version(version, InternalClarificationRules.Version(s.Context, s.People, s.Questions, s.Responses));
            await action(s.Context, s.People, s.Actor);
            var affected = db.ChangeTracker.Entries<ClarificationRequest>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .Select(e => (Question: e.Entity, Opening: e.State == EntityState.Added)).ToArray();
            await db.SaveChangesAsync(ct);
            var fresh = await People(ct);
            Actor(user, fresh);
            var finalContext = await Context(req, id, ct);
            foreach (var change in affected)
                BidRules.Require(Pair(finalContext, fresh, change.Question.SenderUserId, change.Question.RecipientUserId, change.Opening),
                    403, "Clarification participants or stage changed before the operation completed.");
            // Serializable locks retain the role, account, membership and stage facts read by this operation.
            await tx.CommitAsync(ct); return true;
        });
    }
    private async Task<IBidTransaction> BeginRequest(Guid id, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (!await db.PurchaseRequisitions.FromSqlInterpolated($"SELECT * FROM dbo.PurchaseRequisitions WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").AnyAsync(ct)) throw new BidException(404, "Requisition not found.");
            return new RequestTransaction(tx, db);
        }
        catch { await tx.DisposeAsync(); db.ChangeTracker.Clear(); throw; }
    }
    private sealed class RequestTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, ApplicationDbContext db) : IBidTransaction
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);
        public async ValueTask DisposeAsync() { await tx.DisposeAsync(); db.ChangeTracker.Clear(); }
    }
    private void Record(ClarificationRequest q, string actor, string action, string recipient)
    {
        db.AuditLogs.Add(new() { Id = Guid.NewGuid(), ActorUserId = actor, RecordType = q.PurchaseRequisitionId.HasValue ? "PurchaseRequisition" : "Tender", RecordId = (q.PurchaseRequisitionId ?? q.TenderId)!.Value.ToString(), Action = action, NewStatus = q.Status.ToString(), Reason = $"Clarification {q.Id}", OccurredAt = clock.GetUtcNow() });
        db.Notifications.Add(new() { Id = Guid.NewGuid(), RecipientUserId = recipient, PurchaseRequisitionId = q.PurchaseRequisitionId, TenderId = q.TenderId, Subject = action, Message = "An internal clarification has been updated. Open the authorized conversation to view it.", CreatedAt = clock.GetUtcNow() });
    }
}
