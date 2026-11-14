using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SourcingTenderManagementSystem.AuthenticationChecks;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Security;
using SourcingTenderManagementSystem.Web.Data;

if (args.Contains("--compliance-sql-checks-only", StringComparer.Ordinal)) {
    await ComplianceSqlChecks.RunAsync();
    return;
}
if (args.Contains("--compliance-checks-only", StringComparer.Ordinal)) {
    await ComplianceChecks.RunAsync();
    await BidPhase3Checks.RunAsync(complianceChecks: true);
    return;
}
if (args.Contains("--compliance-browser-fixture", StringComparer.Ordinal)) {
    await BidPhase3Checks.RunAsync(browserFixture: true, complianceChecks: true);
    return;
}
if (args.Contains("--bid-phase4-browser-fixture", StringComparer.Ordinal)) {
    await BidPhase3Checks.RunAsync(browserFixture: true, phase4Checks: true);
    return;
}

if (args.Contains("--bid-phase4-checks-only", StringComparer.Ordinal)) {
    await BidPhase3Checks.RunAsync(phase4Checks: true);
    return;
}

if (args.Contains("--admin-dashboard-readonly-checks", StringComparer.Ordinal)) { await AdminDashboardChecks.SqlReadOnlyAsync(); return; }
if (args.Contains("--admin-dashboard-checks-only", StringComparer.Ordinal) || args.Contains("--admin-dashboard-browser-fixture", StringComparer.Ordinal)) {
    await BidPhase3Checks.RunAsync(args.Contains("--admin-dashboard-browser-fixture", StringComparer.Ordinal), adminChecks: true); return;
}

// Internal UI fixture: memory stores only, and returns before any SQL-capable runner.
if (args.Contains("--workspace-ui-checks-only", StringComparer.Ordinal) || args.Contains("--workspace-ui-browser-fixture", StringComparer.Ordinal))
{
    await BidPhase3Checks.RunAsync(args.Contains("--workspace-ui-browser-fixture", StringComparer.Ordinal), workspaceChecks: true);
    return;
}

// This branch registers no SQL provider and returns before the existing SQL/Identity integration runner.
if (args.Contains("--bid-phase3-checks-only", StringComparer.Ordinal) || args.Contains("--bid-phase3-browser-fixture", StringComparer.Ordinal))
{
    var browserFixture = args.Contains("--bid-phase3-browser-fixture", StringComparer.Ordinal);
    await BidPhase3Checks.RunAsync(browserFixture);
    if (browserFixture) return;
    await BidPhase2Checks.RunAsync();
    await BidPhase1Checks.RunAsync();
    TenderRuleChecks.Run();
    RequisitionRuleChecks.Run();
    await SupplierUploadChecks.RunAsync();
    return;
}

// Phase 2 mode uses only private temporary files, memory transactions, and connection-free model queries.
if (args.Contains("--bid-phase2-checks-only", StringComparer.Ordinal))
{
    await BidPhase2Checks.RunAsync();
    await BidPhase1Checks.RunAsync();
    TenderRuleChecks.Run();
    RequisitionRuleChecks.Run();
    await SupplierUploadChecks.RunAsync();
    return;
}

// Explicit database-free Phase 1 mode: returns before hosts, SQL suites or fixture creation.
if (args.Contains("--bid-phase1-checks-only", StringComparer.Ordinal))
{
    await BidPhase1Checks.RunAsync();
    TenderRuleChecks.Run();
    RequisitionRuleChecks.Run();
    return;
}

if(args.Contains("--tender-sql-checks-only",StringComparer.Ordinal))
{
    await TenderSqlChecks.RunAsync();
    return;
}

if (args.Contains("--requisition-sql-checks-only", StringComparer.Ordinal))
{
    await RequisitionSqlChecks.RunAsync();
    return;
}

if (args.Contains("--supplier-sql-checks-only", StringComparer.Ordinal))
{
    await SupplierSqlChecks.RunAsync();
    return;
}

if (args.Contains("--employee-sql-checks-only", StringComparer.Ordinal))
{
    await EmployeeSqlChecks.RunAsync();
    return;
}

if (args.Contains("--provisioning-checks-only", StringComparer.Ordinal))
{
    await FirstAdminChecks.RunAsync();
    return;
}

