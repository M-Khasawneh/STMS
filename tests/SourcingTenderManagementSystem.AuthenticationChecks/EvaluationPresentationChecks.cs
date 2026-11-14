using System.Net;
using System.Security.Claims;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Presentation-only fixture. Real SQL service behavior is exercised by EvaluationSqlChecks.
internal sealed class EvaluationPresentationFixture : IEvaluationManagement
{
    internal const string BidId = "0b83d4af-c784-4da1-a818-a2c59240578d";
    public Task<EvaluationPage> PageAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default)
    {
        var criteria = new[] {
            new EvaluationCriterion { Id = Guid.Parse("53e27d8e-2459-4ae7-8be6-6fcb7e753b61"), Name = "Technical compliance", Description = "Assess the documented requirements.", Kind = EvaluationKind.Technical, Weight = 60m },
            new EvaluationCriterion { Id = Guid.Parse("90fd5cb6-8ec1-46af-9006-1ea44733134b"), Name = "Commercial value", Description = "Assess the offered commercial terms.", Kind = EvaluationKind.Financial, Weight = 40m }
        };
        var form = new EvaluationForm { Version = "presentation-only", Items = criteria.Select(c => new EvaluationLine { CriterionId = c.Id, Score = 80m, Notes = "Saved presentation fixture observations" }).ToList() };
        var results = user.IsInRole(ApplicationRoles.SourcingOfficer) ? criteria.Select(c => new CriterionResult(c, 80m, 80m * c.Weight / 100m)).ToArray() : [];
        return Task.FromResult(new EvaluationPage(new Bid { Id = bidId, Status = BidStatus.Compliant }, new Tender { Title = "Fixture network tender", Status = TenderStatus.UnderEvaluation },
            "Temporary fixture supplier", criteria, form, user.IsInRole(ApplicationRoles.SourcingManager) || user.IsInRole(ApplicationRoles.EvaluationCommittee), false, results.Length > 0 ? 4 : 2, results, []));
    }
    public Task SaveAsync(ClaimsPrincipal user, Guid bidId, EvaluationForm form, bool confirm, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture.");
    public Task StartAsync(ClaimsPrincipal user, Guid tenderId, string version, CancellationToken ct = default) => throw new NotSupportedException("Read-only presentation fixture.");
}
internal static class EvaluationPresentationChecks
{
    public static async Task RunAsync(HttpClient client)
    {
        var count = 0;
        foreach (var actor in new[] { "manager", "evaluator", "officer", "admin", "supplier", "requester", "anonymous" }) {
            client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor);
            var response = await client.GetAsync("/Evaluations/Details/" + EvaluationPresentationFixture.BidId);
            var allowed = actor is "manager" or "evaluator" or "officer";
            if (allowed && response.StatusCode != HttpStatusCode.OK || !allowed && response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)) throw new Exception("Evaluation MVC role/render failure " + actor);
            count++;
            if (actor is "manager" or "evaluator") {
                var html = await response.Content.ReadAsStringAsync();
                if (!html.Contains("Form.Items[0].Score") || !html.Contains("__RequestVerificationToken")) throw new Exception("Evaluation form binding/CSRF rendering");
                if ((await client.GetAsync("/Evaluations/Confirm/" + EvaluationPresentationFixture.BidId)).StatusCode != HttpStatusCode.OK) throw new Exception("Confirmation Razor rendering");
                foreach (var action in new[] { "Save", "Confirm" }) {
                    using var posted = await client.PostAsync($"/Evaluations/{action}/{EvaluationPresentationFixture.BidId}", new FormUrlEncodedContent([]));
                    if (posted.StatusCode != HttpStatusCode.BadRequest) throw new Exception("Evaluation missing CSRF accepted");
                    count++;
                }
                count += 2;
            }
        }
        Console.WriteLine($"PASS: {count} database-free evaluation MVC rendering/role/CSRF checks.");
    }
}
