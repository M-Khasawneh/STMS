using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.Web.Security;

public interface IEmployeeEligibility
{
    Task<bool> IsEligibleAsync(IdentityUser user);
}

public sealed class EmployeeEligibility(ApplicationDbContext database)
    : IEmployeeEligibility
{
    public async Task<bool> IsEligibleAsync(IdentityUser user)
    {
        return await Eligible(database, DateTimeOffset.UtcNow).AnyAsync(x => x.Id == user.Id);
    }

    // Fresh SQL projection: no cross-request cache, no stale role/department/profile snapshot.
    public static IQueryable<IdentityUser> Eligible(ApplicationDbContext db, DateTimeOffset now) => db.Users.AsNoTracking().Where(u =>
        (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) &&
        (db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == ApplicationRoles.Supplier))
            ? db.UserRoles.Count(ur => ur.UserId == u.Id) == 1 && db.SupplierProfiles.Any(p => p.UserId == u.Id && p.Status == Models.Entities.SupplierStatus.Approved)
            : db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && ApplicationRoles.EmployeeRoles.Contains(r.Name!))) &&
              db.EmployeeDepartments.Any(e => e.UserId == u.Id) && !db.SupplierProfiles.Any(p => p.UserId == u.Id)));
}