using var key = RSA.Create(2048);
var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var generatedCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
// Windows Schannel requires an imported key container for the local TLS listener.
using var certificate = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pfx), null);
var experienceBrowser = args.Contains("--experience-browser-fixture", StringComparer.Ordinal);
var webSource = Path.Combine(Directory.GetCurrentDirectory(), "src", "SourcingTenderManagementSystem.Web");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(AccountController).Assembly.GetName().Name,
    EnvironmentName = "Development",
    ContentRootPath = experienceBrowser ? webSource : null,
    WebRootPath = experienceBrowser ? Path.Combine(webSource, "wwwroot") : null
});
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ProtectionController).Assembly);
builder.Services.AddSingleton<IPublicStatistics, UnavailablePublicStatistics>();
builder.Services.AddSingleton<IAdminDashboard>(s => new TestAdminDashboard(s.GetRequiredService<IHttpContextAccessor>(), TimeProvider.System));
builder.Services.AddSingleton<IRoleDashboard, TestRoleDashboard>();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer("Server=localhost;Database=NoSqlSupplierGetOnly;Integrated Security=True;TrustServerCertificate=True"));
builder.Services.AddScoped<IPrivateSupplierFiles, PrivateSupplierFiles>();
builder.Services.AddScoped<SupplierRegistration>();
builder.Services.AddScoped<SupplierReview>();
builder.Services.AddScoped<RequisitionManagement>();
builder.Services.AddScoped<TenderManagement>();
builder.Services.AddSingleton(TimeProvider.System);
var usersStore = new MemoryUsers();
var rolesStore = new MemoryRoles();
builder.Services.AddSingleton<IUserStore<IdentityUser>>(usersStore);
builder.Services.AddSingleton<IRoleStore<IdentityRole>>(rolesStore);
builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddDefaultTokenProviders();
builder.Services.AddAuthenticationFoundation();
// Exercise real Identity/cookie/controller behavior without touching production SQL.
var eligibility = new TestEligibility(usersStore);
builder.Services.RemoveAll<IEmployeeEligibility>();
builder.Services.AddSingleton<IEmployeeEligibility>(eligibility);
var departmentStore = new MemoryDepartments();
builder.Services.AddSingleton<IDepartmentStore>(departmentStore);
var employeeData = new MemoryEmployeeData(usersStore, eligibility.SupplierProfileUsers);
var administrationId = Guid.NewGuid();
employeeData.Departments[administrationId] = new() { Id = administrationId, Name = "Administration" };
eligibility.DepartmentAssignments = employeeData.Assignments;
builder.Services.AddSingleton<IEmployeeData>(employeeData);
builder.Services.AddScoped<EmployeeManagement>();
builder.Services.AddScoped<IProfileTransactions, MemoryProfileTransactions>();
builder.Services.AddScoped<ProfileManagement>();
await using var app = builder.Build();
if (experienceBrowser) {
    app.Use(async (context, next) => { context.Response.Headers["X-STMS-Experience-Fixture"] = "memory-only"; await next(); });
    app.UseStaticFiles();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

string password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
if (experienceBrowser) password = Environment.GetEnvironmentVariable("STMS_UI_TEST_PASSWORD") ?? throw new InvalidOperationException("Supply an ephemeral test password through STMS_UI_TEST_PASSWORD.");
IdentityUser employee;
IdentityUser supplier;
using (var scope = app.Services.CreateScope())
{
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await roles.CreateAsync(new IdentityRole("ExistingRole"));
    await RoleInitializer.InitializeAsync(roles);
    var before = rolesStore.Roles.Keys.Order().ToArray();
    await RoleInitializer.InitializeAsync(roles);
    Check(before.SequenceEqual(rolesStore.Roles.Keys.Order()) && before.Length == 8,
        "role initialization is idempotent and preserves existing roles");
    Check(ApplicationRoles.All.All(role => rolesStore.Roles.Values.Any(existing => existing.Name == role)), "all seven required roles exist");
    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    employee = await Create("employee", ApplicationRoles.RequestingDepartment);
    supplier = await Create("supplier", ApplicationRoles.Supplier);
    await Create("unassigned", null);
    await Create("department-admin", ApplicationRoles.Admin);
    await Create("admin", ApplicationRoles.Admin);
    async Task<IdentityUser> Create(string name, string? role)
    {
        var user = new IdentityUser(name);
        Check((await users.CreateAsync(user, password)).Succeeded, $"temporary {name} fixture created with Identity hashing");
        if (role is not null) Check((await users.AddToRoleAsync(user, role)).Succeeded, $"temporary {name} role assigned");
        if (role != ApplicationRoles.Supplier) employeeData.Assignments[user.Id] = administrationId;
        return user;
    }
}
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var handler = new HttpClientHandler
{
    AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate.Thumbprint
};
using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
try
{
    if (experienceBrowser) { Console.WriteLine("EXPERIENCE_BROWSER_FIXTURE " + address); await app.WaitForShutdownAsync(); return; }
    if (args.Contains("--experience-checks-only", StringComparer.Ordinal)) {
        ExperienceChecks.QueryChecks();
        await ExperienceChecks.ProfileAsync(client, () => new HttpClient(new HttpClientHandler { AllowAutoRedirect=false, ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert?.Thumbprint==certificate.Thumbprint }) { BaseAddress=client.BaseAddress }, usersStore, password);
        return;
    }
    TenderRuleChecks.Run();
    RequisitionRuleChecks.Run();
    await SupplierUploadChecks.RunAsync();
    var registrationPage = await client.GetAsync("/Suppliers/Register");
    Check(registrationPage.StatusCode == HttpStatusCode.OK && (await registrationPage.Content.ReadAsStringAsync()).Contains("multipart/form-data"), "supplier registration is anonymous and supports uploads");
    Check((await client.PostAsync("/Suppliers/Register", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "supplier registration requires antiforgery");
    Check((await client.GetAsync("/Suppliers")).StatusCode == HttpStatusCode.Redirect, "anonymous supplier administration is denied");
    var publicHome = await client.GetAsync("/");
    var publicHtml = await publicHome.Content.ReadAsStringAsync();
    Check(publicHome.StatusCode == HttpStatusCode.OK && publicHtml.Contains("Unified Platform for"), "public home is accessible without authentication");
    // Public metrics must distinguish unavailable data from a genuine zero.
    if (!args.Contains("--workspace-auth-regressions-only", StringComparer.Ordinal)) {
        Check(publicHtml.Contains("Unavailable") && !publicHtml.Contains("data-count=\"0\""), "unavailable statistics are not misreported as zero");
        Check(publicHtml.Contains("Public contact details are not yet configured") && publicHtml.Contains("Register your company"), "public home retains Contact dependency and links implemented Supplier Registration");
    }
    using var employeeSessionHandler = new HttpClientHandler
    {
        AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate.Thumbprint
    };
    using var employeeSession = new HttpClient(employeeSessionHandler) { BaseAddress = client.BaseAddress };
    await EmployeeChecks.RunAsync(client, employeeSession, usersStore, eligibility, employeeData, password);
    await DepartmentChecks.RunAsync(client, departmentStore, password);
    var page = await client.GetAsync("/Account/Login");
    var html = await page.Content.ReadAsStringAsync();
    Check(page.StatusCode == HttpStatusCode.OK && html.Contains("autocomplete=\"current-password\"") && html.Contains("jquery.validate"), "login Razor view renders with client validation");
    Check(page.Headers.CacheControl?.NoStore == true, "authentication page is not cached");
    await Expect("/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
    await Expect("/Protection/Fallback", HttpStatusCode.Redirect, "/Account/Login");
    Check((await client.PostAsync("/Account/Login", Form("employee", password, null))).StatusCode == HttpStatusCode.BadRequest, "login without antiforgery is rejected");
    await Reject("employee", "incorrect-password", "incorrect credentials");
    await Reject("missing", password, "unknown account");
    await Reject("supplier", password, "supplier cannot use employee authentication");
    await Reject("unassigned", password, "account without assigned role");
    var invalid = await client.PostAsync("/Account/Login", Form("", "", await Token("/Account/Login")));
    Check((await invalid.Content.ReadAsStringAsync()).Contains("field is required"), "server-side required-field validation");
    var signedIn = await Login("employee", password, "https://example.invalid/escape");
    Check(signedIn.StatusCode == HttpStatusCode.Redirect && signedIn.Headers.Location?.OriginalString == "/Workspace", "valid login opens the internal dashboard and discards external return URLs");
    var authCookie = signedIn.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-SourcingTender.Auth="));
    Check(authCookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && authCookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && authCookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) && !authCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase), "secure HttpOnly SameSite session cookie");
    // Authenticate first: the fallback policy challenges anonymous unknown routes.
    await Expect("/Account/Register", HttpStatusCode.NotFound);
    await Expect("/Identity/Account/Register", HttpStatusCode.NotFound);
    await Expect("/Account/SupplierRegistration", HttpStatusCode.NotFound);
    var registerPost = await client.PostAsync("/Account/Register", Form("employee", password, await Token("/Account/Login")));
    Check(registerPost.StatusCode == HttpStatusCode.NotFound, "employee registration POST is unavailable");
    await Expect("/Protection/Employee", HttpStatusCode.OK);
    await Expect("/Protection/Fallback", HttpStatusCode.OK);
    await Expect("/Protection/Admin", HttpStatusCode.Redirect, "/Account/AccessDenied");
    await Expect("/Account/AccessDenied", HttpStatusCode.Forbidden);
    Check((await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "logout without antiforgery is rejected");
    await Expect("/Account/Logout", HttpStatusCode.MethodNotAllowed);
    var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token("/Workspace") }));
    Check(logout.StatusCode == HttpStatusCode.Redirect, "authenticated antiforgery-protected logout succeeds");
    await Expect("/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");

    var local = await Login("employee", password, "/Protection/Employee");
    Check(local.Headers.Location?.OriginalString == "/Protection/Employee", "local return URL is preserved");
    using (var scope = app.Services.CreateScope())
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await users.AddToRoleAsync(employee, ApplicationRoles.Admin);
        await Expect("/Protection/Admin", HttpStatusCode.OK);
        await users.RemoveFromRoleAsync(employee, ApplicationRoles.Admin);
        await Expect("/Protection/Admin", HttpStatusCode.Redirect, "/Account/AccessDenied");
        await users.UpdateSecurityStampAsync(employee);
        await Expect("/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
        await Login("employee", password);
        await users.SetLockoutEndDateAsync(employee, DateTimeOffset.UtcNow.AddMinutes(5));
        await Expect("/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
        await Reject("employee", password, "locked account");
        await users.SetLockoutEndDateAsync(employee, null);
        await Login("employee", password);
        eligibility.SupplierProfileUsers.Add(employee.Id);
        await Expect("/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
        await Reject("employee", password, "supplier-profile account without supplier role");
        eligibility.SupplierProfileUsers.Clear();
        await Login("employee", password);
        await users.RemoveFromRoleAsync(employee, ApplicationRoles.RequestingDepartment);
        await Expect("/Protection/Employee", HttpStatusCode.Redirect, "/Account/Login");
        await users.AddToRoleAsync(employee, ApplicationRoles.RequestingDepartment);
        for (var i = 0; i < 5; i++) await Reject("employee", "wrong-password", "failed-password attempt");
        Check(await users.IsLockedOutAsync(employee), "Identity lockout triggers after repeated failed passwords");
    }
    Console.WriteLine("PASS: HTTPS MVC authentication checks completed; no SQL data or persistent test users created.");
}
finally { await app.StopAsync(); }

void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}
async Task Expect(string path, HttpStatusCode status, string? location = null)
{
    var response = await client.GetAsync(path);
    Check(response.StatusCode == status && (location is null || response.Headers.Location?.OriginalString.Contains(location) == true), $"{path} returns {(int)status}" + (location is null ? "" : $" to {location}"));
}
async Task<string> Token(string path)
{
    var response = await client.GetAsync(path);
    if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString == "/Workspace") response = await client.GetAsync("/Workspace");
    var html = await response.Content.ReadAsStringAsync();
    var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    Check(match.Success, "antiforgery token rendered");
    return WebUtility.HtmlDecode(match.Groups[1].Value);
}
FormUrlEncodedContent Form(string userName, string secret, string? token, string? returnUrl = null)
{
    var values = new Dictionary<string, string> { ["UserName"] = userName, ["Password"] = secret, ["ReturnUrl"] = returnUrl ?? "" };
    if (token is not null) values["__RequestVerificationToken"] = token;
    return new FormUrlEncodedContent(values);
}
async Task<HttpResponseMessage> Login(string name, string secret, string? returnUrl = null) =>
    await client.PostAsync("/Account/Login", Form(name, secret, await Token("/Account/Login"), returnUrl));
