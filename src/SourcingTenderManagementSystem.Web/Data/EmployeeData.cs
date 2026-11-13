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

public sealed class EmployeeData(ApplicationDbContext database) : IEmployeeData
{
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
