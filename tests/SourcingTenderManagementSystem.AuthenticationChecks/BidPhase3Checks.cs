using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Runs the real compiled Razor views, MVC filters/binding and bid services, with no SQL provider/store registered.
public static class BidPhase3Checks
{
    private static int checks;
    public static async Task RunAsync(bool browserFixture = false, bool workspaceChecks = false, bool adminChecks = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "bid-mvc-checks-" + Guid.NewGuid().ToString("N"));
        var source = Path.GetFullPath("src/SourcingTenderManagementSystem.Web");
        var f = new BidPhase1Checks.Fixture();
        f.Context.Tender.Title = "Private network equipment tender";
        f.Store.Accounts["other"] = f.Account with { UserId = "other", SupplierId = Guid.NewGuid() };
        f.Store.Accounts["mixed"] = f.Account with { UserId = "mixed", Roles = [ApplicationRoles.Admin, ApplicationRoles.Supplier] };
        f.Store.Accounts["suspended"] = f.Account with { UserId = "suspended", SupplierApproved = false };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(BidsController).Assembly.GetName().Name,
            EnvironmentName = "Development", ContentRootPath = source, WebRootPath = Path.Combine(source, "wwwroot") });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddControllersWithViews();
        builder.Services.Configure<MvcOptions>(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        builder.Services.AddAuthentication("BidPhase3Only").AddScheme<AuthenticationSchemeOptions, BidMvcTestAuthentication>("BidPhase3Only", _ => { });
        builder.Services.AddAuthorization(options => {
            options.AddPolicy("SignedInAccount", policy => policy.RequireAuthenticatedUser());
            foreach (var role in ApplicationRoles.EmployeeRoles) options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
        });
        builder.Services.AddSingleton<IPublicStatistics, UnavailablePublicStatistics>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAdminDashboard>(s => new TestAdminDashboard(s.GetRequiredService<IHttpContextAccessor>(), f.Clock, adminChecks));
        var departments = new MemoryDepartments();
        var initialDepartment = new Department { Id = Guid.NewGuid(), Name = "Administration" };
        departments.Departments[initialDepartment.Id] = initialDepartment;
        builder.Services.AddSingleton<IDepartmentStore>(departments);
        var memoryUsers = new MemoryUsers(); var memoryRoles = new MemoryRoles();
        var employeeData = new MemoryEmployeeData(memoryUsers, []);
        employeeData.Departments[initialDepartment.Id] = initialDepartment;
        builder.Services.AddSingleton<IUserStore<IdentityUser>>(memoryUsers);
        builder.Services.AddSingleton<IRoleStore<IdentityRole>>(memoryRoles);
        builder.Services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddSignInManager();
        builder.Services.AddSingleton<IEmployeeData>(employeeData);
        builder.Services.AddScoped<EmployeeManagement>();
        builder.Services.AddSingleton<TimeProvider>(f.Clock);
        builder.Services.AddSingleton<IBidData>(f.Store);
        builder.Services.AddSingleton<IBidDocumentData>(f.Store);
        builder.Services.AddSingleton<IPrivateBidFiles>(new PrivateBidFiles(new FilesEnvironment(root)));
        builder.Services.AddScoped<IBidSubmissionDocuments, BidSubmissionDocuments>();
        builder.Services.AddScoped<BidManagement>();
        builder.Services.AddScoped<BidDocumentManagement>();
        builder.Services.AddScoped<SupplierBidPages>();
        await using var app = builder.Build();
        app.Use(async (context, next) => { context.Response.Headers["X-STMS-Bid-Fixture"] = "memory-only"; await next(); });
        app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        try
        {
            using (var scope = app.Services.CreateScope())
                foreach (var role in ApplicationRoles.All)
                    await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(new IdentityRole(role));
            await app.StartAsync();
            var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            if (browserFixture)
            {
                Console.WriteLine($"BID_BROWSER_FIXTURE {url} tender={f.Context.Tender.Id:D} temporary-storage={root}");
                await app.WaitForShutdownAsync(); return;
            }
            checks = 0;
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = new(url) };
            if (adminChecks) { await AdminDashboardChecks.HttpAsync(client, (TestAdminDashboard)app.Services.GetRequiredService<IAdminDashboard>()); return; }
            if (workspaceChecks) { await WorkspaceUiChecks.RunAsync(client); return; }
            client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", "supplier");
            async Task<HttpResponseMessage> Get(string path, string actor = "supplier")
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Add("X-Bid-Test-Actor", actor);
                // Per-request values override the default header in HttpClient.
                return await client.SendAsync(request);
            }
            async Task<string> Token()
            {
                var html = await (await client.GetAsync("/Bids")).Content.ReadAsStringAsync();
                var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                Check(match.Success, "Razor generates antiforgery token");
                return WebUtility.HtmlDecode(match.Groups[1].Value);
            }
            async Task<HttpResponseMessage> Post(string path, Dictionary<string, string> fields, bool token = true)
            {
                if (token) fields["__RequestVerificationToken"] = await Token();
                return await client.PostAsync(path, new FormUrlEncodedContent(fields));
            }
            Dictionary<string, string> Form() => new() { ["command"] = "Save", ["Form.DeliveryTimeframe"] = "Two weeks",
                ["Form.TechnicalDetails"] = "<script>alert('private')</script>", ["Form.PaymentTerms"] = "30 days",
                ["Form.OfferValidUntil"] = "2026-12-01", ["Form.Items[0].ItemId"] = f.Context.Items[0].Id.ToString(), ["Form.Items[0].UnitPrice"] = "1.234567",
                ["Form.Items[1].ItemId"] = f.Context.Items[1].Id.ToString(), ["Form.Items[1].UnitPrice"] = "2" };
            var create = $"/Bids/Create?tenderId={f.Context.Tender.Id}";
            foreach (var actor in new[] { "anonymous", "admin", "officer", "mixed", "suspended" })
                Check((await Get("/Bids", actor)).StatusCode == (actor == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), "supplier MVC authorization denies " + actor);
            var editor = await client.GetAsync(create); var html = await editor.Content.ReadAsStringAsync();
            Check(editor.StatusCode == HttpStatusCode.OK && html.Contains("Form.Items[0].ItemId") && html.Contains("1.25") && html.Contains("2.5"), "compiled create view uses every authoritative item and quantity");
            Check(html.Contains("My Offers") && html.Contains("Save your draft first"), "navigation and pre-save document guidance render");
            Check(f.Store.Rows.Count == 0, "GET create does not save a bid");
            f.Store.Participation.Invitation.Status = InvitationStatus.Pending;
            Check((await client.GetAsync(create)).StatusCode == HttpStatusCode.Forbidden, "GET create requires Accepted invitation");
            f.Store.Participation.Invitation.Status = InvitationStatus.Accepted;
            f.Context.Tender.Status = TenderStatus.Published;
            Check((await client.GetAsync(create)).StatusCode == HttpStatusCode.Conflict, "GET create rejects tender outside Open state");
            f.Context.Tender.Status = TenderStatus.Open;
            Check((await Post(create, Form(), false)).StatusCode == HttpStatusCode.BadRequest && f.Store.Rows.Count == 0, "MVC antiforgery blocks bid creation");
            var invalid = Form(); invalid["Form.Items[1].ItemId"] = Guid.NewGuid().ToString();
            html = await (await Post(create, invalid)).Content.ReadAsStringAsync();
            Check(f.Store.Rows.Count == 0 && html.Contains("foreign, missing and duplicate identifiers"), "manipulated item identifiers rejected before persistence");
            invalid = Form(); invalid["Form.Items[0].UnitPrice"] = "not-a-decimal";
            html = await (await Post(create, invalid)).Content.ReadAsStringAsync();
            Check(f.Store.Rows.Count == 0 && html.Contains("field-validation-error") && html.Contains("not-a-decimal"), "numeric binding errors preserve input and field validation");
            var posted = Form(); posted["Status"] = "Submitted"; posted["SupplierProfileId"] = Guid.NewGuid().ToString(); posted["Form.Items[0].Quantity"] = "999";
            Check((await Post(create, posted)).StatusCode == HttpStatusCode.Redirect, "prefixed draft form binds and redirects after save");
            var id = f.Store.Rows.Keys.Single(); var s = f.Store.Rows[id];
            Check(s.Bid.Status == BidStatus.Draft && s.Items.Single(x => x.PurchaseRequisitionItemId == f.Context.Items[0].Id).OfferedQuantity == 1.25m, "posted status, supplier and quantity cannot override service authority");
            var edit = $"/Bids/Edit/{id}"; var details = $"/Bids/Details/{id}"; var submit = $"/Bids/Submit/{id}";
            html = await (await client.GetAsync(details)).Content.ReadAsStringAsync();
            Check(html.Contains("&lt;script&gt;") && !html.Contains("<script>alert"), "confidential text is Razor-encoded");
            var version = BidRules.Version(f.Store.Rows[id]);
            var add = Form(); add["command"] = "AddCharge"; add["Form.Version"] = version;
            html = await (await Post(edit, add)).Content.ReadAsStringAsync();
            Check(html.Contains("Form.Charges[0].CostType") && BidRules.Version(f.Store.Rows[id]) == version, "optional cost row works without JavaScript and does not persist");
            posted = Form(); posted["Form.Version"] = version; posted["Form.Charges[0].CostType"] = "Tax"; posted["Form.Charges[0].Amount"] = "0.1";
            Check((await Post(edit, posted)).StatusCode == HttpStatusCode.Redirect && f.Store.Rows[id].Charges.Single().Amount == .1m, "draft edit binds optional nonnegative costs");
            Check((await Post(edit, posted)).StatusCode == HttpStatusCode.Conflict, "stale commercial form rejected");
            html = await (await client.GetAsync(submit)).Content.ReadAsStringAsync();
            Check(html.Contains("Not ready for submission") && !html.Contains("data-bid-submit"), "confirmation explains missing Technical Proposal and blocks final action");
            foreach (var path in new[] { details, edit, submit, $"/Bids/Summary/{id}" })
                Check((await Get(path, "other")).StatusCode == HttpStatusCode.NotFound, "competitor cannot access supplier page/export");
            async Task<HttpResponseMessage> Upload(string path, string versionValue, string filename, byte[] bytes, bool token = true)
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(versionValue), "Version"); content.Add(new StringContent("Technical Proposal"), "DocumentType");
                content.Add(new ByteArrayContent(bytes), "File", filename);
                if (token) content.Add(new StringContent(await Token()), "__RequestVerificationToken");
                return await client.PostAsync(path, content);
            }
            var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nprivate bid\n%%EOF");
            var upload = $"/Bids/Upload/{id}";
            version = BidRules.Version(f.Store.Rows[id]);
            Check((await Upload(upload, version, "proposal.pdf", pdf, false)).StatusCode == HttpStatusCode.BadRequest, "upload requires antiforgery");
            Check((await Upload(upload, version, "bad.pdf", [1, 2, 3])).StatusCode == HttpStatusCode.BadRequest && f.Store.Rows[id].Documents.Count == 0, "multipart signature failure returns useful error without saving metadata");
            Check((await Upload(upload, version, "proposal.pdf", pdf)).StatusCode == HttpStatusCode.Redirect, "multipart upload reaches private document service");
            var documentId = f.Store.Rows[id].Documents.Single().Id;
            var downloadPath = $"/Bids/Document/{id}?documentId={documentId}";
            var download = await client.GetAsync(downloadPath);
            Check(download.StatusCode == HttpStatusCode.OK && (await download.Content.ReadAsByteArrayAsync()).SequenceEqual(pdf), "protected MVC download returns verified bytes");
            Check(download.Content.Headers.ContentDisposition?.DispositionType == "attachment" && download.Content.Headers.ContentType?.MediaType == "application/octet-stream" &&
                download.Headers.CacheControl?.NoStore == true && download.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff" && download.Headers.Contains("Content-Security-Policy"), "HTTP download applies attachment, no-store, nosniff and sandbox headers");
            Check((await Get(downloadPath, "other")).StatusCode == HttpStatusCode.NotFound, "competing supplier download denied");
            Check((await Get(downloadPath, "admin")).StatusCode == HttpStatusCode.Forbidden, "Admin cannot use supplier download endpoint");
            version = BidRules.Version(f.Store.Rows[id]);
            Check((await Upload($"/Bids/Replace/{id}?documentId={documentId}", version, "revised.pdf", pdf)).StatusCode == HttpStatusCode.Redirect && f.Store.Rows[id].Documents.Single().Id == documentId, "replacement route binds bid and document independently");
            version = BidRules.Version(f.Store.Rows[id]);
            Check((await Post($"/Bids/RemoveDocument/{id}", new() { ["documentId"] = documentId.ToString(), ["version"] = version })).StatusCode == HttpStatusCode.Redirect && f.Store.Rows[id].Documents.Count == 0, "remove route integrates private retirement service");
            Check((await Upload(upload, BidRules.Version(f.Store.Rows[id]), "final.pdf", pdf)).StatusCode == HttpStatusCode.Redirect, "technical document restored before submission");
            var summary = await client.GetAsync($"/Bids/Summary/{id}");
            var text = await summary.Content.ReadAsStringAsync();
            Check(summary.StatusCode == HttpStatusCode.OK && summary.Content.Headers.ContentDisposition?.FileNameStar?.EndsWith(".txt") == true && text.Contains("DRAFT") && text.Contains("6.64320875 JOD") && !text.Contains("StorageReference") && !text.Contains("App_Data"), "own UTF-8 summary has exact totals, Draft warning and no storage paths");
            html = await (await client.GetAsync(submit)).Content.ReadAsStringAsync();
            Check(html.Contains("data-bid-submit") && html.Contains("Asia/Amman") && f.Store.Rows[id].Bid.Status == BidStatus.Draft, "confirmation verifies real documents without changing bid status");
            version = BidRules.Version(f.Store.Rows[id]);
            Check((await Post(submit, new() { ["Version"] = version, ["Confirmed"] = "true" }, false)).StatusCode == HttpStatusCode.BadRequest, "final submission requires antiforgery");
            html = await (await Post(submit, new() { ["Version"] = version, ["Confirmed"] = "false" })).Content.ReadAsStringAsync();
            Check(html.Contains("Confirm that you understand") && f.Store.Rows[id].Bid.Status == BidStatus.Draft, "final submission requires explicit confirmation");
            Check((await Post(submit, new() { ["Version"] = "stale", ["Confirmed"] = "true" })).StatusCode == HttpStatusCode.Conflict, "stale confirmation cannot submit");
            var final = new Dictionary<string, string> { ["Version"] = version, ["Confirmed"] = "true" };
            Check((await Post(submit, final)).StatusCode == HttpStatusCode.Redirect && f.Store.Rows[id].Bid.SubmittedAt?.Offset == TimeSpan.Zero, "explicit final submit persists UTC submission via existing service");
            Check((await Post(submit, final)).StatusCode == HttpStatusCode.Conflict, "duplicate final submit rejected server-side");
            Check((await client.GetAsync(edit)).StatusCode == HttpStatusCode.Conflict, "submitted bid editor is inaccessible");
            html = await (await client.GetAsync(details)).Content.ReadAsStringAsync();
            Check(!html.Contains("Edit draft &amp; documents") && !html.Contains("Review &amp; submit"), "submitted details are read-only");
            Check((await Upload(upload, BidRules.Version(f.Store.Rows[id]), "late.pdf", pdf)).StatusCode == HttpStatusCode.Conflict, "submitted documents cannot be changed through direct HTTP request");
            Check((await client.GetAsync($"/Bids/RemoveDocument/{id}?documentId={documentId}")).StatusCode == HttpStatusCode.MethodNotAllowed, "document modifications never run on GET");
            Console.WriteLine($"PASS: {checks} database-free Phase 3 MVC checks (compiled Razor, real HTTP/antiforgery, memory bid data, private temporary files).");
        }
        finally
        {
            await app.StopAsync();
            var full = Path.GetFullPath(root); var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("bid-mvc-checks-", StringComparison.Ordinal)) throw new Exception("Unsafe test cleanup path");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
    private static void Check(bool valid, string label) { if (!valid) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
    private sealed class FilesEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BidMvcFiles"; public string EnvironmentName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root; public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

// Test assembly only. Production uses its unchanged Identity cookie pipeline, never this header scheme.
public sealed class BidMvcTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerActor = Request.Headers["X-Bid-Test-Actor"].FirstOrDefault();
        // Chromium's native download manager sends cookies, but not Playwright page extraHTTPHeaders.
        // This isolated, in-memory test scheme therefore also accepts its own temporary fixture cookie.
        var actor = headerActor ?? Request.Cookies["BidPhase3FixtureActor"];
        if (headerActor is not null) Response.Cookies.Append("BidPhase3FixtureActor", headerActor,
            new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict, Path = "/" });
        if (string.IsNullOrEmpty(actor) || actor == "anonymous") return Task.FromResult(AuthenticateResult.NoResult());
        var role = actor switch { "admin" => ApplicationRoles.Admin, "officer" => ApplicationRoles.SourcingOfficer,
            "requester" => ApplicationRoles.RequestingDepartment, "manager" => ApplicationRoles.SourcingManager,
            "evaluator" => ApplicationRoles.EvaluationCommittee, "top" => ApplicationRoles.TopCommittee, _ => ApplicationRoles.Supplier };
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Name, actor), new Claim(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
