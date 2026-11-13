using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IAdminDashboard { Task<AdminDashboardModel> ReadAsync(ClaimsPrincipal principal, CancellationToken ct); }

// Read-only aggregates and bounded metadata projections. No bid/evaluation/document queries or audit writes.
public sealed class AdminDashboard(ApplicationDbContext db, UserManager<IdentityUser> users, TimeProvider clock, ILogger<AdminDashboard> logger) : IAdminDashboard
{
    public IQueryable<IdentityUser> EmployeeQuery() => db.Users.AsNoTracking().Where(u =>
        !db.SupplierProfiles.Any(p => p.UserId == u.Id) && !db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == ApplicationRoles.Supplier)));
    public IQueryable<DashboardActivity> ActivityQuery()
    {
        var requests = from log in db.AuditLogs.AsNoTracking() where log.RecordType == "PurchaseRequisition"
            join request in db.PurchaseRequisitions.AsNoTracking() on log.RecordId equals request.Id.ToString()
            select new { RecordId = request.Id, request.Title, Controller = "Requisitions", log.Action, Status = log.NewStatus, At = log.OccurredAt };
        var tenders = from log in db.AuditLogs.AsNoTracking() where log.RecordType == "Tender"
            join tender in db.Tenders.AsNoTracking() on log.RecordId equals tender.Id.ToString()
            select new { RecordId = tender.Id, tender.Title, Controller = "Tenders", log.Action, Status = log.NewStatus, At = log.OccurredAt };
        return requests.Concat(tenders).OrderByDescending(x => x.At).ThenBy(x => x.RecordId).Take(6)
            .Select(x => new DashboardActivity(x.RecordId, x.Title, x.Controller, x.Action, x.Status, x.At));
    }
    public async Task<AdminDashboardModel> ReadAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true || !principal.IsInRole(ApplicationRoles.Admin)) throw new UnauthorizedAccessException();
        try
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
            if (user is null || await users.IsLockedOutAsync(user)) throw new UnauthorizedAccessException();
            var roles = await users.GetRolesAsync(user);
            if (!roles.Contains(ApplicationRoles.Admin) || roles.Contains(ApplicationRoles.Supplier) ||
                !await db.EmployeeDepartments.AnyAsync(x => x.UserId == id, ct) || await db.SupplierProfiles.AnyAsync(x => x.UserId == id, ct))
                throw new UnauthorizedAccessException();
            // Sequential queries share the scoped context. No raw SQL, tracking, SaveChanges, migrations or seeding.
            var userCount = await db.Users.LongCountAsync(ct);
            var employeeCount = await EmployeeQuery().LongCountAsync(ct);
            var departmentCount = await db.Departments.LongCountAsync(ct);
            var suppliers = await db.SupplierProfiles.AsNoTracking().GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.LongCount() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
            var requests = await db.PurchaseRequisitions.AsNoTracking().GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.LongCount() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
            var tenders = await db.Tenders.AsNoTracking().GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.LongCount() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
            var now = clock.GetUtcNow();
            var overdue = await db.Tenders.LongCountAsync(t => t.Status == TenderStatus.Open && t.SubmissionDeadline <= now, ct);
            var deadlines = await db.Tenders.AsNoTracking().Where(t => t.Status == TenderStatus.Open && t.SubmissionDeadline > now)
                .OrderBy(t => t.SubmissionDeadline).ThenBy(t => t.Id).Select(t => new DashboardDeadline(t.Id, t.Title, t.SubmissionDeadline)).Take(4).ToListAsync(ct);
            var activity = await ActivityQuery().ToListAsync(ct);
            return new() { Available = true, ReadAt = clock.GetUtcNow(), Users = userCount, Employees = employeeCount, Departments = departmentCount,
                Suppliers = AdminDashboardModel.Distribution(suppliers), Requisitions = AdminDashboardModel.Distribution(requests), Tenders = AdminDashboardModel.Distribution(tenders),
                PendingSuppliers = suppliers.GetValueOrDefault(SupplierStatus.Pending), RequestsInReview = requests.GetValueOrDefault(PurchaseRequestStatus.Submitted) + requests.GetValueOrDefault(PurchaseRequestStatus.UnderReview),
                ApprovedRequests = requests.GetValueOrDefault(PurchaseRequestStatus.Approved), PastDeadlineOpenTenders = overdue, Deadlines = deadlines, Activity = activity };
        }
        catch (Exception e) when (e is System.Data.Common.DbException or TimeoutException)
        {
            logger.LogWarning("Admin dashboard data is temporarily unavailable.");
            return AdminDashboardModel.Unavailable;
        }
    }
}
