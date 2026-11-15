using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IRoleDashboard { Task<RoleDashboardModel> ReadAsync(ClaimsPrincipal principal, CancellationToken ct); }
public sealed class RoleDashboard(ApplicationDbContext db, UserManager<IdentityUser> users, IEmployeeEligibility eligibility, TimeProvider clock, ILogger<RoleDashboard> logger) : IRoleDashboard
{
    public IQueryable<PurchaseRequisition> OwnRequests(string id) => db.PurchaseRequisitions.AsNoTracking().Where(x => x.CreatedByUserId == id);
    public IQueryable<PurchaseRequisition> ReviewQueue(string id, Guid departmentId) => db.PurchaseRequisitions.AsNoTracking().Where(x => x.CreatedByUserId != id && x.DepartmentId != departmentId && (x.Status == PurchaseRequestStatus.Submitted || x.Status == PurchaseRequestStatus.UnderReview));
    public IQueryable<Bid> OwnOffers(Guid profileId) => db.Bids.AsNoTracking().Where(b => db.TenderSuppliers.Any(l => l.SupplierProfileId == profileId && l.Id == b.TenderSupplierId && l.TenderId == b.TenderId));
    public IQueryable<Tender> AssignedTenders(string id, CommitteeKind kind, DateTimeOffset now) => db.Tenders.AsNoTracking().Where(t =>
        (kind == CommitteeKind.Evaluation ? BidRules.ClosedStages.Contains(t.Status) : t.Status == TenderStatus.AwaitingCommitteeDecision || t.Status == TenderStatus.Awarded) &&
        db.CommitteeMembers.Any(m => m.UserId == id && m.TenderId == t.Id && (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo) &&
            db.CommitteeAssignments.Any(a => a.Id == m.CommitteeAssignmentId && a.TenderId == t.Id && a.Kind == kind)));
    public async Task<RoleDashboardModel> ReadAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException();
        try {
            var user = await users.GetUserAsync(principal);
            if (user is null || await users.IsLockedOutAsync(user) || !await eligibility.IsEligibleAsync(user)) throw new UnauthorizedAccessException();
            var roles = (await users.GetRolesAsync(user)).Where(ApplicationRoles.All.Contains).ToArray();
            if (roles.Contains(ApplicationRoles.Admin)) throw new UnauthorizedAccessException(); // Admin uses its existing guarded service.
            var sections = new List<WorkspaceSection>(); var now = clock.GetUtcNow();
            if (roles.Contains(ApplicationRoles.Supplier)) {
                var profile = await db.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id && x.Status == SupplierStatus.Approved, ct);
                if (profile is null || roles.Length != 1) throw new UnauthorizedAccessException();
                var links = db.TenderSuppliers.Where(x => x.SupplierProfileId == profile.Id);
                var invitations = db.Invitations.AsNoTracking().Where(i => links.Any(l => l.Id == i.TenderSupplierId && db.Tenders.Any(t => t.Id == l.TenderId && t.Status != TenderStatus.Draft)));
                var offers = OwnOffers(profile.Id);
                var pending = await invitations.LongCountAsync(i => i.Status == InvitationStatus.Pending && (i.ExpiresAt == null || now < i.ExpiresAt) && links.Any(l => l.Id == i.TenderSupplierId && db.Tenders.Any(t => t.Id == l.TenderId && now < t.SubmissionDeadline && (t.Status == TenderStatus.Published || t.Status == TenderStatus.Open))), ct);
                var recent = await (from i in invitations join l in links on i.TenderSupplierId equals l.Id join t in db.Tenders on l.TenderId equals t.Id
                    orderby i.SentAt descending, i.Id select new { i.Id, t.Title, i.Status, i.SentAt }).Take(5).ToListAsync(ct);
                sections.Add(new("Supplier workspace", "Your invitations and offers only. Final submissions remain read-only.",
                    [new("Pending invitations", pending, "Tenders", "Inbox"), new("Draft offers", await offers.LongCountAsync(x => x.Status == BidStatus.Draft, ct), "Bids"), new("Submitted offers", await offers.LongCountAsync(x => x.SubmittedAt != null, ct), "Bids")],
                    recent.Select(x => new WorkspaceRecord(x.Id, x.Title, x.Status.ToString(), "Tenders", "Invitation", x.SentAt)).ToArray()));
            } else {
                var departmentId = await db.EmployeeDepartments.Where(x => x.UserId == user.Id).Select(x => x.DepartmentId).SingleAsync(ct);
                if (roles.Contains(ApplicationRoles.RequestingDepartment)) {
                    var own = OwnRequests(user.Id);
                    var recent = await own.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(5).ToListAsync(ct);
                    sections.Add(new("My requisitions", "Only requests created by you are included, including within your department.",
                        [new("Draft & returned", await own.LongCountAsync(x => x.Status == PurchaseRequestStatus.Draft || x.Status == PurchaseRequestStatus.Returned, ct), "Requisitions"), new("In review", await own.LongCountAsync(x => x.Status == PurchaseRequestStatus.Submitted || x.Status == PurchaseRequestStatus.UnderReview, ct), "Requisitions"), new("Approved", await own.LongCountAsync(x => x.Status == PurchaseRequestStatus.Approved, ct), "Requisitions")],
                        recent.Select(x => new WorkspaceRecord(x.Id, x.Title, AdminDashboardModel.Label(x.Status), "Requisitions", At:x.CreatedAt)).ToArray()));
                }
                if (roles.Contains(ApplicationRoles.SourcingOfficer)) {
                    var review = ReviewQueue(user.Id, departmentId);
                    var recent = await review.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Take(5).ToListAsync(ct);
                    sections.Add(new("Sourcing operations", "The review queue excludes requests created by you or originating from your own department.",
                        [new("Eligible for review", await review.LongCountAsync(ct), "Requisitions"), new("Approved requests", await db.PurchaseRequisitions.LongCountAsync(x => x.Status == PurchaseRequestStatus.Approved, ct), "Requisitions"), new("Open tenders", await db.Tenders.LongCountAsync(x => x.Status == TenderStatus.Open, ct), "Tenders")],
                        recent.Select(x => new WorkspaceRecord(x.Id, x.Title, AdminDashboardModel.Label(x.Status), "Requisitions", At:x.SubmittedAt)).ToArray()));
                }
                if (roles.Contains(ApplicationRoles.SourcingManager)) {
                    var configured = db.Tenders.AsNoTracking().Where(t => (t.Status == TenderStatus.Draft || t.Status == TenderStatus.Published) && db.EvaluationCriteria.Any(c => c.TenderId == t.Id) && !db.EvaluationCriteria.Any(c => c.TenderId == t.Id && c.ApprovedAt != null));
                    var recent = await configured.OrderBy(t => t.SubmissionDeadline).ThenBy(t => t.Id).Take(5).ToListAsync(ct);
                    sections.Add(new("Sourcing management", "Review configured criteria through the existing approval workflow. Self-approval remains prohibited.",
                        [new("Unapproved criteria", await configured.LongCountAsync(ct), "Tenders"), new("Published tenders", await db.Tenders.LongCountAsync(x => x.Status == TenderStatus.Published, ct), "Tenders"), new("Open tenders", await db.Tenders.LongCountAsync(x => x.Status == TenderStatus.Open, ct), "Tenders")],
                        recent.Select(x => new WorkspaceRecord(x.Id, x.Title, "Criteria not approved", "Tenders", "Criteria", x.SubmissionDeadline)).ToArray()));
                }
                foreach (var pair in new[] { (ApplicationRoles.EvaluationCommittee, CommitteeKind.Evaluation, "Evaluation committee"), (ApplicationRoles.TopCommittee, CommitteeKind.TopCommittee, "Top committee") }) {
                    if (!roles.Contains(pair.Item1)) continue;
                    var assigned = AssignedTenders(user.Id, pair.Item2, now);
                    var recent = await assigned.OrderByDescending(x => x.SubmissionDeadline).ThenBy(x => x.Id).Take(5).ToListAsync(ct);
                    sections.Add(new(pair.Item3, "Effective assignments only, restricted to the tender stages available to your committee. Open an available assigned tender to continue its authorized workflow.",
                        [new("Available assigned tenders", await assigned.LongCountAsync(ct))], recent.Select(x => new WorkspaceRecord(x.Id, x.Title, AdminDashboardModel.Label(x.Status), pair.Item2 == CommitteeKind.TopCommittee ? "TopCommittee" : null, "Details", x.SubmissionDeadline)).ToArray()));
                }
            }
            var notices = db.Notifications.AsNoTracking().Where(x => x.RecipientUserId == user.Id);
            var messages = await notices.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(4).Select(x => new WorkspaceRecord(x.Id, x.Subject, x.ReadAt == null ? "Unread" : "Read", "Notifications", "Index", x.CreatedAt)).ToListAsync(ct);
            sections.Add(new("My notifications", "Messages addressed to your account.", [new("Unread notifications", await notices.LongCountAsync(x => x.ReadAt == null, ct), "Notifications")], messages));
            return new(true, user.UserName ?? "", roles, sections);
        } catch (Exception e) when (e is System.Data.Common.DbException or TimeoutException) {
            logger.LogWarning("Role dashboard data is temporarily unavailable."); return RoleDashboardModel.Unavailable;
        }
    }
}
