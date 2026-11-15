using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class SupplierExpiryChecks
{
    public sealed class MemoryStore : ISupplierExpiryStore
    {
        public SupplierProfile Profile = new() { Id = Guid.Parse("82fb4cba-43a7-4b05-b66b-1a08ce99badd"), CompanyName = "Synthetic <script>supplier</script>", UserId = "supplier", Status = SupplierStatus.Pending };
        public Dictionary<Guid, SupplierDocument> Documents = [];
        public List<AuditLog> Audit = [];
        public bool FailSave; public Action? AfterSave; public int Begins;
        private readonly SemaphoreSlim gate = new(1, 1);
        public MemoryStore()
        {
            var today = new DateOnly(2026, 11, 13);
            foreach (var (type, date) in new[] { ("Company Registration Certificate", (DateOnly?)today.AddDays(-1)), ("Business / Trade License", today.AddDays(30)), ("Tax Registration Certificate", (DateOnly?)null) })
            { var id = Guid.NewGuid(); Documents[id] = new SupplierDocument { Id = id, SupplierProfileId = Profile.Id, DocumentType = type, OriginalName = "synthetic.pdf", StorageReference = "private-unchanged", ExpiresOn = date }; }
        }
        public async Task<IBidTransaction> BeginAsync(Guid supplierId, CancellationToken ct) { await gate.WaitAsync(ct); Begins++; if (supplierId != Profile.Id) { gate.Release(); throw new BidException(404, "Not found"); } return new Transaction(this); }
        public Task<SupplierDocument?> DocumentAsync(Guid supplierId, Guid documentId, CancellationToken ct) => Task.FromResult(Documents.TryGetValue(documentId, out var d) && d.SupplierProfileId == supplierId ? d : null);
        public Task SaveAsync(SupplierDocument document, AuditLog audit, CancellationToken ct) { Documents[document.Id] = document; Audit.Add(audit); AfterSave?.Invoke(); if (FailSave) throw new IOException("Synthetic persistence failure"); return Task.CompletedTask; }
        private sealed class Transaction(MemoryStore store) : IBidTransaction
        {
            private readonly Dictionary<Guid, DateOnly?> dates = store.Documents.ToDictionary(x => x.Key, x => x.Value.ExpiresOn);
            private readonly int auditCount = store.Audit.Count; private bool committed;
            public Task CommitAsync(CancellationToken ct) { committed = true; return Task.CompletedTask; }
            public ValueTask DisposeAsync() { if (!committed) { foreach (var (id, date) in dates) store.Documents[id].ExpiresOn = date; store.Audit.RemoveRange(auditCount, store.Audit.Count - auditCount); } store.gate.Release(); return ValueTask.CompletedTask; }
        }
    }
    private static ClaimsPrincipal Actor(string id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "memory"));
    internal static async Task Rules()
    {
        int n = 0; void Check(bool valid, string label) { if (!valid) throw new Exception(label); n++; }
        async Task Deny(Func<Task> action, int status) { try { await action(); throw new Exception("Expected rejection"); } catch (BidException e) { Check(e.Status == status, "Rejection status"); } }
        var f = new BidPhase1Checks.Fixture(); var store = new MemoryStore(); var service = new SupplierDocumentExpiry(store, f.Store, f.Clock);
        var admin = new BidAccount("admin", [ApplicationRoles.Admin], null, false, true, true, [], []); f.Store.Accounts["admin"] = admin;
        var doc = store.Documents.Values.First(); var before = doc.ExpiresOn;
        SupplierExpiryForm Form(DateOnly? date) => new() { DocumentId = doc.Id, Version = SupplierExpiryRules.Version(doc), ExpiresOn = date };
        var today = SupplierExpiryRules.Today(f.Clock.Utc);
        Check(SupplierExpiryRules.Today(new(2026, 11, 13, 21, 0, 0, TimeSpan.Zero)) == new DateOnly(2026, 11, 14), "Amman midnight boundary");
        foreach (var (date, label) in new[] { ((DateOnly?)null, "Expiry not recorded"), (today.AddDays(-1), "Expired"), (today, "Expires today"), (today.AddDays(1), "Expires within 30 days"), (today.AddDays(30), "Expires within 30 days"), (today.AddDays(31), "Expiry date recorded") }) Check(SupplierExpiryRules.Label(date, today) == label, "Approved inclusive 30-day boundary");
        foreach (var role in ApplicationRoles.All.Where(r => r != ApplicationRoles.Admin))
        { f.Store.Accounts["blocked"] = admin with { UserId = "blocked", Roles = [role] }; await Deny(() => service.SaveAsync(Actor("blocked"), store.Profile.Id, Form(today), default), 403); }
        Check(store.Begins == 0, "Non-admin rejected before persistence access");
        foreach (var blocked in new[] { admin with { Enabled = false }, admin with { EmployeeDepartment = false }, admin with { Roles = [ApplicationRoles.Admin, ApplicationRoles.Supplier] }, admin with { SupplierId = Guid.NewGuid() } })
        { f.Store.Accounts["admin"] = blocked; await Deny(() => service.SaveAsync(Actor("admin"), store.Profile.Id, Form(today), default), 403); }
        f.Store.Accounts["admin"] = admin;
        var unknown = Form(today); unknown.DocumentId = Guid.NewGuid(); await Deny(() => service.SaveAsync(Actor("admin"), store.Profile.Id, unknown, default), 404);
        var foreign = new SupplierDocument { Id = Guid.NewGuid(), SupplierProfileId = Guid.NewGuid() }; store.Documents[foreign.Id] = foreign;
        await Deny(() => service.SaveAsync(Actor("admin"), store.Profile.Id, new SupplierExpiryForm { DocumentId = foreign.Id, Version = SupplierExpiryRules.Version(foreign), ExpiresOn = today }, default), 404);
        store.Documents.Remove(foreign.Id);
        await Deny(() => service.SaveAsync(Actor("admin"), Guid.NewGuid(), Form(today), default), 404);
        var bad = Form(today); bad.Version = ""; await Deny(() => service.SaveAsync(Actor("admin"), store.Profile.Id, bad, default), 400);
        Check(!Validator.TryValidateObject(bad, new ValidationContext(bad), [], true), "Required concurrency token validation");
        var stale = Form(today);
        await service.SaveAsync(Actor("admin"), store.Profile.Id, Form(today.AddDays(30)), default);
        Check(doc.ExpiresOn == today.AddDays(30) && store.Audit.Count == 1 && store.Audit[0].ActorUserId == "admin" && store.Audit[0].OccurredAt.Offset == TimeSpan.Zero, "Date and actor/UTC audit saved atomically");
        Check(store.Audit[0].Reason!.Contains(before!.Value.ToString("yyyy-MM-dd")) && !store.Audit[0].Reason!.Contains("private-unchanged"), "Audit includes old/new date, excludes storage data");
        Check(store.Profile.Status == SupplierStatus.Pending && doc.StorageReference == "private-unchanged", "No account status or original-file modification");
        await Deny(() => service.SaveAsync(Actor("admin"), store.Profile.Id, stale, default), 409);
        var noOpCount = store.Audit.Count; await service.SaveAsync(Actor("admin"), store.Profile.Id, Form(doc.ExpiresOn), default); Check(store.Audit.Count == noOpCount, "No-op adds no audit event");
        await service.SaveAsync(Actor("admin"), store.Profile.Id, Form(null), default); Check(doc.ExpiresOn is null, "Explicit blank means unknown");
        store.FailSave = true; var count = store.Audit.Count;
        try { await service.SaveAsync(Actor("admin"), store.Profile.Id, Form(today), default); throw new Exception("Expected failure"); } catch (IOException) { Check(doc.ExpiresOn is null && store.Audit.Count == count, "Persistence failure rolls date and audit back"); }
        store.FailSave = false;
        store.AfterSave = () => f.Store.Accounts["admin"] = admin with { Enabled = false };
        await Deny(() => service.SaveAsync(Actor("admin"), store.Profile.Id, Form(today), default), 403); Check(doc.ExpiresOn is null && store.Audit.Count == count, "Revocation before commit rolls back"); store.AfterSave = null; f.Store.Accounts["admin"] = admin;
        var first = Form(today); var second = Form(today.AddDays(1));
        async Task<int> Save(SupplierExpiryForm form) { try { await service.SaveAsync(Actor("admin"), store.Profile.Id, form, default); return 200; } catch (BidException e) { return e.Status; } }
        var results = await Task.WhenAll(Save(first), Save(second)); Check(results.Count(x => x == 200) == 1 && results.Count(x => x == 409) == 1, "Concurrent edits cannot overwrite a newer expiry date");
        store.Profile.Status = SupplierStatus.Approved;
        await service.SaveAsync(Actor("admin"), store.Profile.Id, Form(today.AddDays(-1)), default);
        Check(store.Profile.Status == SupplierStatus.Approved && doc.ExpiresOn < today, "Recording an expired document does not suspend an approved supplier");
        Console.WriteLine($"PASS: {n} database-free supplier expiry authorization, dates, audit, rollback and concurrency checks.");
    }
    internal static async Task Mvc(HttpClient client, IServiceProvider services)
    {
        int n = 0; void Check(bool valid, string label) { if (!valid) throw new Exception(label); n++; }
        var store = services.GetRequiredService<MemoryStore>();
        async Task<HttpResponseMessage> Get(string actor) { using var request = new HttpRequestMessage(HttpMethod.Get, "/NotesPreview/SupplierExpiry"); request.Headers.Add("X-Bid-Test-Actor", actor); return await client.SendAsync(request); }
        var response = await Get("admin"); var html = await response.Content.ReadAsStringAsync();
        Check(response.StatusCode == HttpStatusCode.OK && Regex.Matches(html, "Save expiry date").Count == 3 && html.Contains("Expired") && html.Contains("Expires within 30 days") && html.Contains("Expiry not recorded"), "Compiled Admin dates/forms/statuses");
        Check(!html.Contains("<script>supplier</script>") && html.Contains("&lt;script&gt;"), "Supplier data remains Razor encoded");
        var supplier = await (await Get("supplier")).Content.ReadAsStringAsync(); Check(supplier.Contains("Expires within 30 days") && !supplier.Contains("Save expiry date") && !supplier.Contains("name=\"Version\""), "Supplier read-only date visibility");
        var doc = store.Documents.Values.First(); var url = $"/Suppliers/DocumentExpiry/{store.Profile.Id}";
        async Task<HttpResponseMessage> Post(string actor, bool csrf)
        {
            var fields = new Dictionary<string, string> { ["DocumentId"] = doc.Id.ToString(), ["Version"] = SupplierExpiryRules.Version(doc), ["ExpiresOn"] = "2027-01-31" };
            if (csrf) { var page = await (await Get(actor)).Content.ReadAsStringAsync(); fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value); }
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) }; request.Headers.Add("X-Bid-Test-Actor", actor); return await client.SendAsync(request);
        }
        foreach (var actor in new[] { "anonymous", "supplier", "officer", "manager", "requester", "evaluator", "top" }) Check((await Post(actor, false)).StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Real POST role gate " + actor);
        Check((await Post("admin", false)).StatusCode == HttpStatusCode.BadRequest && store.Audit.Count == 0, "Antiforgery blocks untrusted date updates");
        var saved = await Post("admin", true); Check(saved.StatusCode == HttpStatusCode.Redirect && saved.Headers.Location!.ToString().Contains("/Suppliers/Details/") && doc.ExpiresOn == new DateOnly(2027, 1, 31) && store.Audit.Count == 1, "Real MVC form binding, service save and original redirect route");
        Console.WriteLine($"PASS: {n} supplier expiry compiled Razor/MVC role, read-only visibility, encoding, antiforgery and memory-persistence checks. No SQL connection opened.");
    }
}
