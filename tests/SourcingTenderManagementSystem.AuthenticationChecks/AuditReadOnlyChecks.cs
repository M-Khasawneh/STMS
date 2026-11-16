using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
internal static class AuditReadOnlyChecks
{
    internal static async Task Run()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        target.ApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly; target.ConnectTimeout = 5;
        var services = new ServiceCollection(); services.AddLogging();
        var guard = new ReportingReadOnlyChecks.SelectOnly();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(target.ConnectionString).AddInterceptors(guard));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var data = new BidData(db, users); int checks = 0;
        void Check(bool valid) { if (!valid) throw new Exception("Read-only audit regression failed"); checks++; }
        var employees = await new EmployeeData(db).ListDetailsAsync(1, null, default);
        Check(guard.Commands == 4); // count, page, batched roles, batched departments; independent of employee count.
        Check(employees.Count <= 25 && employees is IPagedResults && employees.All(x => !x.IsProtected && !x.Roles.Contains(ApplicationRoles.Supplier)));
        var none = await new EmployeeData(db).ListDetailsAsync(999999, "audit-no-match-" + Guid.NewGuid(), default);
        Check(none.Count == 0 && ((IPagedResults)none).Info.Page == 1);
        var roles = await (from ur in db.UserRoles.AsNoTracking() join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, Role = r.Name }).ToListAsync();
        foreach (var id in roles.Select(r => r.UserId).Distinct()) {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id)], "ReadOnlyAudit"));
            var account = await data.AccountAsync(principal, DateTimeOffset.UtcNow, default);
            var eligible = await EmployeeEligibility.Eligible(db, DateTimeOffset.UtcNow).AnyAsync(u => u.Id == id);
            Check(eligible == WorkspaceDiscovery.Eligible(account));
            if (account?.Supplier == true) {
                var before = guard.Commands;
                var rows = await data.OffersAsync(account, DateTimeOffset.UtcNow, 1, null, null, default);
                Check(guard.Commands - before == 2); // count + projected page, no item/document reads.
                Check(rows.Count <= 25 && rows is IPagedResults);
            }
            if (account?.Internal == true && account.Roles.Contains(ApplicationRoles.SourcingOfficer)) {
                var rows = await data.ReviewRowsAsync(account, DateTimeOffset.UtcNow, null, 1, null, null, default);
                Check(rows.Count <= 25 && rows.All(r => r.Bid.SubmittedAt.HasValue));
            }
        }
        Console.WriteLine($"PASS: {checks} SELECT-only paging, listing and eligibility checks; no database writes.");
    }
}
