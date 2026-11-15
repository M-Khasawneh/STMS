using Microsoft.AspNetCore.Authorization;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class ReportingChecks
{
    public static void Rules(string? output = null)
    {
        SupplierParticipationChecks.Rules();
        AwardReportChecks.Rules();
        int n = 0; void Check(bool yes, string label) { if (!yes) throw new Exception(label); n++; }
        void Deny(Action action) { try { action(); throw new Exception("Expected refusal"); } catch (BidException) { n++; } }
        var a = new BidAccount("employee", [ApplicationRoles.RequestingDepartment], null, false, true, true, [], []);
        Check(ReportRules.Categories(a).SequenceEqual(new[] { "requests", "tenders" }), "Requester report scope");
        foreach (var role in ApplicationRoles.All) { var actor = a with { Roles = [role], SupplierId = role == ApplicationRoles.Supplier ? Guid.NewGuid() : null, SupplierApproved = role == ApplicationRoles.Supplier }; var categories = ReportRules.Categories(actor, true); Check(categories.Length > 0, "Available role reports " + role); if (role == ApplicationRoles.Admin) Check(!categories.Any(c => c is "offers" or "evaluations" or "decisions" or "unified"), "Admin confidentiality"); if (role == ApplicationRoles.Supplier) Check(categories.SequenceEqual(new[] { "offers" }), "Supplier own offers only"); Check(ReportRules.Categories(actor with { Enabled = false }, true).Length == 0, "Disabled account " + role); }
        Check(ReportRules.Categories(a with { Roles = [ApplicationRoles.Admin, ApplicationRoles.SourcingOfficer] }).SequenceEqual(new[] { "requests", "tenders", "suppliers" }), "Admin restrictions win");
        Check(ReportRules.Categories(a with { Roles = [ApplicationRoles.Supplier, ApplicationRoles.SourcingOfficer], SupplierId = Guid.NewGuid(), SupplierApproved = true }).Length == 0, "Mixed supplier denied");
        Check(ReportRules.Categories(a with { EmployeeDepartment = false }).Length == 0, "Department required"); Check(!ReportRules.Categories(a with { Roles = [ApplicationRoles.TopCommittee] }).Contains("overview"), "Top overview requires final-stage assignment");
        Check(ReportRules.Participation(1, 4) == 25m && ReportRules.Participation(0, 4) == 0m && ReportRules.Participation(0, 0) == null, "Participation and zero denominator"); var now = DateTimeOffset.UtcNow;
        Check(ReportRules.ApprovalDays(now, now.AddHours(36)) == 1.5m, "Approval formula"); Check(ReportRules.ApprovalDays(null, now) == null && ReportRules.ApprovalDays(now, null) == null && ReportRules.ApprovalDays(now, now.AddDays(-1)) == null, "Incomplete timing excluded");
        Deny(() => ReportRules.Validate(new() { From = new(2026, 12, 1), To = new(2026, 11, 1) })); Deny(() => ReportRules.Validate(new() { Page = 0 })); Deny(() => ReportRules.Validate(new() { Search = new string('x', 201) })); Deny(() => ReportRules.Limit(10001)); ReportRules.Limit(10000); n++;
        using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=ReportingTranslationOnly;Integrated Security=True").Options))
        {
            var bids = new BidData(db, null!); var reports = new Reporting(db, bids, null!, null!, TimeProvider.System); var sql = reports.RequestQuery(a).ToQueryString(); Check(sql.Contains("CreatedByUserId") && sql.Contains("employee"), "Owner-scoped parameterized query");
            var scoped = a with { Roles = [ApplicationRoles.EvaluationCommittee], EvaluationTenders = [Guid.NewGuid()] }; sql = reports.TenderQuery(scoped).ToQueryString(); Check(sql.Contains(scoped.EvaluationTenders[0].ToString(), StringComparison.OrdinalIgnoreCase) && sql.Contains("Status"), "Assignment/stage query");
            Check(reports.TenderQuery(a with { Roles = [ApplicationRoles.TopCommittee], TopTenders = [Guid.NewGuid()] }).ToQueryString().Contains("AwaitingCommitteeDecision"), "Top stage restriction"); Check(bids.VisibleQuery(a with { Roles = [ApplicationRoles.Supplier], SupplierId = Guid.NewGuid(), SupplierApproved = true }, now).ToQueryString().Contains("SupplierProfileId"), "Supplier ownership query reused");
        }
        var document = FixtureDocument(); var bytes = ReportExports.Excel(document);
        using (var zip = new ZipArchive(new MemoryStream(bytes))) { Check(zip.GetEntry("[Content_Types].xml") != null && zip.GetEntry("xl/workbook.xml") != null, "Genuine XLSX"); foreach (var entry in zip.Entries) { using var stream = entry.Open(); var xml = XDocument.Load(stream); Check(!xml.Descendants().Any(x => x.Name.LocalName == "f"), "No formulas"); } using var sheet = zip.GetEntry("xl/worksheets/sheet2.xml")!.Open(); var data = XDocument.Load(sheet); Check(data.ToString().Contains("=HYPERLINK") && data.ToString().Contains("inlineStr"), "Formula-looking data remains literal"); Check(data.Descendants().Any(x => x.Name.LocalName == "v" && x.Value == "125.123456"), "Exact numeric precision"); }
        Check(ReportCell.Value(0.000000000001m).Text == "0.000000000001", "Positive fractional item totals are not displayed as zero");
        var precise = document with { Tables = [new("Exact values", ["Amount"], [new[] { ReportCell.Value(123456789012.123456m) }])] };
        using (var zip = new ZipArchive(new MemoryStream(ReportExports.Excel(precise)))) { using var entry = zip.GetEntry("xl/worksheets/sheet2.xml")!.Open(); var xml = XDocument.Load(entry); Check(xml.ToString().Contains("123456789012.123456") && xml.ToString().Contains("inlineStr"), "Values beyond Excel numeric precision remain exact literal text"); }
        var rtl = document with { Title = "\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u0634\u062A\u0631\u064A\u0627\u062A" }; Check(ReportExports.HasRightToLeftText(rtl), "RTL detected"); Deny(() => ReportExports.Pdf(rtl)); Check(ReportExports.Excel(rtl).Length > 100, "Arabic preserved in Excel");
        var pdf = ReportExports.Pdf(document); using (var parsed = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import)) Check(parsed.PageCount >= 1 && parsed.Info.Title == document.Title, "Valid PDF");
        var longDoc = document with { Tables = [new("Long narrative", ["Section", "Text"], [new[] { new ReportCell("Observations"), new ReportCell(string.Concat(Enumerable.Repeat("All observations must remain in the exported report. ", 350))) }])] }; var longPdf = ReportExports.Pdf(longDoc); using (var parsed = PdfReader.Open(new MemoryStream(longPdf), PdfDocumentOpenMode.Import)) Check(parsed.PageCount > 1, "Long row continuation");
        if (output != null) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, "reports-fixture.xlsx"), bytes); File.WriteAllBytes(Path.Combine(output, "reports-fixture.pdf"), pdf); File.WriteAllBytes(Path.Combine(output, "reports-long.pdf"), longPdf); }
        Console.WriteLine($"PASS: {n} database-free reporting permission, metric, query translation and real Excel/PDF export checks.");
    }
    internal static ReportDocument FixtureDocument() => new("Purchase requests", DateTimeOffset.UtcNow, [new("Matching requests", "2"), new("Average approval days", "1.5")], [new("Authorized report rows", ["Title", "Status", "Total"], [new[] { new ReportCell("=HYPERLINK(\"https://example.test\")"), new ReportCell("Approved"), ReportCell.Value(125.123456m) }, new[] { new ReportCell("<script>report fixture</script>"), new ReportCell("Submitted"), ReportCell.Value(50m) }])], ["Temporary memory fixture only. No SQL records were queried or changed."]);
    public static async Task Mvc(HttpClient client, IServiceProvider services)
    {
        int n = 0; void Check(bool yes, string label) { if (!yes) throw new Exception(label); n++; }
        var policies=services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();
        var supplier=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"supplier"),new Claim(ClaimTypes.Role,ApplicationRoles.Supplier)],"policy-check"));
        using(var policyScope=services.CreateScope()){
            var authorization=policyScope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
            var defaults=await policies.GetDefaultPolicyAsync();
            Check(!(await authorization.AuthorizeAsync(supplier,null,defaults)).Succeeded,"Fixture uses production employee-only default policy");
            var declared=typeof(SourcingTenderManagementSystem.Web.Controllers.ReportsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),true).Cast<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
            var reportPolicy=(await Microsoft.AspNetCore.Authorization.AuthorizationPolicy.CombineAsync(policies,declared))!;
            Check((await authorization.AuthorizeAsync(supplier,null,reportPolicy)).Succeeded,"Supplier personal reports use explicit signed-in policy, not employee-only default");
        }
        async Task<HttpResponseMessage> Get(string actor, string url) { client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor); return await client.GetAsync(url); }
        Check((await Get("anonymous", "/Reports")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous denied");
        foreach (var actor in new[] { "admin", "requester", "officer", "manager", "evaluator", "top", "supplier" }) { var res = await Get(actor, "/Reports"); var html = await res.Content.ReadAsStringAsync(); Check(res.StatusCode == HttpStatusCode.OK, "Compiled report " + actor); Check(html.Contains("Download Excel") && html.Contains("Download PDF"), "Export routes"); Check(!html.Contains("<script>report fixture</script>") && html.Contains("&lt;script&gt;report fixture&lt;/script&gt;"), "Encoded data"); Check(res.Headers.CacheControl?.NoStore == true, "No report cache"); }
        Check((await Get("supplier", "/Reports?Category=requests")).StatusCode == HttpStatusCode.Forbidden, "Supplier category tampering"); Check((await Get("admin", "/Reports?Category=offers")).StatusCode == HttpStatusCode.Forbidden, "Admin bid denial"); Check((await Get("requester", "/Reports?Category=decisions")).StatusCode == HttpStatusCode.Forbidden, "Requester decision denial");
        foreach (var format in new[] { "xlsx", "pdf" }) { var response = await Get("officer", "/Reports/Download?Category=requests&format=" + format); Check(response.StatusCode == HttpStatusCode.OK, "Actual " + format + " download"); Check(response.Content.Headers.ContentDisposition?.DispositionType == "attachment", "Attachment header"); Check(response.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "No sniffing"); Check((await response.Content.ReadAsByteArrayAsync()).Length > 100, "Real file bytes"); }
        Check((await Get("officer", "/Reports/Download?format=html")).StatusCode == HttpStatusCode.BadRequest, "Unsupported format"); Check((await Get("officer", "/Reports?From=2026-12-01&To=2026-11-01")).StatusCode == HttpStatusCode.BadRequest, "Invalid dates");
        Check((await Get("officer", "/Reports/Print?Category=requests&Search=rtl-fixture")).StatusCode == HttpStatusCode.OK, "Authorized full Arabic print/PDF view");
        Check((await Get("officer", "/Reports/Download?Category=requests&Search=rtl-fixture&format=pdf")).StatusCode == HttpStatusCode.BadRequest, "Direct unsupported Arabic PDF refused");
        Check((await Get("supplier", "/Reports/Print?Category=requests")).StatusCode == HttpStatusCode.Forbidden, "Print view retains category isolation");
        foreach (var role in new[] { "officer", "manager", "evaluator", "top" }) { var page = await Get(role, "/Reports?Category=tenders&Search=participation-fixture"); var text = await page.Content.ReadAsStringAsync(); Check(page.StatusCode == HttpStatusCode.OK && text.Contains("Supplier participation in authorized tenders") && text.Contains("Participation %") && text.Contains("25") && text.Contains("&lt;script&gt;synthetic supplier&lt;/script&gt;"), "Compiled participation table and escaped supplier " + role); }
        foreach (var role in new[] { "admin", "requester", "supplier" }) { var page = await Get(role, "/Reports?Category=tenders&Search=participation-fixture"); Check(!(await page.Content.ReadAsStringAsync()).Contains("Supplier participation in authorized tenders"), "Existing restricted role gets no confidential participation table " + role); }
        foreach (var role in new[] { "officer", "manager", "evaluator", "top" }) { var response = await Get(role, "/Reports?Category=awards"); var text = await response.Content.ReadAsStringAsync(); Check(response.StatusCode == HttpStatusCode.OK && text.Contains("Final awards and recorded contract references") && text.Contains("Tender, supplier or contract reference") && text.Contains("Awarded from") && text.Contains("Not recorded") && !text.Contains("<script>award supplier</script>"), "Award report Razor/filter labels and encoded real-field presentation " + role); }
        foreach (var role in new[] { "admin", "requester", "supplier" }) foreach (var action in new[] { "Index", "Download", "Print" }) Check((await Get(role, "/Reports/" + action + "?Category=awards&format=xlsx")).StatusCode == HttpStatusCode.Forbidden, "Award report/export/print retains role restrictions " + role);
        foreach (var format in new[] { "xlsx", "pdf" }) Check((await Get("officer", "/Reports/Download?Category=awards&format=" + format)).StatusCode == HttpStatusCode.OK, "Real award report " + format + " export");
        var fixture = (ReportingFixture)services.GetRequiredService<IReporting>(); fixture.RevokeDuringExport = true; Check((await Get("officer", "/Reports/Download?Category=requests&format=xlsx")).StatusCode == HttpStatusCode.Conflict, "Revoked export scope rejected"); fixture.RevokeDuringExport = false;
        Console.WriteLine($"PASS: {n} memory-only report MVC role, Razor, encoding and actual download checks.");
    }
}
internal sealed class ReportingFixture : IReporting
{
    public bool RevokeDuringExport; private int exportReads;
    public Task<ReportPage> ReadAsync(ClaimsPrincipal user, ReportFilter f, CancellationToken ct) { ReportRules.Validate(f); var role = user.FindFirstValue(ClaimTypes.Role) ?? ""; var a = new BidAccount(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", [role], role == ApplicationRoles.Supplier ? Guid.NewGuid() : null, true, true, true, [], []); var categories = ReportRules.Categories(a, role == ApplicationRoles.TopCommittee); BidRules.Require(categories.Length > 0, 403, "No reports"); if (string.IsNullOrEmpty(f.Category)) f.Category = categories[0]; BidRules.Require(categories.Contains(f.Category), 403, "Report not authorized"); return Task.FromResult(new ReportPage(f, categories, f.Search == "rtl-fixture" ? ReportingChecks.FixtureDocument() with { Title = "\u062A\u0642\u0631\u064A\u0631 \u0627\u0644\u0645\u0634\u062A\u0631\u064A\u0627\u062A" } : f.Category == "awards" ? AwardReportChecks.Document() : f.Search == "participation-fixture" && a.Internal && a.Roles.Any(r => r is ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee) ? SupplierParticipationChecks.Document() : ReportingChecks.FixtureDocument()) { ScopeKey = RevokeDuringExport ? (++exportReads == 1 ? "before" : "after") : "stable" }); }
}
