using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Real compiled views with a presentation-only report boundary. Persistence uses the separate SQL checks.
internal sealed class UnifiedReportPresentationFixture : IUnifiedReportManagement
{
    public UnifiedReportForm? LastSaved { get; private set; }
    public bool LastSubmitted { get; private set; }
    internal const string TenderId = "8bff4fe2-e45e-455d-a8ca-8f283c07f2bc";
    internal const string SubmittedId = "92dcc730-ebdd-48db-a5cc-5e1c9ee191ac";
    public Task<UnifiedReportPage> PageAsync(ClaimsPrincipal user, Guid tenderId, CancellationToken ct = default)
    {
        var submitted = tenderId == Guid.Parse(SubmittedId);
        var members = CommitteeRules.Positions.Select((p, i) => new ReportMember(Guid.Parse($"00000000-0000-0000-0000-{(i + 1).ToString("D12")}"), "fixture-" + i, "Fixture member " + i, p)).ToArray();
        var criteria = new[] {
            new ReportCriterion(Guid.Parse("00000000-0000-0000-0001-000000000001"), "Technical compliance", EvaluationKind.Technical, 60m),
            new ReportCriterion(Guid.Parse("00000000-0000-0000-0001-000000000002"), "Commercial value", EvaluationKind.Financial, 40m)
        };
        var bidId = Guid.Parse("00000000-0000-0000-0002-000000000001");
        var offer = new ReportOffer(bidId, "Fixture approved supplier", BidStatus.Compliant, 4, 137.5m, "Payment in 30 days", "Delivery in 14 days", new DateOnly(2026, 12, 31),
            [new ReportLine("Network equipment", "Documented specification", 2.5m, 50m, 125m)], [new ReportCharge("Delivery", 12.5m)],
            [new ReportPoints(criteria[0].Id, 80m, 48m), new ReportPoints(criteria[1].Id, 90m, 36m)],
            [new ReportNote(members[3].Id, members[3].Name, criteria[0].Id, 80m, "<script>fixture-note</script>", "Documented observation", DateTimeOffset.UtcNow)], ["Compliant: Requirements checked"]);
        var excluded = offer with { BidId = Guid.Parse("00000000-0000-0000-0002-000000000002"), Supplier = "Fixture excluded supplier", Status = BidStatus.NonCompliant, ConfirmedMembers = 0, Results = [], Notes = [], ComplianceReasons = ["Non-compliant: Mandatory requirement not met"] };
        var snapshot = new ReportSnapshot(1, tenderId, "Fixture tender", members, criteria, [offer, excluded]);
        var report = new EvaluationReport { Id = Guid.NewGuid(), TenderId = tenderId, PreparedByUserId = "fixture-3", RecommendedBidId = bidId,
            Observations = "Fixture combined observations", FinalResult = "The committee assessment is complete.", Recommendation = "Recommend the documented compliant offer for the stated technical and commercial reasons.",
            SubmittedAt = submitted ? DateTimeOffset.UtcNow : null, Comparison = JsonSerializer.Serialize(snapshot) };
        var form = new UnifiedReportForm { Version = "presentation-version", RecommendedBidId = bidId, Observations = report.Observations, FinalResult = report.FinalResult, Recommendation = report.Recommendation };
        return Task.FromResult(new UnifiedReportPage(new Tender { Id = tenderId, Title = snapshot.TenderTitle, Status = submitted ? TenderStatus.AwaitingManagerReview : TenderStatus.UnderEvaluation },
            report, snapshot, form, !submitted && user.IsInRole(ApplicationRoles.EvaluationCommittee), true, []));
    }
    public Task SaveAsync(ClaimsPrincipal user, Guid tenderId, UnifiedReportForm form, bool submit, CancellationToken ct = default)
    {
        LastSaved = form; LastSubmitted = submit;
        return Task.CompletedTask;
    }
}
internal static class UnifiedReportPresentationChecks
{
    public static async Task RunAsync(HttpClient client, IServiceProvider services)
    {
        var checks = 0;
        foreach (var actor in new[] { "officer", "manager", "evaluator", "admin", "supplier", "requester", "anonymous" }) {
            client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor);
            var response = await client.GetAsync("/UnifiedReports/Details/" + UnifiedReportPresentationFixture.TenderId);
            var allowed = actor is "officer" or "manager" or "evaluator";
            if (allowed && response.StatusCode != HttpStatusCode.OK || !allowed && response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)) throw new Exception("Report MVC role/render guard " + actor);
            checks++;
            if (allowed) {
                var html = await response.Content.ReadAsStringAsync();
                if (!html.Contains("Fixture excluded supplier") || !html.Contains("&lt;script&gt;fixture-note&lt;/script&gt;") || html.Contains("<script>fixture-note</script>")) throw new Exception("Report comparison/encoding");
                checks++;
            }
            if (actor == "evaluator") {
                var html = await response.Content.ReadAsStringAsync();
                if (!html.Contains("Form.Recommendation") || !html.Contains("__RequestVerificationToken")) throw new Exception("Report form field binding");
                if ((await client.GetAsync("/UnifiedReports/Submit/" + UnifiedReportPresentationFixture.TenderId)).StatusCode != HttpStatusCode.OK) throw new Exception("Report submission preview");
                foreach (var action in new[] { "Save", "Submit" }) {
                    if ((await client.PostAsync($"/UnifiedReports/{action}/{UnifiedReportPresentationFixture.TenderId}", new FormUrlEncodedContent([]))).StatusCode != HttpStatusCode.BadRequest) throw new Exception("Report missing CSRF accepted");
                    checks++;
                }
                checks += 2;
                var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
                var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Form.Version"] = "presentation-version",
                    ["Form.RecommendedBidId"] = "00000000-0000-0000-0002-000000000001", ["Form.Observations"] = "Bound committee observation", ["Form.NonCompliance"] = "" };
                using var saved = await client.PostAsync("/UnifiedReports/Save/" + UnifiedReportPresentationFixture.TenderId, new FormUrlEncodedContent(fields));
                var fixture = (UnifiedReportPresentationFixture)services.GetRequiredService<IUnifiedReportManagement>();
                if (saved.StatusCode != HttpStatusCode.Redirect || fixture.LastSaved?.Observations != "Bound committee observation" || fixture.LastSaved.NonCompliance is not null) throw new Exception("Report MVC prefixed form/optional-empty binding");
                checks++;
                fields["Form.Observations"] = new string('x', 12001);
                if ((await client.PostAsync("/UnifiedReports/Save/" + UnifiedReportPresentationFixture.TenderId, new FormUrlEncodedContent(fields))).StatusCode != HttpStatusCode.BadRequest) throw new Exception("Report MVC text validation");
                checks++;
                var confirmed = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["version"] = "presentation-version", ["accept"] = "true" };
                if ((await client.PostAsync("/UnifiedReports/Submit/" + UnifiedReportPresentationFixture.TenderId, new FormUrlEncodedContent(confirmed))).StatusCode != HttpStatusCode.Redirect || !fixture.LastSubmitted) throw new Exception("Report explicit submission action wiring");
                checks++;
            }
        }
        client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", "manager");
        var submitted = await client.GetStringAsync("/UnifiedReports/Details/" + UnifiedReportPresentationFixture.SubmittedId);
        if (submitted.Contains("name=\"Form.Recommendation\"")) throw new Exception("Submitted report shows editable controls");
        checks++;
        Console.WriteLine($"PASS: {checks} database-free report MVC rendering/role/CSRF checks.");
    }
}
