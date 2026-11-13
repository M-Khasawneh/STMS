using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Explicit command for authorized temporary SQL/HTTPS tests. Cleanup only owns this run's fixtures.
public static class RequisitionSqlChecks
{
    public static async Task RunAsync()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
        var target = new SqlConnectionStringBuilder(connection);
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Unapproved SQL target.");
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(AccountController).Assembly.GetName().Name,
            EnvironmentName = "Development", WebRootPath = Path.GetFullPath("src/SourcingTenderManagementSystem.Web/wwwroot") });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider(); builder.Services.AddControllersWithViews();
        var failure = new AuditFailure();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection).AddInterceptors(failure));
        builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthenticationFoundation();
        builder.Services.AddScoped<RequisitionManagement>(); builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IEmployeeData, EmployeeData>(); builder.Services.AddScoped<EmployeeManagement>();
        builder.Services.AddScoped<IDepartmentStore, DepartmentStore>(); builder.Services.AddScoped<IPublicStatistics, PublicStatistics>();
        await using var app = builder.Build();
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        var marker = "req-check-" + Guid.NewGuid().ToString("N");
        var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        Dictionary<string,long> baseline; string originalUsers; string originalMemberships; Guid homeDepartment;
        var reviewDepartment = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            baseline = await Counts(db);
            originalUsers = await UserSnapshot(db); originalMemberships = await MembershipSnapshot(db);
            homeDepartment = await db.Departments.Where(x => x.Name == "Administration").Select(x => x.Id).SingleAsync();
        }
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        HttpClient Browser() => new(new HttpClientHandler { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate.Thumbprint }) { BaseAddress = new Uri(address) };
        using var anonymous = Browser(); using var owner = Browser(); using var colleague = Browser(); using var officer = Browser(); using var sameOfficer = Browser(); using var admin = Browser(); using var supplier = Browser(); using var evaluator = Browser();
        var ids = new Dictionary<string,string>();
        try
        {
            using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); db.Departments.Add(new Department { Id = reviewDepartment, Name = marker + " review department" }); await db.SaveChangesAsync(); }
            await CreateUser("owner", ApplicationRoles.RequestingDepartment, homeDepartment);
            await CreateUser("colleague", ApplicationRoles.RequestingDepartment, homeDepartment);
            await CreateUser("officer", ApplicationRoles.SourcingOfficer, reviewDepartment);
            await CreateUser("same-officer", ApplicationRoles.SourcingOfficer, homeDepartment);
            await CreateUser("admin", ApplicationRoles.Admin, homeDepartment);
            await CreateUser("supplier", ApplicationRoles.Supplier, null);
            await CreateUser("evaluator", ApplicationRoles.EvaluationCommittee, homeDepartment);
            foreach (var pair in new[] { (owner,"owner"), (colleague,"colleague"), (officer,"officer"), (sameOfficer,"same-officer"), (admin,"admin"), (supplier,"supplier"), (evaluator,"evaluator") }) await Login(pair.Item1, pair.Item2);
            Check((await anonymous.GetAsync("/Requisitions")).Headers.Location?.OriginalString.Contains("Login") == true, "anonymous requisition access denied");
            foreach (var client in new[] { supplier, evaluator }) Check(Denied(await client.GetAsync("/Requisitions")), "non-requisition role has no module access");
            foreach (var client in new[] { officer, admin, sameOfficer }) Check(Denied(await client.GetAsync("/Requisitions/Create")), "officer and Admin cannot create content");
            Check((await owner.PostAsync("/Requisitions/Create", new FormUrlEncodedContent(Fields("no-csrf")))).StatusCode == HttpStatusCode.BadRequest, "create requires antiforgery");
            var add = Fields("add-item"); add["command"] = "AddItem"; add["__RequestVerificationToken"] = await Token(owner, "/Requisitions/Create");
            var addResponse = await owner.PostAsync("/Requisitions/Create", new FormUrlEncodedContent(add));
            Check(addResponse.StatusCode == HttpStatusCode.OK && (await addResponse.Content.ReadAsStringAsync()).Contains("Items[1].Description"), "no-JavaScript add-item form preserves fields without saving");
            foreach (var change in new[] { ("Priority","Critical"), ("Items[0].Quantity","0"), ("Items[0].Quantity","0.0000001"), ("Items[0].Specifications", " ") })
            {
                var invalid = Fields("invalid"); invalid[change.Item1] = change.Item2;
                var response = await Create(owner, invalid); Check(response.StatusCode == HttpStatusCode.OK, "invalid field rejected: " + change.Item1);
            }
            Check(await RequestCount() == 0, "invalid forms and item adjustments do not persist requests");
            var fields = Fields("first"); fields["DepartmentId"] = reviewDepartment.ToString(); fields["CreatedByUserId"] = ids["officer"]; fields["Status"] = "Approved";
            var first = await CreateId(owner, fields);
            foreach (var action in new[] { "Edit", "Submit", "Review" })
            {
                var client = action == "Review" ? officer : owner;
                Check((await client.PostAsync("/Requisitions/" + action + "/" + first, new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, action + " requires antiforgery");
            }
            var detail = await Details(first, "owner");
            Check(detail.Request.Status == PurchaseRequestStatus.Draft && detail.Request.DepartmentId == homeDepartment && detail.Request.CreatedByUserId == ids["owner"] && detail.Items.Single().Quantity == .125m, "draft, ownership, department and fractional quantities persist; overposting ignored");
            Check(detail.History.Count == 1 && detail.History[0].Reason!.Contains("Specifications"), "creation audit includes request and item snapshot");
            foreach (var path in new[] { "/Requisitions/Details/", "/Requisitions/Edit/" }) Check((await colleague.GetAsync(path + first)).StatusCode == HttpStatusCode.NotFound, "same-department colleague cannot view or edit another owner's request");
            Check(!(await colleague.GetStringAsync("/Requisitions")).Contains(first.ToString()), "requester list excludes colleague requests");
            foreach (var client in new[] { officer, admin }) Check((await client.GetAsync("/Requisitions/Details/" + first)).StatusCode == HttpStatusCode.OK, "officer/Admin can view requests across departments");
            foreach (var client in new[] { officer, admin }) Check(Denied(await client.GetAsync("/Requisitions/Edit/" + first)), "officer/Admin cannot edit content");
            var edited = Fields("first-edited"); edited["Version"] = detail.Version; edited["Items[0].Id"] = detail.Items[0].Id.ToString();
            edited["Items[1].Description"] = "Second material"; edited["Items[1].Quantity"] = "2.5"; edited["Items[1].Specifications"] = "Second specification";
            Check((await Edit(owner, first, edited)).StatusCode == HttpStatusCode.Redirect, "owner edits draft and adds item");
            var stale = await Edit(owner, first, edited); Check(stale.StatusCode == HttpStatusCode.Conflict, "stale draft form cannot overwrite a newer save");
            detail = await Details(first, "owner");
            Check(detail.Items.Count == 2 && detail.Items.Any(x => x.Id == Guid.Parse(edited["Items[0].Id"])), "editing preserves existing item identity");
            var foreign = await CreateId(colleague, Fields("colleague"));
            var otherItem = (await Details(foreign, "colleague")).Items[0].Id;
            var forged = Fields("forged"); forged["Version"] = detail.Version; forged["Items[0].Id"] = otherItem.ToString();
            Check((await Edit(owner, first, forged)).StatusCode == HttpStatusCode.OK && (await Details(first, "owner")).Request.Title == detail.Request.Title, "foreign item ID cannot reassign or change another request");
            var removed = Fields("one-item"); removed["Version"] = detail.Version; removed["Items[0].Id"] = detail.Items[0].Id.ToString();
            Check((await Edit(owner, first, removed)).StatusCode == HttpStatusCode.Redirect && (await Details(first, "owner")).Items.Count == 1, "owner removes a draft item atomically");
            // Empty drafts are valid; submission must revalidate their saved contents.
            var empty = Fields("empty"); foreach (var keyName in empty.Keys.Where(x => x.StartsWith("Items[")).ToList()) empty.Remove(keyName);
            var emptyId = await CreateId(owner, empty);
            Check((await Submit(owner, emptyId)).StatusCode == HttpStatusCode.OK && (await Details(emptyId, "owner")).Request.Status == PurchaseRequestStatus.Draft, "zero-item draft saves but cannot be submitted");
            var pastFields = Fields("past"); pastFields["TargetDate"] = DateOnly.FromDateTime(DateTime.Now).AddDays(-1).ToString("yyyy-MM-dd");
            var past = await CreateId(owner, pastFields);
            Check((await Submit(owner, past)).StatusCode == HttpStatusCode.OK && (await Details(past, "owner")).Request.Status == PurchaseRequestStatus.Draft, "past target date is rejected at submission");
            Check((await Submit(owner, first)).StatusCode == HttpStatusCode.Redirect, "Draft transitions to Submitted");
            detail = await Details(first, "owner"); Check(detail.Request.SubmittedAt.HasValue, "submission timestamp persists");
            foreach (var client in new[] { colleague, officer, admin }) Check(Denied(await Submit(client, first, detail.Version)), "only owner can submit; officers/Admin cannot submit");
            Check(Denied(await owner.GetAsync("/Requisitions/Edit/" + first)), "Submitted request content is read-only");
            Check((await Review(officer, first, "Approved")).StatusCode == HttpStatusCode.OK, "approval before explicit Under Review is rejected");
            Check((await Details(first, "officer")).Request.Status == PurchaseRequestStatus.Submitted, "invalid transition leaves state unchanged");
            Check(Denied(await Review(sameOfficer, first, "UnderReview")), "officer cannot start own-department review");
            Check(Denied(await Review(admin, first, "UnderReview")), "Admin oversight cannot review");
            Check((await Review(officer, first, "UnderReview")).StatusCode == HttpStatusCode.Redirect, "Submitted transitions to Under Review");
            Check((await Review(officer, first, "Returned", " ")).StatusCode == HttpStatusCode.OK, "return requires a nonempty reason");
            Check((await Review(officer, first, "Returned", "Clarify the item specifications")).StatusCode == HttpStatusCode.Redirect, "Under Review transitions to Returned with reason");
            detail = await Details(first, "owner"); Check(detail.Reviews.Any(x => x.Outcome == PurchaseRequestStatus.Returned && x.Comments == "Clarify the item specifications"), "return decision, reviewer and reason persist");
            Check((await Submit(owner, first)).StatusCode == HttpStatusCode.OK && (await Details(first, "owner")).Request.Status == PurchaseRequestStatus.Returned, "Returned request cannot be resubmitted without correction");
            var corrected = Fields("corrected"); corrected["Version"] = detail.Version; corrected["Items[0].Id"] = detail.Items[0].Id.ToString(); corrected["Items[0].Specifications"] = "Corrected specification"; corrected["command"] = "Submit";
            Check((await Edit(owner, first, corrected)).StatusCode == HttpStatusCode.Redirect, "owner corrects Returned request and resubmits");
            Check((await Review(officer, first, "UnderReview")).StatusCode == HttpStatusCode.Redirect && (await Review(officer, first, "Approved")).StatusCode == HttpStatusCode.Redirect, "corrected request completes explicit review and approval");
            detail = await Details(first, "owner"); Check(detail.Request.Status == PurchaseRequestStatus.Approved && detail.Reviews.Count == 4 && detail.History.Any(x => x.PreviousStatus == "Returned" && x.NewStatus == "Submitted"), "approval preserves all earlier review and audit history");
            Check(Denied(await owner.GetAsync("/Requisitions/Edit/" + first)), "Approved requests remain read-only");
            var rejectedFields = Fields("reject"); rejectedFields["command"] = "Submit"; var rejected = await CreateId(owner, rejectedFields);
            await Review(officer, rejected, "UnderReview");
            Check((await Review(officer, rejected, "Rejected", "")).StatusCode == HttpStatusCode.OK, "rejection requires a reason");
            Check((await Review(officer, rejected, "Rejected", "Not justified")).StatusCode == HttpStatusCode.Redirect, "rejection records the decision");
            Check(Denied(await owner.GetAsync("/Requisitions/Edit/" + rejected)) && (await Review(officer, rejected, "UnderReview")).StatusCode == HttpStatusCode.OK, "Rejected requests cannot be edited or reopened");
            // Seed an owned historical request to exercise creator exclusion independently of department exclusion.
            Guid self;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var request = new PurchaseRequisition { Id = Guid.NewGuid(), CreatedByUserId = ids["officer"], DepartmentId = homeDepartment, Title = marker + " self", Reason = "Historical requester role", Priority = "Low", TargetDate = DateOnly.FromDateTime(DateTime.Now), Status = PurchaseRequestStatus.Submitted, CreatedAt = DateTimeOffset.UtcNow };
                db.PurchaseRequisitions.Add(request); await db.SaveChangesAsync(); self = request.Id;
            }
            Check(Denied(await Review(officer, self, "UnderReview")), "officer cannot review own historical request from a different department");
            // Competing review POSTs use one original version; the row lock and audit fingerprint admit only one.
            var raceFields = Fields("race"); raceFields["command"] = "Submit"; var race = await CreateId(owner, raceFields); await Review(officer, race, "UnderReview");
            var raceVersion = (await Details(race, "officer")).Version;
            var outcomes = await Task.WhenAll(RaceDecision(PurchaseRequestStatus.Approved), RaceDecision(PurchaseRequestStatus.Returned));
            Check(outcomes.Count(x => x) == 1, "simultaneous decisions yield one success and one stale rejection");
            var raced = await Details(race, "officer"); Check(raced.Reviews.Count == 2, "concurrent decisions do not duplicate review history");
            using (var scope = app.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<RequisitionManagement>();
                var actor = await service.ActorAsync(Principal(ids["owner"]));
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var before = await db.PurchaseRequisitions.CountAsync(); var beforeAudit = await db.AuditLogs.CountAsync();
                failure.FailNext = true;
                try { await service.SaveAsync(null, actor, new RequisitionForm { Title = marker + " rollback", Reason = "Atomicity check", Priority = "High", TargetDate = service.Today, Items = [new() { Description = "Item", Specifications = "Spec", Quantity = .5m }] }, false); throw new Exception("Expected audit failure."); }
                catch (SyntheticAuditException) { }
                db.ChangeTracker.Clear();
                Check(await db.PurchaseRequisitions.CountAsync() == before && await db.AuditLogs.CountAsync() == beforeAudit, "audit persistence failure rolls back request and items");
            }
            var browserFields = Fields("browser-review"); browserFields["command"] = "Submit";
            var browserReview = await CreateId(owner, browserFields); await Review(officer, browserReview, "UnderReview");
            // Browser QA uses only the fixture accounts and requests owned by this run.
            var start = new System.Diagnostics.ProcessStartInfo("node", "tests/requisition-browser.cjs") { UseShellExecute = false };
            start.Environment["REQ_TEST_URL"] = address; start.Environment["REQ_TEST_PASSWORD"] = password; start.Environment["REQ_TEST_MARKER"] = marker;
            start.Environment["REQ_TEST_REQUEST"] = first.ToString(); start.Environment["REQ_TEST_REVIEW"] = browserReview.ToString(); start.Environment["REQ_TEST_EDIT"] = emptyId.ToString();
            using var browserProcess = System.Diagnostics.Process.Start(start)!; await browserProcess.WaitForExitAsync();
            Check(browserProcess.ExitCode == 0, "browser responsive/item/review checks passed");

            async Task<bool> RaceDecision(PurchaseRequestStatus decision)
            {
                using var scope = app.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<RequisitionManagement>();
                try { await service.ReviewAsync(race, await service.ActorAsync(Principal(ids["officer"])), new RequisitionActionForm { Version = raceVersion, Decision = decision, Comments = "Concurrent review" }); return true; }
                catch (RequisitionException exception) when (exception.Status == 409) { return false; }
            }
        }
        finally
        {
            await app.StopAsync();
            using var cleanup = app.Services.CreateScope(); var db = cleanup.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var users = cleanup.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var ownedUsers = await db.Users.Where(x => x.UserName != null && x.UserName.StartsWith(marker + "-")).ToListAsync(); var userIds = ownedUsers.Select(x => x.Id).ToArray();
            var requests = await db.PurchaseRequisitions.Where(x => userIds.Contains(x.CreatedByUserId)).ToListAsync(); var requestIds = requests.Select(x => x.Id).ToArray(); var recordIds = requestIds.Select(x => x.ToString()).ToArray();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                db.PurchaseRequisitionReviews.RemoveRange(await db.PurchaseRequisitionReviews.Where(x => requestIds.Contains(x.PurchaseRequisitionId)).ToListAsync());
                db.PurchaseRequisitionItems.RemoveRange(await db.PurchaseRequisitionItems.Where(x => requestIds.Contains(x.PurchaseRequisitionId)).ToListAsync());
                db.AuditLogs.RemoveRange(await db.AuditLogs.Where(x => x.RecordType == "PurchaseRequisition" && recordIds.Contains(x.RecordId)).ToListAsync());
                await db.SaveChangesAsync(); db.PurchaseRequisitions.RemoveRange(requests); await db.SaveChangesAsync();
                db.SupplierProfiles.RemoveRange(await db.SupplierProfiles.Where(x => userIds.Contains(x.UserId)).ToListAsync());
                db.EmployeeDepartments.RemoveRange(await db.EmployeeDepartments.Where(x => userIds.Contains(x.UserId)).ToListAsync()); await db.SaveChangesAsync();
                foreach (var user in ownedUsers) Check((await users.DeleteAsync(user)).Succeeded, "temporary requisition test account removed");
                var temporaryDepartment = await db.Departments.SingleOrDefaultAsync(x => x.Id == reviewDepartment && x.Name == marker + " review department");
                if (temporaryDepartment is not null) { db.Departments.Remove(temporaryDepartment); await db.SaveChangesAsync(); }
                await transaction.CommitAsync();
            }
            var after = await Counts(db);
            Check(baseline.Count == after.Count && baseline.All(x => after.GetValueOrDefault(x.Key) == x.Value), "all table counts including requisitions, audit and migrations match baseline");
            Check(originalUsers == await UserSnapshot(db) && originalMemberships == await MembershipSnapshot(db), "all existing Identity users and role assignments are unchanged");
        }
        Console.WriteLine("PASS: requisition SQL/HTTPS/browser tests completed; all temporary data removed.");

        async Task CreateUser(string suffix, string role, Guid? department)
        {
            using var scope = app.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new IdentityUser(marker + "-" + suffix); Check((await users.CreateAsync(user, password)).Succeeded && (await users.AddToRoleAsync(user, role)).Succeeded, "temporary fixture: " + suffix); ids[suffix] = user.Id;
            if (department.HasValue) db.EmployeeDepartments.Add(new EmployeeDepartment { UserId = user.Id, DepartmentId = department.Value });
            else db.SupplierProfiles.Add(new SupplierProfile { Id = Guid.NewGuid(), UserId = user.Id, CompanyName = marker, RegistrationNumber = marker, TaxNumber = marker, ContactName = "Test", ContactEmail = "test@example.invalid", ContactPhone = "+96265550100", Status = SupplierStatus.Approved });
            await db.SaveChangesAsync();
        }
        async Task Login(HttpClient client, string suffix)
        {
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["UserName"] = marker + "-" + suffix, ["Password"] = password, ["__RequestVerificationToken"] = await Token(client, "/Account/Login") }));
            Check(response.StatusCode == HttpStatusCode.Redirect, "fixture sign-in: " + suffix);
        }
        Dictionary<string,string> Fields(string suffix) => new() { ["Title"] = marker + " " + suffix, ["Reason"] = "Temporary requisition test", ["Priority"] = "Medium", ["TargetDate"] = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"),
            ["Items[0].Description"] = "Material", ["Items[0].Quantity"] = "0.125", ["Items[0].Specifications"] = "Required specification", ["command"] = "SaveDraft" };
        async Task<HttpResponseMessage> Create(HttpClient client, Dictionary<string,string> fields) { fields["__RequestVerificationToken"] = await Token(client, "/Requisitions/Create"); return await client.PostAsync("/Requisitions/Create", new FormUrlEncodedContent(fields)); }
        async Task<Guid> CreateId(HttpClient client, Dictionary<string,string> fields)
        {
            var response = await Create(client, fields); if (response.StatusCode != HttpStatusCode.Redirect) throw new Exception("Create failed: " + await response.Content.ReadAsStringAsync());
            return Guid.Parse(response.Headers.Location!.OriginalString.Split('/').Last());
        }
        async Task<HttpResponseMessage> Edit(HttpClient client, Guid id, Dictionary<string,string> fields) { fields["__RequestVerificationToken"] = await Token(client, "/Requisitions/Edit/" + id); return await client.PostAsync("/Requisitions/Edit/" + id, new FormUrlEncodedContent(fields)); }
        async Task<HttpResponseMessage> Submit(HttpClient client, Guid id, string? version = null)
        {
            version ??= (await Details(id, "owner")).Version;
            return await client.PostAsync("/Requisitions/Submit/" + id, new FormUrlEncodedContent(new Dictionary<string,string> { ["Version"] = version, ["__RequestVerificationToken"] = await Token(client, "/") }));
        }
        async Task<HttpResponseMessage> Review(HttpClient client, Guid id, string decision, string comments = "")
        {
            var version = (await Details(id, "officer")).Version;
            return await client.PostAsync("/Requisitions/Review/" + id, new FormUrlEncodedContent(new Dictionary<string,string> { ["Version"] = version, ["Decision"] = decision, ["Comments"] = comments, ["__RequestVerificationToken"] = await Token(client, "/") }));
        }
        async Task<RequisitionDetails> Details(Guid id, string suffix)
        {
            using var scope = app.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<RequisitionManagement>(); return await service.DetailsAsync(id, await service.ActorAsync(Principal(ids[suffix])));
        }
        async Task<int> RequestCount() { using var scope = app.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PurchaseRequisitions.CountAsync(x => x.CreatedByUserId == ids["owner"]); }
    }
    private static ClaimsPrincipal Principal(string id) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "Test"));
    private static bool Denied(HttpResponseMessage response) => response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound || response.Headers.Location?.OriginalString.Contains("AccessDenied") == true;
    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path); var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""); if (!match.Success) throw new Exception("Missing token: " + path); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static Task<string> UserSnapshot(ApplicationDbContext db) => SnapshotUsers(db);
    private static async Task<string> SnapshotUsers(ApplicationDbContext db) => JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
    private static async Task<string> MembershipSnapshot(ApplicationDbContext db) => JsonSerializer.Serialize(await db.UserRoles.AsNoTracking().OrderBy(x => x.UserId).ThenBy(x => x.RoleId).ToListAsync());
    private static async Task<Dictionary<string,long>> Counts(ApplicationDbContext database)
    {
        await database.Database.OpenConnectionAsync(); var counts = new Dictionary<string,long>();
        foreach (var table in database.Model.GetEntityTypes().Select(x => x.GetTableName()!).Distinct().Append("__EFMigrationsHistory"))
        { await using var command = database.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[" + table.Replace("]", "]]") + "]"; counts[table] = (long)(await command.ExecuteScalarAsync())!; }
        return counts;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
    private sealed class SyntheticAuditException : Exception { }
    private sealed class AuditFailure : SaveChangesInterceptor
    {
        public bool FailNext { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (FailNext && eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added)) { FailNext = false; throw new SyntheticAuditException(); }
            return ValueTask.FromResult(result);
        }
    }
}
