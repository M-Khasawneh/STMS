using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class ComplianceChecks
{
    private static int checks;
    private static void Check(bool valid, string name)
    {
        if (!valid) throw new InvalidOperationException(name);
        checks++;
    }
    private static void Reject(Action action, int status)
    {
        try { action(); throw new InvalidOperationException("Expected rejection"); }
        catch (BidException e) { Check(e.Status == status, "Expected status " + status); }
    }
    public static async Task RunAsync()
    {
        checks = 0;
        var officer = new BidAccount("officer", [ApplicationRoles.SourcingOfficer], null, false, true, true, [], []);
        var supplier = new BidAccount("supplier", [ApplicationRoles.Supplier], Guid.NewGuid(), true, true, false, [], []);
        var tender = new Tender { Id = Guid.NewGuid(), Status = TenderStatus.Closed };
        var bid = new Bid { Id = Guid.NewGuid(), TenderId = tender.Id, Status = BidStatus.Submitted, SubmittedAt = DateTimeOffset.UtcNow };
        foreach (var role in ApplicationRoles.All.Where(x => x != ApplicationRoles.SourcingOfficer))
            Reject(() => ComplianceRules.Review(officer with { Roles = [role] }, bid, tender), 403);
        foreach (var account in new[] { officer with { Enabled = false }, officer with { EmployeeDepartment = false }, officer with { Roles = [ApplicationRoles.Admin, ApplicationRoles.SourcingOfficer] }, officer with { SupplierId = Guid.NewGuid() } })
            Reject(() => ComplianceRules.Review(account, bid, tender), 403);
        foreach (var status in Enum.GetValues<TenderStatus>().Where(x => x != TenderStatus.Closed)) {
            tender.Status = status;
            Reject(() => ComplianceRules.Review(officer, bid, tender), 409);
        }
        tender.Status = TenderStatus.Closed;
        foreach (var status in new[] { BidStatus.Submitted, BidStatus.UnderReview, BidStatus.Compliant, BidStatus.NonCompliant }) {
            bid.Status = status;
            ComplianceRules.Review(officer, bid, tender);
            Check(true, "Supported compliance state");
        }
        foreach (var status in new[] { BidStatus.Draft, BidStatus.Accepted, BidStatus.Rejected, BidStatus.Withdrawn }) {
            bid.Status = status;
            Reject(() => ComplianceRules.Review(officer, bid, tender), 409);
        }
        bid.Status = BidStatus.Submitted;
        bid.SubmittedAt = null;
        Reject(() => ComplianceRules.Review(officer, bid, tender), 409);
        bid.SubmittedAt = DateTimeOffset.UtcNow;
        foreach (var value in new ComplianceStatus?[] { null, (ComplianceStatus)999 })
            Reject(() => ComplianceRules.Decision(new() { Outcome = value }), 400);
        Reject(() => ComplianceRules.Decision(new() { Outcome = ComplianceStatus.NonCompliant, Reasons = "   " }), 400);
        Reject(() => ComplianceRules.Decision(new() { Outcome = ComplianceStatus.Compliant, Reasons = new string('x', 8001) }), 400);
        ComplianceRules.Decision(new() { Outcome = ComplianceStatus.NonCompliant, Reasons = "Mandatory specification not met." });
        Check(true, "Reason accepted");
        foreach (var count in new[] { 0, 1, 10 }) { ComplianceRules.AttachmentCount(count); Check(true, "Attachment limit accepted"); }
        foreach (var count in new[] { -1, 11 }) Reject(() => ComplianceRules.AttachmentCount(count), 400);
        Reject(() => ComplianceRules.Text(" ", 256, "Subject"), 400);
        Reject(() => ComplianceRules.Text(new string('x', 257), 256, "Subject"), 400);
        var request = new ClarificationRequest { Id = Guid.NewGuid(), RecipientUserId = supplier.UserId, Status = ClarificationStatus.Pending };
        ComplianceRules.Reply(supplier, request, tender);
        Check(true, "Addressed approved supplier can reply");
        Reject(() => ComplianceRules.Reply(supplier with { UserId = "competitor" }, request, tender), 403);
        Reject(() => ComplianceRules.Reply(supplier with { SupplierApproved = false }, request, tender), 403);
        Reject(() => ComplianceRules.Reply(officer, request, tender), 403);
        foreach (var status in new[] { ClarificationStatus.Answered, ClarificationStatus.Resolved }) {
            request.Status = status;
            Reject(() => ComplianceRules.Reply(supplier, request, tender), 409);
        }
        request.Status = ClarificationStatus.Pending;
        var version = ComplianceRules.Version(bid, [], [request]);
        ComplianceRules.Version(version, version);
        Check(true, "Current version accepted");
        Reject(() => ComplianceRules.Version(null, version), 409);
        request.Status = ClarificationStatus.Answered;
        Reject(() => ComplianceRules.Version(version, ComplianceRules.Version(bid, [], [request])), 409);
        Reject(() => ComplianceRules.Version(version, ComplianceRules.Version(bid, [new() { Id = Guid.NewGuid() }], [request])), 409);
        Check(!BidRules.CanRead(supplier with { SupplierId = Guid.NewGuid() }, bid, tender, supplier.SupplierId!.Value), "Competing supplier hidden");
        Check(!BidRules.CanRead(officer with { Roles = [ApplicationRoles.Admin] }, bid, tender, supplier.SupplierId!.Value), "Admin confidential access denied");
        Check(!BidRules.CanRead(officer with { Roles = [ApplicationRoles.EvaluationCommittee] }, bid, tender, supplier.SupplierId!.Value), "Unassigned evaluator denied");
        Check(BidRules.CanRead(officer with { Roles = [ApplicationRoles.EvaluationCommittee], EvaluationTenders = [tender.Id] }, bid, tender, supplier.SupplierId!.Value), "Assigned evaluator sees closed offer");
        await FilesAsync(bid.Id, tender.Id);
        Console.WriteLine($"PASS: {checks} database-free compliance rules/private-file checks.");
    }
    private static async Task FilesAsync(Guid bidId, Guid tenderId)
    {
        var root = Path.Combine(Path.GetTempPath(), "stms-clarification-checks-" + Guid.NewGuid().ToString("N"));
        var files = PrivateBidFiles.ForClarifications(new FilesEnvironment(root));
        try
        {
            var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\nclarification evidence\n%%EOF");
            var upload = await SupplierUploadValidation.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Files", "../evidence.pdf"), default);
            var reference = PrivateBidFiles.NewReference(bidId, bytes);
            var intent = new BidFileIntent(Guid.NewGuid(), bidId, tenderId, reference, null);
            await files.WriteIntentAsync(intent, default);
            await files.SaveAsync(bidId, reference, upload, default);
            var path = Path.Combine(root, "App_Data", "ClarificationDocuments", reference.Replace('/', Path.DirectorySeparatorChar));
            Check(File.Exists(path) && !Directory.Exists(Path.Combine(root, "wwwroot")), "Clarification files private and separate");
            Check((await files.IntentsAsync(bidId, default)).Single() == intent, "Durable recovery intent retained until commit resolution");
            var doc = new BidDocument { BidId = bidId, StorageReference = reference, OriginalName = upload.OriginalName, ContentType = upload.ContentType, Size = bytes.Length };
            await using (var stream = await files.OpenVerifiedAsync(doc, default)) Check(stream.Length == bytes.Length, "Verified download");
            await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes(new string('x', bytes.Length)));
            try { await files.OpenVerifiedAsync(doc, default); throw new Exception("Corrupt file accepted"); }
            catch (InvalidDataException) { Check(true, "Corrupt attachment rejected"); }
            doc.BidId = Guid.NewGuid();
            try { await files.OpenVerifiedAsync(doc, default); throw new Exception("Foreign file accepted"); }
            catch (InvalidDataException) { Check(true, "Cross-bid storage reference rejected"); }
            await files.DeleteAsync(bidId, reference, default);
            await files.CompleteIntentAsync(intent, default);
            Check(!File.Exists(path) && (await files.IntentsAsync(bidId, default)).Count == 0, "Rollback cleanup removes bytes and journal");
            foreach (var name in new[] { "evidence.exe", "evidence.png" }) {
                try { await SupplierUploadValidation.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Files", name), default); throw new Exception("Invalid upload accepted"); }
                catch (InvalidDataException) { Check(true, "Extension/signature mismatch rejected"); }
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class FilesEnvironment(string root) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ComplianceChecks";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    public static async Task HttpAsync(HttpClient client)
    {
        var count = 0;
        async Task<HttpResponseMessage> Get(string actor) {
            client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");
            client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor);
            return await client.GetAsync("/Compliance/Details/" + ComplianceMvcFixture.BidId);
        }
        foreach (var actor in new[] { "officer", "supplier" }) {
            using var response = await Get(actor);
            var html = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK || !html.Contains("Compliance &amp; clarifications") || !html.Contains("__RequestVerificationToken")) throw new Exception("Compliance Razor rendering failed for " + actor);
            if (html.Contains("<script>private-question</script>") || !html.Contains("&lt;script&gt;private-question&lt;/script&gt;")) throw new Exception("Unencoded clarification text");
            if (actor == "officer" && (!html.Contains("/Compliance/Decide/") || !html.Contains("/Compliance/Ask/"))) throw new Exception("Officer form wiring");
            if (actor == "supplier" && (!html.Contains("multipart/form-data") || html.Contains("/Compliance/Decide/"))) throw new Exception("Supplier form isolation");
            count += 4;
        }
        foreach (var actor in new[] { "admin", "requester", "anonymous" }) {
            using var response = await Get(actor);
            if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)) throw new Exception("HTTP role guard failed");
            count++;
        }
        await Get("officer");
        foreach (var action in new[] { "Decide", "Ask", "Reply", "Resolve" }) {
            await Get(action == "Reply" ? "supplier" : "officer");
            using var response = await client.PostAsync($"/Compliance/{action}/{ComplianceMvcFixture.BidId}", new FormUrlEncodedContent([]));
            if (response.StatusCode != HttpStatusCode.BadRequest) throw new Exception("Missing CSRF accepted");
            count++;
        }
        foreach (var action in new[] { "Decide", "Ask", "Reply" }) {
            var page = await (await Get(action == "Reply" ? "supplier" : "officer")).Content.ReadAsStringAsync();
            var token = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            var field = action == "Decide" ? "Reasons" : action == "Ask" ? "Subject" : "Response";
            using var response = await client.PostAsync($"/Compliance/{action}/{ComplianceMvcFixture.BidId}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, [field] = "Keep my review note" }));
            if (response.StatusCode != HttpStatusCode.BadRequest) throw new Exception("Required model binding accepted incomplete form");
            var html = await response.Content.ReadAsStringAsync();
            if (!html.Contains("Keep my review note")) throw new Exception("Validation discarded posted input");
            count += 2;
        }
        foreach (var action in new[] { "Decide", "Ask", "Resolve", "Reply" }) {
            await Get(action == "Reply" ? "officer" : "supplier");
            using var response = await client.PostAsync($"/Compliance/{action}/{ComplianceMvcFixture.BidId}", new FormUrlEncodedContent([]));
            if (response.StatusCode != HttpStatusCode.Forbidden) throw new Exception("Wrong role reached modifying action");
            count++;
        }
        Console.WriteLine($"PASS: {count} compliance MVC rendering/role/antiforgery checks with a presentation-only fixture service.");
    }
}

