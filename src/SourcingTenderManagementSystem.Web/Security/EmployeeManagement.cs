using Microsoft.AspNetCore.Identity;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Employees;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Security;

public sealed class EmployeeManagement(UserManager<IdentityUser> users, RoleManager<IdentityRole> roles, IEmployeeData data)
{
    public Task<IReadOnlyList<Department>> DepartmentsAsync() => data.DepartmentsAsync();
    public static bool IsDisabled(IdentityUser user) =>
        user.LockoutEnabled && user.LockoutEnd == DateTimeOffset.MaxValue;

    public async Task<IReadOnlyList<EmployeeDetails>> ListAsync(int? page = null, string? search = null, CancellationToken ct = default)
    {
        if (data is IEmployeeDirectory directory) return await directory.ListDetailsAsync(page, search, ct);
        var employees = new List<EmployeeDetails>();
        foreach (var user in await data.ListUsersAsync())
            if (await DescribeAsync(user) is { IsProtected: false } employee) employees.Add(employee);
        return employees;
    }

    public async Task<EmployeeDetails?> GetAsync(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? null : await DescribeAsync(user);
    }

    private async Task<EmployeeDetails?> DescribeAsync(IdentityUser user)
    {
        var assignedRoles = await users.GetRolesAsync(user);
        if (assignedRoles.Contains(ApplicationRoles.Supplier) || await data.HasSupplierProfileAsync(user.Id)) return null;
        var departmentId = await data.DepartmentForAsync(user.Id);
        var departmentName = (await data.DepartmentsAsync()).FirstOrDefault(item => item.Id == departmentId)?.Name;
        return new EmployeeDetails(user.Id, user.UserName ?? "", user.Email, user.PhoneNumber,
            assignedRoles.ToArray(), IsDisabled(user), await users.IsLockedOutAsync(user) && !IsDisabled(user),
            string.Equals(user.NormalizedUserName, users.NormalizeName("admin"), StringComparison.Ordinal), departmentId, departmentName);
    }

    public async Task<IdentityResult> CreateAsync(EmployeeCreateViewModel model)
    {
        await using var transaction = await data.BeginAsync();
        if (!await ValidRolesAsync(model.Roles)) return Failure("Select existing employee roles. Supplier is excluded; at least one role is required.");
        if (model.DepartmentId is not Guid departmentId || !await data.DepartmentExistsAsync(departmentId))
            return Failure("Select an existing department.");
        var user = new IdentityUser(model.UserName.Trim()) { Email = Clean(model.Email), PhoneNumber = Clean(model.PhoneNumber) };
        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded) return result;
        result = await users.AddToRolesAsync(user, model.Roles.Distinct(StringComparer.Ordinal));
        if (!result.Succeeded) return result;
        await data.AssignDepartmentAsync(user.Id, departmentId);
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> EditAsync(string id, EmployeeEditViewModel model)
    {
        await using var transaction = await data.BeginAsync();
        var user = await users.FindByIdAsync(id);
        var employee = user is null ? null : await DescribeAsync(user);
        if (employee is null) return Failure("Employee is unavailable.");
        if (model.DepartmentId is not Guid departmentId || !await data.DepartmentExistsAsync(departmentId))
            return Failure("Select an existing department.");
        var selectedRoles = model.Roles.Distinct(StringComparer.Ordinal).ToArray();
        if (model.UpdateRoles)
        {
            if (!await ValidRolesAsync(selectedRoles)) return Failure("Select existing employee roles. Supplier is excluded; at least one role is required.");
            if (employee.IsProtected && !selectedRoles.Contains(ApplicationRoles.Admin))
                return Failure("The first Admin must retain the Admin role.");
        }
        if (employee.IsProtected && !employee.Roles.Contains(ApplicationRoles.Admin))
            return Failure("The first Admin's role state requires separate review.");

        if (user!.Email != Clean(model.Email))
        {
            var result = await users.SetEmailAsync(user, Clean(model.Email));
            if (!result.Succeeded) return result;
        }
        if (user.PhoneNumber != Clean(model.PhoneNumber))
        {
            var result = await users.SetPhoneNumberAsync(user, Clean(model.PhoneNumber));
            if (!result.Succeeded) return result;
        }
        if (model.UpdateRoles)
        {
            var result = await users.RemoveFromRolesAsync(user, employee.Roles.Except(selectedRoles, StringComparer.Ordinal));
            if (!result.Succeeded) return result;
            result = await users.AddToRolesAsync(user, selectedRoles.Except(employee.Roles, StringComparer.Ordinal));
            if (!result.Succeeded) return result;
        }
        await data.AssignDepartmentAsync(user.Id, departmentId);
        var stampResult = await users.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) return stampResult;
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetEnabledAsync(string id, bool enabled)
    {
        await using var transaction = await data.BeginAsync();
        var user = await users.FindByIdAsync(id);
        var employee = user is null ? null : await DescribeAsync(user);
        if (employee is null) return Failure("Employee is unavailable.");
        if (employee.IsProtected && !enabled) return Failure("The first Admin cannot be disabled.");

        // Existing Identity lockout fields represent administrative disablement.
        // Enabling an already-enabled account does not clear a temporary security lockout.
        if (enabled && !IsDisabled(user!)) return IdentityResult.Success;
        var result = await users.SetLockoutEnabledAsync(user!, true);
        if (!result.Succeeded) return result;
        result = await users.SetLockoutEndDateAsync(user!, enabled ? null : DateTimeOffset.MaxValue);
        if (!result.Succeeded) return result;
        if (enabled)
        {
            result = await users.ResetAccessFailedCountAsync(user!);
            if (!result.Succeeded) return result;
        }
        result = await users.UpdateSecurityStampAsync(user!);
        if (!result.Succeeded) return result;
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    private async Task<bool> ValidRolesAsync(IEnumerable<string> selected)
    {
        var distinct = selected.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0) return false;
        foreach (var role in distinct)
            if (!ApplicationRoles.EmployeeRoles.Contains(role, StringComparer.Ordinal) || !await roles.RoleExistsAsync(role)) return false;
        return true;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IdentityResult Failure(string message) =>
        IdentityResult.Failed(new IdentityError { Code = "EmployeeManagement", Description = message });
}
