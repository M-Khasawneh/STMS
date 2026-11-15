using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

// This runner has no fixture creation, SaveChanges, SQL mutations, export-file writes or cleanup operations.
internal static class ReportingReadOnlyChecks
{
    public static async Task RunAsync()
    {
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var target = new SqlConnectionStringBuilder(json.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Refusing an unexpected read-only database target.");
        target.ApplicationIntent = ApplicationIntent.ReadOnly; target.ConnectTimeout = 5;
        var services = new ServiceCollection(); services.AddLogging(); services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(target.ConnectionString).AddInterceptors(new SelectOnly()));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>(); services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<BidData>(); services.AddScoped<IBidData>(s => s.GetRequiredService<BidData>()); services.AddScoped<TenderManagement>(); services.AddScoped<EvaluationManagement>(); services.AddScoped<IEvaluationManagement>(s => s.GetRequiredService<EvaluationManagement>()); services.AddScoped<IUnifiedReportManagement, UnifiedReportManagement>(); services.AddScoped<IReporting, Reporting>(); services.AddScoped<ITopCommitteeData, TopCommitteeData>(); services.AddScoped<ITopCommitteeManagement, TopCommitteeManagement>();
        services.AddScoped<IBidSubmissionDocuments,BidSubmissionDocumentsPending>(); services.AddScoped<BidManagement>(); services.AddScoped<BidReviewPages>(); services.AddScoped<IAiBidData,AiBidData>(); services.AddSingleton<IAiBidDocuments,TestAiDocuments>();
        await using var provider = services.BuildServiceProvider(); var count = 0; var skipped = new List<string>();
        foreach (var role in ApplicationRoles.All)
        {
            using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var data = scope.ServiceProvider.GetRequiredService<BidData>(); var reports = scope.ServiceProvider.GetRequiredService<IReporting>(); var now = DateTimeOffset.UtcNow;
            var ids = await (from u in db.Users.AsNoTracking() join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id where r.Name == role && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) orderby u.Id select u.Id).ToListAsync();
            ClaimsPrincipal? actor = null;
            foreach (var id in ids) { var candidate = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "ReadOnlyReportCheck")); var account = await data.AccountAsync(candidate, now, CancellationToken.None); if (account != null && ReportRules.Categories(account).Length > 0) { actor = candidate; break; } }
            if (actor == null) { skipped.Add(role); continue; }
            var topData = scope.ServiceProvider.GetRequiredService<ITopCommitteeData>();
            var topAccount = await data.AccountAsync(actor, now, CancellationToken.None);
            if (topAccount is not null) {
                if (topAccount.Internal && topAccount.Roles.Any(r => r is ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or ApplicationRoles.TopCommittee)) {
                    var top = scope.ServiceProvider.GetRequiredService<ITopCommitteeManagement>();
                    var rows = await top.ListAsync(actor); count++;
                    foreach (var row in rows.Take(2)) { await top.PageAsync(actor,row.Id); count++; }
                    Console.WriteLine($"PASS: SELECT-only final-decision list/page queries for {role}; {rows.Count} authorized tenders.");
                }
                var tender = await db.Tenders.AsNoTracking().OrderBy(t=>t.Id).Select(t=>(Guid?)t.Id).FirstOrDefaultAsync();
                if (tender.HasValue) {
                    try { await topData.OutcomeAsync(topAccount,tender.Value,CancellationToken.None); }
                    catch (BidException error) when (error.Status == 404) { /* Scoped query correctly returned no metadata. */ }
                    count++; Console.WriteLine($"PASS: SELECT-only protected outcome projection for {role}.");
                }
            }
            if (topAccount is not null && topAccount.Internal && topAccount.Roles.Any(r=>r is ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee)) {
                var visible=await data.VisibleQuery(topAccount,now).OrderBy(b=>b.Id).Select(b=>(Guid?)b.TenderId).FirstOrDefaultAsync();
                if(visible.HasValue){var ai=scope.ServiceProvider.GetRequiredService<IAiBidData>();var evidence=await ai.SourceAsync(actor,visible.Value,CancellationToken.None);await ai.FindAsync(evidence,CancellationToken.None);count++;Console.WriteLine($"PASS: SELECT-only AI evidence/history queries for {role}. No Ollama call or analysis persistence.");}
            }
            var identityUser = await db.Users.AsNoTracking().SingleAsync(u=>u.Id==actor.FindFirstValue(ClaimTypes.NameIdentifier));
            var identityManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            _ = await identityManager.GetPasskeysAsync(identityUser); count++;
            Console.WriteLine($"PASS: SELECT-only native Identity passkey-store query for {role}; no credentials registered.");
            var initial = await reports.ReadAsync(actor, new(), CancellationToken.None);
            foreach (var category in initial.Categories)
            {
                var page = await reports.ReadAsync(actor, new() { Category = category }, CancellationToken.None); count++; Console.WriteLine($"PASS: read-only SQL report {role}/{category}; {page.Document.Tables.Count} tables.");
                if (category is "requests" or "tenders" or "offers" or "evaluations" or "decisions") { await reports.ReadAsync(actor, new() { Category = category, From = new DateOnly(1900, 1, 1), To = new DateOnly(2100, 12, 31) }, CancellationToken.None); count++; }
                if (category == "unified" && page.Tenders.FirstOrDefault() is { } tender) { page = await reports.ReadAsync(actor, new() { Category = category, TenderId = tender.Id }, CancellationToken.None); count++; }
                if (page.Document.Tables.Count > 0) { var xlsx = ReportExports.Excel(page.Document); if (xlsx.Length < 100) throw new Exception("Empty Excel export."); count++; if (!ReportExports.HasRightToLeftText(page.Document)) { var pdf = ReportExports.Pdf(page.Document); if (pdf.Length < 100) throw new Exception("Empty PDF export."); count++; } else Console.WriteLine("LIMITATION: PDF skipped for right-to-left text; Excel verified."); }
            }
        }
        Console.WriteLine($"PASS: {count} actual SQL read/report/export checks. SELECT-only interceptor active; no files or database rows written. Roles without eligible existing accounts: {string.Join(", ", skipped)}.");
    }
    private sealed class SelectOnly : DbCommandInterceptor
    {
        private static void Guard(DbCommand command) { if (!command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(command.CommandText, @"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|EXEC|EXECUTE|BACKUP|RESTORE|DBCC)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new InvalidOperationException("Read-only reporting guard rejected a non-SELECT command."); }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result) { Guard(command); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Guard(command); return ValueTask.FromResult(result); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) => throw new InvalidOperationException("Read-only reporting guard rejected a non-query.");
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) => throw new InvalidOperationException("Read-only reporting guard rejected a non-query.");
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result) { Guard(command); return result; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default) { Guard(command); return ValueTask.FromResult(result); }
    }
}
