using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IEmployeeTransaction : IAsyncDisposable
{
    Task CommitAsync();
}

public interface IEmployeeData
{
    Task<IReadOnlyList<IdentityUser>> ListUsersAsync();
    Task<bool> HasSupplierProfileAsync(string userId);
    Task<IEmployeeTransaction> BeginAsync();
    Task<IReadOnlyList<Department>> DepartmentsAsync();
    Task<Guid?> DepartmentForAsync(string userId);
    Task<bool> DepartmentExistsAsync(Guid departmentId);
    Task AssignDepartmentAsync(string userId, Guid departmentId);
}

public interface IEmployeeDirectory
{
    Task<IReadOnlyList<SourcingTenderManagementSystem.Web.Models.Employees.EmployeeDetails>> ListDetailsAsync(int? page, string? search, CancellationToken ct);
}
public sealed class EmployeeData(ApplicationDbContext database) : IEmployeeData, IEmployeeDirectory
{
    public async Task<IReadOnlyList<SourcingTenderManagementSystem.Web.Models.Employees.EmployeeDetails>> ListDetailsAsync(int? page, string? search, CancellationToken ct)
    {
        var query = database.Users.AsNoTracking().Where(u => u.NormalizedUserName != "ADMIN" &&
            !database.SupplierProfiles.Any(p => p.UserId == u.Id) &&
            !database.UserRoles.Any(ur => ur.UserId == u.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Supplier")));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(u => (u.UserName != null && u.UserName.Contains(search)) || (u.Email != null && u.Email.Contains(search)));
        var ordered = query.OrderBy(u => u.UserName).ThenBy(u => u.Id);
        var selected = page.HasValue ? await Models.PageWindow<IdentityUser>.ReadAsync(ordered, page.Value, search, null, ct) : (IReadOnlyList<IdentityUser>)await ordered.ToListAsync(ct);
        var ids = selected.Select(u => u.Id).ToArray();
        var roles = await (from ur in database.UserRoles.AsNoTracking() join role in database.Roles on ur.RoleId equals role.Id where ids.Contains(ur.UserId) select new { ur.UserId, role.Name }).ToListAsync(ct);
        var departments = await (from a in database.EmployeeDepartments.AsNoTracking() join d in database.Departments on a.DepartmentId equals d.Id where ids.Contains(a.UserId) select new { a.UserId, d.Id, d.Name }).ToDictionaryAsync(x => x.UserId, ct);
        var now = DateTimeOffset.UtcNow;
        var rows = selected.Select(u => new Models.Employees.EmployeeDetails(u.Id, u.UserName ?? "", u.Email, u.PhoneNumber,
            roles.Where(x => x.UserId == u.Id).Select(x => x.Name!).ToArray(), Security.EmployeeManagement.IsDisabled(u),
            u.LockoutEnabled && u.LockoutEnd > now && !Security.EmployeeManagement.IsDisabled(u), false,
            departments.TryGetValue(u.Id, out var d) ? d.Id : null, departments.GetValueOrDefault(u.Id)?.Name)).ToList();
        return selected is Models.IPagedResults paged ? new Models.PageWindow<Models.Employees.EmployeeDetails>(rows, paged.Info) : rows;
    }
    public async Task<IReadOnlyList<Department>> DepartmentsAsync() =>
        await database.Departments.AsNoTracking().OrderBy(item => item.Name).ThenBy(item => item.Id).ToListAsync();
    public Task<Guid?> DepartmentForAsync(string userId) =>
        database.EmployeeDepartments.Where(item => item.UserId == userId).Select(item => (Guid?)item.DepartmentId).SingleOrDefaultAsync();
    public Task<bool> DepartmentExistsAsync(Guid departmentId) => database.Departments.AnyAsync(item => item.Id == departmentId);
    public async Task AssignDepartmentAsync(string userId, Guid departmentId)
    {
        var assignment = await database.EmployeeDepartments.SingleOrDefaultAsync(item => item.UserId == userId);
        if (assignment is null) database.EmployeeDepartments.Add(new EmployeeDepartment { UserId = userId, DepartmentId = departmentId });
        else assignment.DepartmentId = departmentId;
        await database.SaveChangesAsync();
    }
    public async Task<IReadOnlyList<IdentityUser>> ListUsersAsync() =>
        await database.Users.AsNoTracking().OrderBy(user => user.UserName).ToListAsync();
    public Task<bool> HasSupplierProfileAsync(string userId) =>
        database.SupplierProfiles.AsNoTracking().AnyAsync(profile => profile.UserId == userId);
    public async Task<IEmployeeTransaction> BeginAsync() =>
        new Transaction(await database.Database.BeginTransactionAsync());

    private sealed class Transaction(IDbContextTransaction transaction) : IEmployeeTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