async Task Reject(string name, string secret, string reason)
{
    var response = await Login(name, secret);
    var html = await response.Content.ReadAsStringAsync();
    Check(response.StatusCode == HttpStatusCode.OK && html.Contains("Unable to sign in.") && !html.Contains(secret), "rejects " + reason + " without echoing password");
    Check(!response.Headers.TryGetValues("Set-Cookie", out var cookies) || !cookies.Any(value => value.StartsWith("__Host-SourcingTender.Auth=")), "rejected login grants no authentication cookie");
}

public sealed class TestEligibility(MemoryUsers users) : IEmployeeEligibility
{
    public Dictionary<string, Guid> DepartmentAssignments { get; set; } = [];
    public HashSet<string> SupplierProfileUsers { get; } = [];
    public Task<bool> IsEligibleAsync(IdentityUser user)
    {
        var roles = users.Roles.GetValueOrDefault(user.Id) ?? [];
        return Task.FromResult(!roles.Contains(ApplicationRoles.Supplier.ToUpperInvariant()) &&
            DepartmentAssignments.ContainsKey(user.Id) &&
            ApplicationRoles.EmployeeRoles.Any(role => roles.Contains(role.ToUpperInvariant())) &&
            !SupplierProfileUsers.Contains(user.Id));
    }
}

// Test assembly only: no diagnostic/probe endpoints ship in the MVC application.
public sealed class ProtectionController : Controller
{
    [Authorize] public IActionResult Employee() => Content("Authorized");
    public IActionResult Fallback() => Content("Authorized by fallback");
    [Authorize(Policy = ApplicationRoles.Admin)] public IActionResult Admin() => Content("Admin only");
}

public sealed class UnavailablePublicStatistics : IPublicStatistics
{
    public Task<SourcingTenderManagementSystem.Web.Models.PublicHomeViewModel> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SourcingTenderManagementSystem.Web.Models.PublicHomeViewModel(null, null, null));
}
