using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.Web.Security;

public interface IEmployeeEligibility
{
    Task<bool> IsEligibleAsync(IdentityUser user);
}

public sealed class EmployeeEligibility(UserManager<IdentityUser> users, ApplicationDbContext database)
    : IEmployeeEligibility
{
    public async Task<bool> IsEligibleAsync(IdentityUser user)
    {
        var roles = await users.GetRolesAsync(user);
        // Supplier access is independent from employee departments and never grants employee roles.
        if (roles.Contains(ApplicationRoles.Supplier))
            return roles.Count == 1 && await database.SupplierProfiles.AsNoTracking()
                .AnyAsync(profile => profile.UserId == user.Id && profile.Status == Models.Entities.SupplierStatus.Approved);
        return roles.Intersect(ApplicationRoles.EmployeeRoles).Any() &&
            await database.EmployeeDepartments.AsNoTracking().AnyAsync(assignment => assignment.UserId == user.Id) &&
            !await database.SupplierProfiles.AsNoTracking().AnyAsync(profile => profile.UserId == user.Id);
    }
}
