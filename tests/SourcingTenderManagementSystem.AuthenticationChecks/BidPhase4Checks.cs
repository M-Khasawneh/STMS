using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class BidPhase4Checks
{
    internal static async Task RunAsync(HttpClient client, IServiceProvider services, BidPhase1Checks.Fixture f)
    {
        var checks = 0;
        void Check(bool valid, string message) {
            if (!valid) throw new Exception(message);
            checks++;
            Console.WriteLine("PASS: " + message);
        }
        async Task<HttpResponseMessage> Get(string path, string actor) {
            client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");
            client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor);
            return await client.GetAsync(path);
        }
        async Task ExpectFailure(Func<Task> action, int status) {
            try { await action(); throw new Exception("Expected rejection"); }
            catch (BidException e) { Check(e.Status == status, "service rejects unauthorized/stale operation " + status); }
        }
        using var scope = services.CreateScope();
        var bids = scope.ServiceProvider.GetRequiredService<BidManagement>();
        var documents = scope.ServiceProvider.GetRequiredService<BidDocumentManagement>();
        var review = scope.ServiceProvider.GetRequiredService<BidReviewPages>();
        var tender = f.Context.Tender;
        foreach (var pair in new[] { ("officer", ApplicationRoles.SourcingOfficer), ("manager", ApplicationRoles.SourcingManager),
            ("evaluator", ApplicationRoles.EvaluationCommittee), ("top", ApplicationRoles.TopCommittee),
            ("admin", ApplicationRoles.Admin), ("requester", ApplicationRoles.RequestingDepartment) }) {
            f.Store.Accounts[pair.Item1] = new(pair.Item1, [pair.Item2], null, false, true, true, [], []);
        }
        var id = await bids.CreateDraftAsync(f.User, tender.Id, f.Form());
        Check(f.Store.Activity.Count(x => x.Action == "DraftCreated") == 1, "draft creation records an audit event");
        var details = await bids.DetailsAsync(f.User, id);
        var form = f.Form(); form.Version = details.Version;
        await bids.SaveDraftAsync(f.User, id, form);
        Check(f.Store.Activity.Any(x => x.Action == "DraftUpdated"), "draft edit records an audit event");
        byte[] pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nprivate technical proposal\n%%EOF");
        FormFile Upload() => new(new MemoryStream(pdf), 0, pdf.Length, "File", "technical.pdf");
        details = await bids.DetailsAsync(f.User, id);
        var documentId = await documents.UploadAsync(f.User, id, details.Version, "Technical Proposal", Upload());
        Check(f.Store.Activity.Any(x => x.Action == "DocumentUploaded" && x.Reason == $"Document {documentId:D}"), "document upload records the document identifier without names or storage paths");
        details = await bids.DetailsAsync(f.User, id);
        await documents.ReplaceAsync(f.User, id, documentId, details.Version, "Technical Proposal", Upload());
        Check(f.Store.Activity.Any(x => x.Action == "DocumentReplaced"), "document replacement records an audit event");
        details = await bids.DetailsAsync(f.User, id);
        await documents.DeleteAsync(f.User, id, documentId, details.Version);
        Check(f.Store.Activity.Any(x => x.Action == "DocumentRemoved"), "document removal records an audit event");
        details = await bids.DetailsAsync(f.User, id);
        documentId = await documents.UploadAsync(f.User, id, details.Version, "Technical Proposal", Upload());
        details = await bids.DetailsAsync(f.User, id);
        var before = f.Store.Activity.Count;
        f.Store.OnActivity = () => throw new InvalidOperationException("Simulated audit persistence failure");
        try { await bids.SubmitAsync(f.User, id, details.Version); throw new Exception("Expected activity failure"); }
        catch (InvalidOperationException e) when (e.Message == "Simulated audit persistence failure") { }
        f.Store.OnActivity = null;
        Check(f.Store.Rows[id].Bid.Status == BidStatus.Draft && f.Store.Activity.Count == before && f.Store.Receipts.Count == 0,
            "bid, audit and receipt roll back together on activity failure");
        await bids.SubmitAsync(f.User, id, details.Version);
        Check(f.Store.Activity.Count(x => x.Action == "BidSubmitted") == 1 && f.Store.Receipts.Count == 1,
            "submission persists one audit event and one receipt");
        var receipt = f.Store.Receipts.Single();
        Check(receipt.RecipientUserId == f.Account.UserId && receipt.BidId == id && receipt.TenderId == tender.Id &&
            !receipt.Message.Contains("technical.pdf"), "submission receipt contains no proposal details and belongs only to the supplier");
        await ExpectFailure(() => bids.SubmitAsync(f.User, id, details.Version), 409);
        Check(f.Store.Receipts.Count == 1, "duplicate submission cannot duplicate notifications");
        Check((await bids.HistoryAsync(f.User, id)).Count == f.Store.Activity.Count, "owner sees their bid activity");
        Check((await Get($"/Bids/History/{id}", "supplier")).StatusCode == HttpStatusCode.OK, "own activity history renders through supplier MVC");
        Check((await Get($"/Bids/History/{id}", "other")).StatusCode == HttpStatusCode.NotFound, "competing supplier cannot view activity history");
        var competitor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "other")], "test"));
        await ExpectFailure(() => bids.HistoryAsync(competitor, id), 404);

        foreach (var actor in new[] { "admin", "requester", "supplier", "other", "anonymous" }) {
            var status = (await Get("/ReviewBids", actor)).StatusCode;
            Check(status == (actor == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), "internal MVC denies " + actor);
        }
        foreach (var actor in new[] { "officer", "manager", "evaluator", "top" }) {
            Check(!(await (await Get("/ReviewBids", actor)).Content.ReadAsStringAsync()).Contains(tender.Title), "Open tender contents hidden from " + actor);
            Check((await Get($"/ReviewBids/Details/{id}", actor)).StatusCode == HttpStatusCode.NotFound, "Open offer details denied to " + actor);
            Check((await Get($"/ReviewBids/Document/{id}?documentId={documentId}", actor)).StatusCode == HttpStatusCode.NotFound, "Open document denied to " + actor);
        }
        // A second supplier submits a different complete offer through the same services.
        var participation = f.Store.Participation;
        var secondLink = new TenderSupplier { Id = Guid.NewGuid(), TenderId = tender.Id, SupplierProfileId = f.Store.Accounts["other"].SupplierId!.Value };
        f.Store.Participation = new(secondLink, new Invitation { TenderSupplierId = secondLink.Id, Status = InvitationStatus.Accepted });
        var secondForm = f.Form();
        secondForm.Items[0].UnitPrice = 9.876543m;
        var secondId = await bids.CreateDraftAsync(competitor, tender.Id, secondForm);
        var secondVersion = (await bids.DetailsAsync(competitor, secondId)).Version;
        await documents.UploadAsync(competitor, secondId, secondVersion, "Technical Proposal", Upload());
        secondVersion = (await bids.DetailsAsync(competitor, secondId)).Version;
        await bids.SubmitAsync(competitor, secondId, secondVersion);
        f.Store.Participation = participation;
        Check(f.Store.Receipts.Count == 2 && f.Store.Receipts.Select(x => x.RecipientUserId).Distinct().Count() == 2,
            "each supplier receives only its own submission receipt");
        // Passing the deadline alone never closes a tender or releases confidential offers.
        f.Clock.Utc = tender.SubmissionDeadline;
        Check((await Get($"/ReviewBids/Details/{id}", "officer")).StatusCode == HttpStatusCode.NotFound, "deadline does not implicitly grant closed-stage access");
        tender.Status = TenderStatus.Closed;
        var closedList = await (await Get("/ReviewBids", "manager")).Content.ReadAsStringAsync();
        Check(closedList.Contains("Temporary supplier company"), "authorized list identifies the supplier through a scoped lookup");
        foreach (var actor in new[] { "officer", "manager" }) {
            var response = await Get($"/ReviewBids/Details/{id}", actor);
            var html = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == HttpStatusCode.OK && html.Contains("Payment terms") && html.Contains("technical.pdf"), "closed offer details render for " + actor);
            Check(!html.Contains("Edit draft") && !html.Contains("StorageReference"), "internal details contain no supplier editing or storage identifiers");
            var download = await Get($"/ReviewBids/Document/{id}?documentId={documentId}", actor);
            Check(download.StatusCode == HttpStatusCode.OK && (await download.Content.ReadAsByteArrayAsync()).SequenceEqual(pdf) &&
                download.Content.Headers.ContentDisposition?.DispositionType == "attachment" && download.Headers.CacheControl?.NoStore == true,
                "internal download uses verified private attachment and no-store headers");
            var comparison = await Get($"/ReviewBids/Compare?tenderId={tender.Id}", actor);
            var comparisonHtml = await comparison.Content.ReadAsStringAsync();
            Check(comparison.StatusCode == HttpStatusCode.OK && comparisonHtml.Contains(id.ToString()) && comparisonHtml.Contains(secondId.ToString()) &&
                comparisonHtml.Contains("9.876543") && comparisonHtml.Contains("Temporary supplier company") && comparisonHtml.Contains("Quantity 1.25"),
                "comparison renders two submitted offers, server prices and requested quantities for " + actor);
            Check((await Get($"/ReviewBids/History/{id}", actor)).StatusCode == HttpStatusCode.OK, "authorized history renders for " + actor);
        }
        Check((await Get($"/ReviewBids/Details/{id}", "evaluator")).StatusCode == HttpStatusCode.NotFound, "unassigned evaluator cannot read closed offers");
        f.Store.Accounts["evaluator"] = f.Store.Accounts["evaluator"] with { EvaluationTenders = [tender.Id] };
        Check((await Get($"/ReviewBids/Details/{id}", "evaluator")).StatusCode == HttpStatusCode.OK, "assigned evaluator can read closed offers");
        f.Store.Accounts["evaluator"] = f.Store.Accounts["evaluator"] with { EvaluationTenders = [Guid.NewGuid()] };
        Check((await Get($"/ReviewBids/Document/{id}?documentId={documentId}", "evaluator")).StatusCode == HttpStatusCode.NotFound, "foreign or revoked assignment cannot download");
        f.Store.Accounts["top"] = f.Store.Accounts["top"] with { TopTenders = [tender.Id] };
        Check((await Get($"/ReviewBids/Details/{id}", "top")).StatusCode == HttpStatusCode.NotFound, "Top Committee blocked before final-decision stage");
        tender.Status = TenderStatus.AwaitingCommitteeDecision;
        Check((await Get($"/ReviewBids/Details/{id}", "top")).StatusCode == HttpStatusCode.OK, "assigned Top member reads at final-decision stage");
        f.Store.Accounts["top"] = f.Store.Accounts["top"] with { TopTenders = [] };
        Check((await Get($"/ReviewBids/Details/{id}", "top")).StatusCode == HttpStatusCode.NotFound, "unassigned Top member denied");
        f.Store.Accounts["officer"] = f.Store.Accounts["officer"] with { Enabled = false };
        Check((await Get("/ReviewBids", "officer")).StatusCode == HttpStatusCode.Forbidden, "stale cookie cannot bypass disabled account");
        Check((await Get($"/ReviewBids/Compare?tenderId={Guid.NewGuid()}", "manager")).StatusCode == HttpStatusCode.NotFound, "unknown comparison cannot reveal another tender");
        // A draft in an otherwise visible closed tender remains private.
        var original = f.Store.Rows[id];
        f.Store.Rows[id] = original with { Bid = new Bid { Id = id, TenderId = tender.Id, TenderSupplierId = original.Bid.TenderSupplierId, Status = BidStatus.Draft } };
        Check((await Get($"/ReviewBids/Details/{id}", "manager")).StatusCode == HttpStatusCode.NotFound, "draft remains hidden even after tender closure");
        f.Store.Rows[id] = original;
        var concurrent = new BidPhase1Checks.Fixture();
        var concurrentId = await concurrent.Service.CreateDraftAsync(concurrent.User, concurrent.Context.Tender.Id, concurrent.Form());
        concurrent.AddDocument(concurrentId);
        var version = (await concurrent.Service.DetailsAsync(concurrent.User, concurrentId)).Version;
        async Task<bool> SubmitOnce() {
            try { await concurrent.Service.SubmitAsync(concurrent.User, concurrentId, version); return true; }
            catch (BidException e) when (e.Status == 409) { return false; }
        }
        var submissions = await Task.WhenAll(SubmitOnce(), SubmitOnce());
        Check(submissions.Count(x => x) == 1 && concurrent.Store.Receipts.Count == 1 && concurrent.Store.Activity.Count(x => x.Action == "BidSubmitted") == 1,
            "concurrent submissions commit exactly one receipt and submission audit event");
        Console.WriteLine($"PASS: {checks} Phase 4 bid activity/internal MVC checks; memory data and temporary private files only.");
    }

    internal static async Task PrepareBrowserAsync(IServiceProvider services, BidPhase1Checks.Fixture f)
    {
        foreach (var pair in new[] { ("officer", ApplicationRoles.SourcingOfficer), ("manager", ApplicationRoles.SourcingManager),
            ("evaluator", ApplicationRoles.EvaluationCommittee), ("top", ApplicationRoles.TopCommittee) })
            f.Store.Accounts[pair.Item1] = new(pair.Item1, [pair.Item2], null, false, true, true, [f.Context.Tender.Id], [f.Context.Tender.Id]);
        using var scope = services.CreateScope();
        var bids = scope.ServiceProvider.GetRequiredService<BidManagement>();
        var documents = scope.ServiceProvider.GetRequiredService<BidDocumentManagement>();
        var id = await bids.CreateDraftAsync(f.User, f.Context.Tender.Id, f.Form());
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nprivate technical proposal\n%%EOF");
        var file = new FormFile(new MemoryStream(pdf), 0, pdf.Length, "File", "technical.pdf");
        var version = (await bids.DetailsAsync(f.User, id)).Version;
        await documents.UploadAsync(f.User, id, version, "Technical Proposal", file);
        version = (await bids.DetailsAsync(f.User, id)).Version;
        await bids.SubmitAsync(f.User, id, version);
        f.Context.Tender.Status = TenderStatus.Closed;
        Console.WriteLine("PHASE4_BROWSER_BID " + id);
    }
}