// This fixture renders real compiled views. It never substitutes for persistence/service tests.
internal sealed class ComplianceMvcFixture : IComplianceManagement
{
    internal static readonly Guid BidId = Guid.Parse("57b4b211-7e94-4bd2-8c91-13e314e95d8f");
    public Task<CompliancePage> PageAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default)
    {
        var supplier = user.IsInRole(ApplicationRoles.Supplier);
        var actor = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var request = new ClarificationRequest { Id = Guid.NewGuid(), BidId = BidId, Subject = "Clarify technical evidence", Description = "<script>private-question</script>", RecipientUserId = actor, SenderUserId = "officer", Status = ClarificationStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        return Task.FromResult(new CompliancePage(bidId, "Network equipment tender", "fixture-version", !supplier, supplier, actor,
            [new ComplianceReview { Outcome = ComplianceStatus.PendingClarification, ReviewedAt = DateTimeOffset.UtcNow, Reasons = "Please clarify the offered specification." }], [new ClarificationThread(request, [])]));
    }
    public Task DecideAsync(ClaimsPrincipal user, Guid bidId, ComplianceDecisionForm form, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture");
    public Task AskAsync(ClaimsPrincipal user, Guid bidId, ClarificationForm form, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture");
    public Task ReplyAsync(ClaimsPrincipal user, Guid bidId, Guid requestId, ClarificationReplyForm form, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture");
    public Task ResolveAsync(ClaimsPrincipal user, Guid bidId, Guid requestId, string version, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture");
    public Task<BidDocumentDownload> DownloadAsync(ClaimsPrincipal user, Guid bidId, Guid attachmentId, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture");
}
