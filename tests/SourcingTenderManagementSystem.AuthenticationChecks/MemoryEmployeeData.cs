using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public sealed class MemoryEmployeeData(MemoryUsers users, HashSet<string> supplierProfiles) : IEmployeeData
{
    public Dictionary<Guid, Department> Departments { get; } = [];
    public Dictionary<string, Guid> Assignments { get; } = [];
    public Task<IReadOnlyList<Department>> DepartmentsAsync() => Task.FromResult<IReadOnlyList<Department>>(Departments.Values.OrderBy(item => item.Name).ToList());
    public Task<Guid?> DepartmentForAsync(string id) => Task.FromResult(Assignments.TryGetValue(id, out var value) ? (Guid?)value : null);
    public Task<bool> DepartmentExistsAsync(Guid id) => Task.FromResult(Departments.ContainsKey(id));
    public Task AssignDepartmentAsync(string id, Guid departmentId) { Assignments[id] = departmentId; return Task.CompletedTask; }
    public Task<IReadOnlyList<IdentityUser>> ListUsersAsync() =>
        Task.FromResult<IReadOnlyList<IdentityUser>>(users.Users.Values.OrderBy(user => user.UserName).ToList());
    public Task<bool> HasSupplierProfileAsync(string id) => Task.FromResult(supplierProfiles.Contains(id));
    public Task<IEmployeeTransaction> BeginAsync() => Task.FromResult<IEmployeeTransaction>(new Transaction(users, Assignments));

    private sealed class Transaction(MemoryUsers users, Dictionary<string, Guid> assignments) : IEmployeeTransaction
    {
        // Snapshot test data only, kept in memory; never print or persist these values.
        private readonly Dictionary<string, IdentityUser> originalUsers = users.Users.ToDictionary(
            pair => pair.Key, pair => JsonSerializer.Deserialize<IdentityUser>(JsonSerializer.Serialize(pair.Value))!);
        private readonly Dictionary<string, HashSet<string>> originalRoles = users.Roles.ToDictionary(pair => pair.Key, pair => new HashSet<string>(pair.Value));
        private bool committed;
        private readonly Dictionary<string, Guid> originalAssignments = new(assignments);
        public Task CommitAsync() { committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync()
        {
            if (!committed)
            {
                users.Users.Clear();
                users.Roles.Clear();
                foreach (var pair in originalUsers) users.Users[pair.Key] = pair.Value;
                foreach (var pair in originalRoles) users.Roles[pair.Key] = pair.Value;
                assignments.Clear();
                foreach (var pair in originalAssignments) assignments[pair.Key] = pair.Value;
            }
            return ValueTask.CompletedTask;
        }
    }
}
