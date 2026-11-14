using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed class CommitteeManagement(ApplicationDbContext db, IBidData bids, TimeProvider clock)
{
    private async Task<BidAccount> Manager(ClaimsPrincipal principal, CancellationToken ct)
    {
        var actor = await bids.AccountAsync(principal, clock.GetUtcNow(), ct);
        BidRules.Require(actor is not null && CommitteeRules.Manager(actor), 403, "An enabled Sourcing Manager employee account is required.");
        return actor;
    }
    public async Task<CommitteePage> PageAsync(ClaimsPrincipal user, Guid tenderId, CancellationToken ct = default)
    {
        await Manager(user, ct);
        var context = await bids.ContextAsync(tenderId, ct) ?? throw new BidException(404, "Tender not found.");
        BidRules.Require(await db.CommitteeAssignments.CountAsync(x => x.TenderId == tenderId && x.Kind == CommitteeKind.Evaluation, ct) <= 1,
            409, "Multiple evaluation committees already exist for this tender. Resolve the data conflict before editing.");
        var members = await Members(tenderId, ct);
        var started = await Started(tenderId, ct);
        var candidates = await Candidates(ct);
        var now = clock.GetUtcNow();
        var active = members.Where(x => CommitteeRules.Effective(x, now)).ToArray();
        ValidateExisting(members, active, now);
        string Seat(int i) => active.SingleOrDefault(x => x.Position == CommitteeRules.Positions[i])?.UserId ?? "";
        var names = await db.Users.AsNoTracking().Where(x => members.Select(m => m.UserId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? x.Id, ct);
        var form = new CommitteeForm { Version = CommitteeRules.Version(members, started), ManagerUserId = Seat(0), DepartmentRepresentativeId = Seat(1), FinancialRepresentativeId = Seat(2), TechnicalRepresentativeId = Seat(3) };
        var changes = await db.AuditLogs.AsNoTracking().Where(x => x.RecordType == "EvaluationCommittee" && x.RecordId == tenderId.ToString())
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
        await Manager(user, ct);
        var sourceDepartment = await SourceDepartment(tenderId, ct);
        return new(context.Tender, sourceDepartment, form, started, candidates, members.Select(m => new CommitteeHistory(m, names.GetValueOrDefault(m.UserId, m.UserId))).ToArray(), changes);
    }
    public async Task SaveAsync(ClaimsPrincipal user, Guid tenderId, CommitteeForm form, CancellationToken ct = default)
    {
        await Manager(user, ct);
        await BidManagement.Mutate(async () =>
        {
            await using var tx = await bids.BeginAsync(tenderId, ct);
            var actor = await Manager(user, ct);
            var context = await bids.ContextAsync(tenderId, ct) ?? throw new BidException(404, "Tender not found.");
            var assignments = await db.CommitteeAssignments.Where(x => x.TenderId == tenderId && x.Kind == CommitteeKind.Evaluation).ToListAsync(ct);
            BidRules.Require(assignments.Count <= 1, 409, "Multiple evaluation committees already exist for this tender. Resolve the data conflict before changing membership.");
            var members = await Members(tenderId, ct);
            var now = clock.GetUtcNow();
            var active = members.Where(x => CommitteeRules.Effective(x, now)).ToArray();
            ValidateExisting(members, active, now);
            var started = await Started(tenderId, ct);
            ComplianceRules.Version(form.Version, CommitteeRules.Version(members, started));
            var sourceDepartment = await SourceDepartment(tenderId, ct);
            var selection = CommitteeRules.Selection(form);
            var unchanged = active.Length == 4 && active.All(x => selection[Array.IndexOf(CommitteeRules.Positions, x.Position)] == x.UserId);
            CommitteeRules.Validate(form, await Candidates(ct), sourceDepartment, started && !unchanged);
            if (unchanged) { await tx.CommitAsync(ct); return true; }
            var assignment = assignments.SingleOrDefault();
            if (assignment is null) {
                assignment = new CommitteeAssignment { Id = Guid.NewGuid(), TenderId = tenderId, Kind = CommitteeKind.Evaluation };
                db.CommitteeAssignments.Add(assignment);
            }
            for (var i = 0; i < CommitteeRules.Positions.Length; i++)
            {
                var position = CommitteeRules.Positions[i];
                var previous = active.SingleOrDefault(x => x.Position == position);
                if (previous?.UserId == selection[i]) continue;
                if (previous is not null) {
                    db.Attach(previous);
                    previous.EffectiveTo = now;
                    db.Entry(previous).Property(x => x.EffectiveTo).IsModified = true;
                }
                db.CommitteeMembers.Add(new CommitteeMember { Id = Guid.NewGuid(), TenderId = tenderId, CommitteeAssignmentId = assignment.Id,
                    UserId = selection[i], Position = position, EffectiveFrom = now });
                db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = actor.UserId, RecordType = "EvaluationCommittee", RecordId = tenderId.ToString(),
                    Action = previous is null ? "MemberAssigned" : "MemberReplaced", PreviousStatus = previous?.UserId, NewStatus = selection[i],
                    Reason = $"{position}. {form.Reason?.Trim()}", OccurredAt = now });
            }
            await db.SaveChangesAsync(ct);
            await Manager(user, ct);
            CommitteeRules.Validate(form, await Candidates(ct), sourceDepartment, started);
            await tx.CommitAsync(ct);
            return true;
        });
    }
    private async Task<List<CommitteeMember>> Members(Guid id, CancellationToken ct) => await
        (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId }
            equals new { CommitteeAssignmentId = a.Id, a.TenderId }
         where m.TenderId == id && a.Kind == CommitteeKind.Evaluation orderby m.EffectiveFrom, m.Id select m).ToListAsync(ct);
    private async Task<bool> Started(Guid id, CancellationToken ct) =>
        await db.TechnicalEvaluations.AnyAsync(x => x.TenderId == id, ct) || await db.FinancialEvaluations.AnyAsync(x => x.TenderId == id, ct);
    private async Task<List<CommitteeCandidate>> Candidates(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await (from user in db.Users.AsNoTracking() join employee in db.EmployeeDepartments on user.Id equals employee.UserId
            where (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= now) && !db.SupplierProfiles.Any(x => x.UserId == user.Id)
            orderby user.UserName select new { user.Id, Name = user.UserName ?? user.Id, employee.DepartmentId }).ToListAsync(ct);
        var roles = await (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
            select new { membership.UserId, role.Name }).ToListAsync(ct);
        return rows.Select(x => new CommitteeCandidate(x.Id, x.Name, x.DepartmentId, roles.Where(r => r.UserId == x.Id).Select(r => r.Name!).ToArray()))
            .Where(x => !x.Roles.Contains(ApplicationRoles.Admin) && !x.Roles.Contains(ApplicationRoles.Supplier) &&
                (x.Roles.Contains(ApplicationRoles.SourcingManager) || x.Roles.Contains(ApplicationRoles.EvaluationCommittee))).ToList();
    }
    private Task<Guid> SourceDepartment(Guid id, CancellationToken ct) => (from link in db.TenderPurchaseRequisitions
        join request in db.PurchaseRequisitions on link.PurchaseRequisitionId equals request.Id
        where link.TenderId == id select request.DepartmentId).SingleAsync(ct);
    private static void ValidateExisting(IReadOnlyList<CommitteeMember> all, IReadOnlyList<CommitteeMember> active, DateTimeOffset now)
    {
        BidRules.Require(all.All(x => CommitteeRules.Positions.Contains(x.Position)) &&
            all.All(x => x.EffectiveTo is null || x.EffectiveFrom is null || x.EffectiveFrom <= x.EffectiveTo) &&
            active.Select(x => x.Position).Distinct().Count() == active.Count && active.Select(x => x.UserId).Distinct().Count() == active.Count,
            409, "Existing committee membership conflicts with the approved positions or distinct-member rule. Resolve the data conflict before editing.");
        BidRules.Require(!all.Any(x => x.EffectiveFrom > now), 409, "Scheduled committee assignments require review before editing.");
    }
}
