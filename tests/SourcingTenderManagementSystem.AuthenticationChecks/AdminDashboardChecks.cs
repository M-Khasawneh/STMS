using System.Data.Common;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class AdminDashboardChecks
{
    private static int checks;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
    public static async Task HttpAsync(HttpClient client, TestAdminDashboard fixture)
    {
        checks = 0;
        client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", "admin");
        foreach (var state in new[] { "populated", "empty", "unavailable", "encoded" })
        {
            client.DefaultRequestHeaders.Remove("X-Admin-Test-State"); client.DefaultRequestHeaders.Add("X-Admin-Test-State", state);
            var response = await client.GetAsync("/Workspace"); var html = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == HttpStatusCode.OK && html.Contains("Administration overview"), state + " Admin view compiles and renders");
            Check(html.Contains("admin-dashboard-page") && html.Contains("admin-dashboard.css") && !html.Contains("workspace-welcome"), "Admin-only theme without old welcome/cards: " + state);
            Check(response.Headers.CacheControl?.NoStore == true, "Admin data is never browser cached");
            if (state == "populated") {
                Check(html.Contains("42") && html.Contains("Pending Admin review") && html.Contains("Converted to tender"), "aggregate and status labels render");
                Check(html.Contains("Asia/Amman") && html.Contains("Requisition created"), "actual audit metadata uses existing time display");
                Check(!html.Contains("Technical Proposal") && !html.Contains("/Bids/") && !html.Contains("StorageReference"), "confidential bid contents excluded");
            }
            if (state == "empty") Check(html.Contains("No records yet") && html.Contains("No recorded activity yet") && html.Contains("No upcoming Open tender deadlines"), "zero data has honest empty states");
            if (state == "encoded") Check(html.Contains("&lt;script&gt;") && !html.Contains("<script>private"), "record titles are HTML-encoded");
            if (state == "unavailable") Check(html.Contains("temporarily unavailable") && !html.Contains("admin-status-list") && !html.Contains("data-metric-value>0"), "data failure never masquerades as zero");
        }
        foreach (var path in new[] { "/Departments", "/Employees", "/Home/Privacy" }) {
            var response = await client.GetAsync(path); var html = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == HttpStatusCode.OK && html.Contains("admin-workspace") && html.Contains("admin-shell.css") && !html.Contains("admin-dashboard.css"), "shared Admin theme without dashboard widgets: " + path);
        }
        var calls = fixture.Calls;
        foreach (var role in new[] { "supplier", "requester", "officer", "manager", "evaluator", "top" }) {
            client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", role);
            var html = await client.GetStringAsync("/Workspace");
            Check(!html.Contains("admin-dashboard.css") && html.Contains("My dashboard"), role + " has a personal dashboard without Admin data");
        }
        Check(fixture.Calls == calls, "non-Admin roles never invoke aggregate queries");
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var service = new AdminDashboard(db, null!, TimeProvider.System, NullLogger<AdminDashboard>.Instance);
        var anonymous = new ClaimsPrincipal();
        try { await service.ReadAsync(anonymous, default); throw new Exception("Expected authorization rejection"); }
        catch (UnauthorizedAccessException) { Check(true, "service rejects anonymous before SQL access"); }
        var supplier = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, ApplicationRoles.Supplier)], "memory"));
        try { await service.ReadAsync(supplier, default); throw new Exception("Expected authorization rejection"); }
        catch (UnauthorizedAccessException) { Check(true, "service rejects Supplier before SQL access"); }
        var employees = service.EmployeeQuery().ToQueryString();
        Check(employees.Contains("SupplierProfiles") && employees.Contains("AspNetUserRoles") && employees.Contains("NOT EXISTS"), "employee filtering translates and excludes supplier profiles/roles");
        var activity = service.ActivityQuery().ToQueryString();
        Check(activity.Contains("UNION ALL") && activity.Contains("TOP") && activity.Contains("PurchaseRequisitions") && activity.Contains("Tenders"), "bounded activity projection translates into SQL");
        Check(!activity.Contains("Reason") && !activity.Contains("ActorUserId") && !activity.Contains("BidDocuments") && !activity.Contains("[Bids]"), "SQL projection omits confidential payloads");
        Check(AdminDashboardModel.Distribution(new Dictionary<TenderStatus, long> { [TenderStatus.Awarded] = 2 }).Any(x => x.Label == "Awarded" && x.Count == 2), "later stages appear only when recorded");
        Check(AdminDashboardModel.Distribution(new Dictionary<TenderStatus, long>()).Count == 4, "unimplemented later-stage zero widgets omitted");
        Check(AdminDashboardModel.Distribution(new Dictionary<SupplierStatus, long> { [(SupplierStatus)99] = 1 }).Sum(x => x.Count) == 1, "unexpected recorded status cannot silently lower totals");
        Console.WriteLine($"PASS: {checks} database-free Admin dashboard checks.");
    }
    // Optional explicit mode: SELECT-only commands against existing records; never seeds or writes.
    public static async Task SqlReadOnlyAsync()
    {
        var source = Path.GetFullPath("src/SourcingTenderManagementSystem.Web");
        var config = new ConfigurationBuilder().SetBasePath(source).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
        var services = new ServiceCollection(); services.AddLogging(x => x.ClearProviders());
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(config.GetConnectionString("DefaultConnection")).AddInterceptors(new SelectOnly()));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton(TimeProvider.System); services.AddScoped<IAdminDashboard, AdminDashboard>();
        await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedUserName == "ADMIN") ?? throw new Exception("Existing Admin was not found. No accounts were created.");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, existing.Id), new Claim(ClaimTypes.Role, ApplicationRoles.Admin)], "readonly-verification"));
        var model = await scope.ServiceProvider.GetRequiredService<IAdminDashboard>().ReadAsync(principal, default);
        Check(model.Available, "real SQL dashboard queries completed");
        Check(model.Users == await db.Users.LongCountAsync() && model.Departments == await db.Departments.LongCountAsync(), "real user and department totals match independent counts");
        Check(model.Suppliers.Sum(x => x.Count) == await db.SupplierProfiles.LongCountAsync() && model.Requisitions.Sum(x => x.Count) == await db.PurchaseRequisitions.LongCountAsync() && model.Tenders.Sum(x => x.Count) == await db.Tenders.LongCountAsync(), "real status distributions match entity totals");
        var employees = new EmployeeManagement(scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(), scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(), new EmployeeData(db));
        Check(model.Employees == (await employees.ListAsync()).Count, "employee KPI matches existing employee-management semantics");
        Check(model.Activity.Count <= 6 && model.Deadlines.Count <= 4 && !db.ChangeTracker.HasChanges(), "read-only bounded projections leave no pending writes");
        Console.WriteLine("PASS: existing SQL records verified using SELECT-only interception; no records, schemas or migrations changed.");
    }
    private sealed class SelectOnly : DbCommandInterceptor
    {
        private static void Validate(DbCommand command) { if (!command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Non-SELECT database command blocked by read-only verification."); }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Validate(command); return result; }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) => throw new InvalidOperationException("Database writes are prohibited.");
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Validate(command); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Validate(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Database writes are prohibited.");
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) { Validate(command); return ValueTask.FromResult(result); }
    }
}

// Test assembly only: intentionally synthetic datasets for deterministic UI/empty/error verification.
public sealed class TestAdminDashboard(IHttpContextAccessor accessor, TimeProvider clock, bool populated = false) : IAdminDashboard
{
    public int Calls;
    public Task<AdminDashboardModel> ReadAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!principal.IsInRole(ApplicationRoles.Admin)) throw new UnauthorizedAccessException();
        Calls++;
        var state = accessor.HttpContext?.Request.Headers["X-Admin-Test-State"].FirstOrDefault() ?? (populated ? "populated" : "unavailable");
        var empty = state == "empty"; var now = clock.GetUtcNow();
        var result = state == "unavailable" ? AdminDashboardModel.Unavailable : new AdminDashboardModel {
            Available = true, ReadAt = now, Users = empty ? 0 : 69, Employees = empty ? 0 : 42, Departments = empty ? 0 : 8,
            Suppliers = AdminDashboardModel.Distribution(empty ? new Dictionary<SupplierStatus, long>() : new() { [SupplierStatus.Pending] = 6, [SupplierStatus.Approved] = 17, [SupplierStatus.Rejected] = 3, [SupplierStatus.Suspended] = 1 }),
            Requisitions = AdminDashboardModel.Distribution(empty ? new Dictionary<PurchaseRequestStatus, long>() : new() { [PurchaseRequestStatus.Draft] = 8, [PurchaseRequestStatus.Submitted] = 7, [PurchaseRequestStatus.UnderReview] = 5, [PurchaseRequestStatus.Approved] = 4, [PurchaseRequestStatus.Rejected] = 2, [PurchaseRequestStatus.Returned] = 3, [PurchaseRequestStatus.ConvertedToTender] = 14 }),
            Tenders = AdminDashboardModel.Distribution(empty ? new Dictionary<TenderStatus, long>() : new() { [TenderStatus.Draft] = 3, [TenderStatus.Published] = 2, [TenderStatus.Open] = 5, [TenderStatus.Closed] = 4 }),
            PendingSuppliers = empty ? 0 : 6, RequestsInReview = empty ? 0 : 12, ApprovedRequests = empty ? 0 : 4, PastDeadlineOpenTenders = empty ? 0 : 1,
            Deadlines = empty ? [] : [new(Guid.NewGuid(), "Network infrastructure procurement", now.AddDays(2)), new(Guid.NewGuid(), "Office equipment and workstations", now.AddDays(4)), new(Guid.NewGuid(), "Annual support services", now.AddDays(7)), new(Guid.NewGuid(), "Facilities maintenance services", now.AddDays(9))],
            Activity = empty ? [] : [new(Guid.NewGuid(), "Network infrastructure procurement", "Tenders", "Tender.StatusChanged", "Open", now.AddHours(-1)), new(Guid.NewGuid(), "Office workstations", "Requisitions", "Requisition.Created", "Draft", now.AddHours(-2)), new(Guid.NewGuid(), state == "encoded" ? "<script>private title</script>" : "IT equipment procurement", "Requisitions", "Requisition.Reviewed", "Approved", now.AddHours(-3))] };
        return Task.FromResult(result);
    }
}
