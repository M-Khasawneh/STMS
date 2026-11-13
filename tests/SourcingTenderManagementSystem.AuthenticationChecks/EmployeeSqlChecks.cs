using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Employees;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class EmployeeSqlChecks
{
    // Explicit opt-in integration test. Requires database-change approval even
    // though every existing-account edit is rolled back before exit.
    public static async Task RunAsync()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(
            "src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connectionString = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
        var target = new SqlConnectionStringBuilder(connectionString);
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB")
            throw new InvalidOperationException("SQL integration test target does not match the approved Windows Authentication database.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(AccountController).Assembly.GetName().Name });
        builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var failure = new MembershipFailure();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString).AddInterceptors(failure));
        builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthenticationFoundation();
        builder.Services.AddScoped<IEmployeeData, SavepointEmployeeData>();
        builder.Services.AddScoped<EmployeeManagement>();
        await using var app = builder.Build(); // No HTTP listener is started.
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var employees = scope.ServiceProvider.GetRequiredService<EmployeeManagement>();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        await database.Database.OpenConnectionAsync();
        var baseline = await Counts(database);
        var firstAdmin = await users.FindByNameAsync("admin") ?? throw new InvalidOperationException("Expected first Admin is absent.");
        var firstBefore = await employees.GetAsync(firstAdmin.Id);
        var firstId = firstAdmin.Id;
        var originalRoles = firstBefore!.Roles.ToArray();
        var departmentId = firstBefore.DepartmentId ?? throw new InvalidOperationException("Admin department missing.");
        Check(firstBefore.DepartmentName == "Administration", "existing Admin belongs to Administration");
        await using (var outerTransaction = await database.Database.BeginTransactionAsync())
        {
            try
            {
                var selected = originalRoles.Append(ApplicationRoles.SourcingOfficer).Append(ApplicationRoles.EvaluationCommittee).Distinct().ToArray();
                Check((await employees.EditAsync(firstId, new EmployeeEditViewModel {
                    DepartmentId = departmentId, Email = firstBefore.Email, PhoneNumber = firstBefore.PhoneNumber,
                    UpdateRoles = true, Roles = selected })).Succeeded, "SQL multiple-role and department edit succeeds");
                database.ChangeTracker.Clear();
                var saved = await employees.GetAsync(firstId);
                Check(saved is not null && saved.DepartmentId == departmentId && saved.Roles.Order().SequenceEqual(selected.Order()),
                    "SQL reload confirms one department and multiple Identity roles");
                Check(await scope.ServiceProvider.GetRequiredService<IEmployeeEligibility>().IsEligibleAsync((await users.FindByIdAsync(firstId))!),
                    "department-assigned Admin remains eligible for authentication");
                var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>().CreateAsync((await users.FindByIdAsync(firstId))!);
                Check((await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>()
                    .AuthorizeAsync(principal, null, ApplicationRoles.Admin)).Succeeded, "Admin authorization remains system-wide");
                Check((await employees.EditAsync(firstId, new EmployeeEditViewModel {
                    DepartmentId = departmentId, Email = firstBefore.Email, PhoneNumber = firstBefore.PhoneNumber,
                    Roles = [ApplicationRoles.Supplier], UpdateRoles = false })).Succeeded, "SQL contact-only edit preserves existing roles");
                database.ChangeTracker.Clear();
                Check((await employees.GetAsync(firstId))!.Roles.Order().SequenceEqual(selected.Order()), "SQL role preservation verified after reload");
                foreach (var invalid in new[] { Array.Empty<string>(), new[] { ApplicationRoles.Supplier }, new[] { ApplicationRoles.SourcingOfficer } })
                    Check(!(await employees.EditAsync(firstId, new EmployeeEditViewModel { DepartmentId = departmentId,
                        UpdateRoles = true, Roles = invalid })).Succeeded, "SQL rejects empty, Supplier, or first-Admin demotion selection");
                Check(!(await employees.EditAsync(firstId, new EmployeeEditViewModel { DepartmentId = Guid.NewGuid() })).Succeeded,
                    "SQL rejects nonexistent departments");
                Check(!(await employees.SetEnabledAsync(firstId, false)).Succeeded, "SQL protects first Admin from disabling");
            }
            finally { await outerTransaction.RollbackAsync(); }
        }
        database.ChangeTracker.Clear();
        var restored = await employees.GetAsync(firstId);
        Check(restored is not null && restored == firstBefore with { Roles = restored.Roles } && restored.Roles.Order().SequenceEqual(originalRoles.Order()),
            "rollback restores original Admin metadata, department and roles");
        var final = await Counts(database);
        Check(baseline.Count == final.Count && baseline.All(pair => final.GetValueOrDefault(pair.Key) == pair.Value),
            "all application and migration-history table counts match the pre-test baseline");
        Console.WriteLine("PASS: SQL employee persistence checks completed; no users or departments created; all test edits rolled back.");
    }

    private static async Task<Dictionary<string, long>> Counts(ApplicationDbContext database)
    {
        var counts = new Dictionary<string, long>();
        var tables = database.Model.GetEntityTypes().Select(entity => entity.GetTableName()!).Distinct().Append("__EFMigrationsHistory");
        foreach (var table in tables)
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[" + table.Replace("]", "]]") + "]";
            counts[table] = (long)(await command.ExecuteScalarAsync())!;
        }
        return counts;
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }

    // Keep the production service and actual EF Identity stores. Each service
    // transaction is a SQL savepoint inside one outer transaction that never commits.
    private sealed class SavepointEmployeeData(ApplicationDbContext database) : IEmployeeData
    {
        private readonly EmployeeData inner = new(database);
        public Task<IReadOnlyList<IdentityUser>> ListUsersAsync() => inner.ListUsersAsync();
        public Task<bool> HasSupplierProfileAsync(string userId) => inner.HasSupplierProfileAsync(userId);
        public Task<IReadOnlyList<SourcingTenderManagementSystem.Web.Models.Entities.Department>> DepartmentsAsync() => inner.DepartmentsAsync();
        public Task<Guid?> DepartmentForAsync(string id) => inner.DepartmentForAsync(id);
        public Task<bool> DepartmentExistsAsync(Guid id) => inner.DepartmentExistsAsync(id);
        public Task AssignDepartmentAsync(string id, Guid departmentId) => inner.AssignDepartmentAsync(id, departmentId);
        public async Task<IEmployeeTransaction> BeginAsync()
        {
            var transaction = database.Database.CurrentTransaction ?? throw new InvalidOperationException("Outer rollback transaction is required.");
            var name = "employee_" + Guid.NewGuid().ToString("N")[..16];
            await transaction.CreateSavepointAsync(name);
            return new Lease(transaction, name);
        }
    }
    private sealed class Lease(IDbContextTransaction transaction, string name) : IEmployeeTransaction
    {
        private bool committed;
        public Task CommitAsync() { committed = true; return Task.CompletedTask; }
        public async ValueTask DisposeAsync() { if (!committed) await transaction.RollbackToSavepointAsync(name); }
    }
    private sealed class SyntheticMembershipException : Exception { }
    private sealed class MembershipFailure : SaveChangesInterceptor
    {
        public bool FailNextMembership { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNextMembership && eventData.Context!.ChangeTracker.Entries<IdentityUserRole<string>>().Any(entry => entry.State == EntityState.Added))
            {
                FailNextMembership = false;
                throw new SyntheticMembershipException();
            }
            return ValueTask.FromResult(result);
        }
    }
}
