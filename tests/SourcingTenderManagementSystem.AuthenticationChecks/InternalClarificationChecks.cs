using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Clarifications;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class InternalClarificationChecks
{
    public static void Rules()
    {
        var n = 0; void Check(bool yes) { if (!yes) throw new Exception("Internal clarification policy check " + n); n++; }
        void Deny(Action f) { try { f(); throw new Exception("Expected denial"); } catch (BidException) { n++; } }
        var now = DateTimeOffset.UtcNow; var dept = Guid.NewGuid(); var other = Guid.NewGuid();
        ClarificationPerson P(string id, string role, Guid? d = null) => new(id, id, d ?? other, [role]);
        var owner = P("requester", ApplicationRoles.RequestingDepartment, dept); var officer = P("officer", ApplicationRoles.SourcingOfficer); var manager = P("manager", ApplicationRoles.SourcingManager); var evaluator = P("evaluator", ApplicationRoles.EvaluationCommittee); var top = P("top", ApplicationRoles.TopCommittee);
        var q = new PurchaseRequisition { Id = Guid.NewGuid(), CreatedByUserId = owner.Id, DepartmentId = dept, Status = PurchaseRequestStatus.Submitted };
        var c = new InternalClarificationContext(q.Id, true, "Request", q, null, dept, null, [], []);
        foreach (var status in Enum.GetValues<PurchaseRequestStatus>())
        {
            q.Status = status; var open = status is PurchaseRequestStatus.Submitted or PurchaseRequestStatus.UnderReview or PurchaseRequestStatus.Returned;
            Check(InternalClarificationRules.Allowed(c, owner, officer, now, true) == open); Check(InternalClarificationRules.Allowed(c, officer, owner, now, true) == open); Check(InternalClarificationRules.Allowed(c, owner, officer, now, false));
        }
        q.Status = PurchaseRequestStatus.Submitted;
        Check(!InternalClarificationRules.Allowed(c, owner, officer with { DepartmentId = dept }, now, true));
        Check(!InternalClarificationRules.Allowed(c, owner, officer with { Id = owner.Id }, now, true));
        Check(!InternalClarificationRules.Allowed(c, owner with { Id = "colleague" }, officer, now, true));
        Check(!InternalClarificationRules.Allowed(c with { ReviewerId = "different" }, owner, officer, now, true));
        Check(InternalClarificationRules.Allowed(c with { ReviewerId = officer.Id }, owner, officer, now, true));
        foreach (var role in new[] { ApplicationRoles.Admin, ApplicationRoles.Supplier, ApplicationRoles.SourcingManager }) Check(!InternalClarificationRules.Allowed(c, owner, P("other", role), now, true));
        var t = new Tender { Id = Guid.NewGuid(), CreatedByUserId = officer.Id };
        CommitteeMember M(string user, string position) => new() { Id = Guid.NewGuid(), TenderId = t.Id, UserId = user, Position = position, EffectiveFrom = now.AddDays(-1) };
        var mm = M(manager.Id, CommitteeRules.Positions[0]); var em = M(evaluator.Id, CommitteeRules.Positions[3]); var tm = M(top.Id, "Member 1");
        var tc = new InternalClarificationContext(t.Id, false, "Tender", null, t, dept, null, [mm, em], [tm]);
        foreach (var stage in Enum.GetValues<TenderStatus>())
        {
            t.Status = stage;
            Check(InternalClarificationRules.Allowed(tc, officer, evaluator, now, true) == (stage == TenderStatus.UnderEvaluation));
            Check(InternalClarificationRules.Allowed(tc, evaluator, officer, now, true) == (stage == TenderStatus.UnderEvaluation));
            Check(InternalClarificationRules.Allowed(tc, manager, evaluator, now, true) == (stage is TenderStatus.UnderEvaluation or TenderStatus.AwaitingManagerReview));
            Check(InternalClarificationRules.Allowed(tc, evaluator, manager, now, true) == (stage is TenderStatus.UnderEvaluation or TenderStatus.AwaitingManagerReview));
            Check(InternalClarificationRules.Allowed(tc, manager, top, now, true) == (stage == TenderStatus.AwaitingCommitteeDecision));
            Check(InternalClarificationRules.Allowed(tc, top, manager, now, true) == (stage == TenderStatus.AwaitingCommitteeDecision));
            Check(InternalClarificationRules.Allowed(tc, officer, evaluator, now, false));
            Check(InternalClarificationRules.Allowed(tc, manager, top, now, false));
        }
        t.Status = TenderStatus.UnderEvaluation;
        foreach (var invalid in new[] { evaluator with { Id = "unassigned" }, evaluator with { Roles = [ApplicationRoles.Admin, ApplicationRoles.EvaluationCommittee] }, evaluator with { Roles = [ApplicationRoles.Supplier, ApplicationRoles.EvaluationCommittee] }, evaluator with { Roles = [ApplicationRoles.TopCommittee] } }) Check(!InternalClarificationRules.Allowed(tc, officer, invalid, now, true));
        Check(!InternalClarificationRules.Allowed(tc, officer with { Id = "another-officer" }, evaluator, now, true));
        Check(!InternalClarificationRules.Allowed(tc, manager with { Id = "another-manager" }, evaluator, now, true));
        Check(!InternalClarificationRules.Allowed(tc, evaluator, evaluator, now, true));
        em.EffectiveTo = now; Check(!InternalClarificationRules.Allowed(tc, officer, evaluator, now, false)); em.EffectiveTo = null;
        em.EffectiveFrom = now.AddSeconds(1); Check(!InternalClarificationRules.Allowed(tc, officer, evaluator, now, true)); em.EffectiveFrom = now.AddDays(-1);
        em.TenderId = Guid.NewGuid(); Check(!InternalClarificationRules.Allowed(tc, officer, evaluator, now, true)); em.TenderId = t.Id;
        em.Position = CommitteeRules.Positions[1]; Check(!InternalClarificationRules.Allowed(tc, officer, evaluator, now, true)); Check(InternalClarificationRules.Allowed(tc, officer, evaluator with { DepartmentId = dept }, now, true)); em.Position = CommitteeRules.Positions[3];
        var question = new ClarificationRequest { Id = Guid.NewGuid(), SenderUserId = officer.Id, RecipientUserId = evaluator.Id, Status = ClarificationStatus.Pending };
        InternalClarificationRules.Reply(question, evaluator.Id, false); Check(true); Deny(() => InternalClarificationRules.Reply(question, officer.Id, false)); Deny(() => InternalClarificationRules.Reply(question, evaluator.Id, true)); Deny(() => InternalClarificationRules.Resolve(question, officer.Id, false));
        question.Status = ClarificationStatus.Answered; Deny(() => InternalClarificationRules.Reply(question, evaluator.Id, true)); InternalClarificationRules.Resolve(question, officer.Id, true); Check(true); Deny(() => InternalClarificationRules.Resolve(question, evaluator.Id, true));
        question.Status = ClarificationStatus.Resolved; Deny(() => InternalClarificationRules.Resolve(question, officer.Id, true)); Deny(() => InternalClarificationRules.Reply(question, evaluator.Id, true));
        var version = InternalClarificationRules.Version(tc, [officer, evaluator], [question], []); t.Status = TenderStatus.AwaitingManagerReview; Check(version != InternalClarificationRules.Version(tc, [officer, evaluator], [question], [])); Deny(() => ComplianceRules.Version(version, InternalClarificationRules.Version(tc, [officer, evaluator], [question], [])));
        foreach (var text in new[] { "", "   ", new string('x', 8001) }) Deny(() => ComplianceRules.Text(text, 8000, "Response"));
        Console.WriteLine($"PASS: {n} internal clarification policy, lifecycle and stale-form checks (no database).");
    }
    public static async Task Mvc(HttpClient client)
    {
        int n = 0; void Check(bool x) { if (!x) throw new Exception("Internal clarification MVC check " + n); n++; }
        async Task<HttpResponseMessage> Get(string actor, string url) { client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor); return await client.GetAsync(url); }
        var route = "/Clarifications/Details/" + InternalClarificationFixture.Id;
        foreach (var actor in new[] { "anonymous", "supplier", "mixed", "admin", "requester", "manager", "top" })
        {
            var res = await Get(actor, route); Check(res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        }
        var page = await Get("officer", route); var html = await page.Content.ReadAsStringAsync(); Check(page.StatusCode == HttpStatusCode.OK); Check(html.Contains("name=\"RecipientId\"") && html.Contains("name=\"Description\"")); Check(html.Contains("&lt;script&gt;private question&lt;/script&gt;") && !html.Contains("<script>private question</script>"));
        Check((await client.GetAsync("/Clarifications")).StatusCode == HttpStatusCode.OK);
        foreach (var action in new[] { "Ask", "Reply", "Resolve" }) Check((await client.PostAsync("/Clarifications/" + action + "/" + InternalClarificationFixture.Id, new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value); Check(token.Length > 0);
        var fields = new Dictionary<string, string> { { "__RequestVerificationToken", token }, { "Version", "fixture-version" }, { "RecipientId", "evaluator" }, { "Subject", "Bound question" }, { "Description", "Bound details" } };
        Check((await client.PostAsync("/Clarifications/Ask/" + InternalClarificationFixture.Id, new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.Redirect);
        fields["Description"] = " "; Check((await client.PostAsync("/Clarifications/Ask/" + InternalClarificationFixture.Id, new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.BadRequest);
        Console.WriteLine($"PASS: {n} compiled Razor, binding, role, encoding and antiforgery checks (memory-only presentation fixture).");
    }
}
internal sealed class InternalClarificationFixture : IInternalClarifications
{
    public const string Id = "6fc71a15-682b-435a-a1b8-2e570b7af02d";
    private readonly Guid department = Guid.NewGuid();
    private readonly List<ClarificationRequest> questions = [];
    private readonly List<ClarificationResponse> responses = [];
    public InternalClarificationFixture() { questions.Add(new() { Id = Guid.Parse("cf371a15-682b-435a-a1b8-2e570b7af02d"), TenderId = Guid.Parse(Id), SenderUserId = "officer", RecipientUserId = "evaluator", Subject = "Documented requirement", Description = "<script>private question</script>", Status = ClarificationStatus.Pending, CreatedAt = DateTimeOffset.UtcNow }); }
    private string Actor(ClaimsPrincipal user) { var id = user.FindFirstValue(ClaimTypes.NameIdentifier); if (id is not ("officer" or "evaluator")) throw new BidException(403, "Not a participant."); return id; }
    public Task<bool> AvailableAsync(ClaimsPrincipal user, bool req, Guid id, CancellationToken ct) => Task.FromResult(false);
    public Task<InternalInbox> InboxAsync(ClaimsPrincipal user, int page, CancellationToken ct) { var actor = Actor(user); return Task.FromResult(new InternalInbox(questions.Where(q => q.SenderUserId == actor || q.RecipientUserId == actor).Select(q => new InternalInboxItem(q, "Fixture sourcing process")).ToArray(), 1, false)); }
    public Task<InternalClarificationPage> PageAsync(ClaimsPrincipal user, bool requisition, Guid id, CancellationToken ct) { var actor = Actor(user); if (requisition || id != Guid.Parse(Id)) throw new BidException(404, "Not found"); var c = new InternalClarificationContext(id, false, "Fixture sourcing process", null, new() { Id = id, Status = TenderStatus.UnderEvaluation }, department, null, [], []); return Task.FromResult(new InternalClarificationPage(c, [new(actor == "officer" ? "evaluator" : "officer", "Eligible recipient", department, [])], questions.Select(q => new InternalThread(q, responses.SingleOrDefault(r => r.ClarificationRequestId == q.Id), "Sourcing officer", "Technical representative", "fixture-version", actor == q.RecipientUserId && q.Status == ClarificationStatus.Pending, actor == q.SenderUserId && q.Status == ClarificationStatus.Answered)).ToArray(), "fixture-version")); }
    public Task AskAsync(ClaimsPrincipal user, bool req, Guid id, InternalQuestionForm form, CancellationToken ct) { var actor = Actor(user); ComplianceRules.Version(form.Version, "fixture-version"); ComplianceRules.Text(form.Description, 8000, "Question"); questions.Add(new() { Id = Guid.NewGuid(), TenderId = id, SenderUserId = actor, RecipientUserId = form.RecipientId, Subject = form.Subject, Description = form.Description, Status = ClarificationStatus.Pending, CreatedAt = DateTimeOffset.UtcNow }); return Task.CompletedTask; }
    public Task ReplyAsync(ClaimsPrincipal user, bool req, Guid id, Guid requestId, InternalReplyForm form, CancellationToken ct) { var actor = Actor(user); var q = questions.Single(q => q.Id == requestId); InternalClarificationRules.Reply(q, actor, responses.Any(r => r.ClarificationRequestId == q.Id)); responses.Add(new() { Id = Guid.NewGuid(), ClarificationRequestId = q.Id, ResponderUserId = actor, Response = form.Response, RespondedAt = DateTimeOffset.UtcNow }); q.Status = ClarificationStatus.Answered; return Task.CompletedTask; }
    public Task ResolveAsync(ClaimsPrincipal user, bool req, Guid id, Guid requestId, string version, CancellationToken ct) { var actor = Actor(user); var q = questions.Single(q => q.Id == requestId); InternalClarificationRules.Resolve(q, actor, responses.Any(r => r.ClarificationRequestId == q.Id)); q.Status = ClarificationStatus.Resolved; return Task.CompletedTask; }
}
