using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class EvaluationSqlChecks
{
    public static async Task VerifyAsync(IServiceProvider provider, string[] users, Guid tenderId, Guid bidId, Action<bool> failAudit, bool reportChecks = false)
    {
        var count = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS: " + message); }
        ClaimsPrincipal Actor(int i) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, users[i])], "SqlTest"));
        async Task<T> With<TService, T>(Func<TService, Task<T>> action) where TService : notnull {
            using var scope = provider.CreateScope(); return await action(scope.ServiceProvider.GetRequiredService<TService>());
        }
        async Task Do<TService>(Func<TService, Task> action) where TService : notnull =>
            _ = await With<TService, bool>(async s => { await action(s); return true; });
        async Task Reject(Func<Task> action, int status) {
            try { await action(); throw new Exception("Expected evaluation rejection"); }
            catch (BidException e) { Check(e.Status == status, "Evaluation guard rejects " + status); }
        }
        Task<EvaluationPage> Page(int i) => With<EvaluationManagement, EvaluationPage>(s => s.PageAsync(Actor(i), bidId));
        Task Save(int i, EvaluationForm form, bool confirm = false) => Do<EvaluationManagement>(s => s.SaveAsync(Actor(i), bidId, form, confirm));
        async Task Start(int i) {
            var detail = await With<TenderManagement, SourcingTenderManagementSystem.Web.Models.Tenders.TenderDetails>(async s => await s.DetailsAsync(tenderId, await s.ActorAsync(Actor(7))));
            await Do<EvaluationManagement>(s => s.StartAsync(Actor(i), tenderId, detail.Version));
        }
        await Reject(() => Page(1), 403);
        await Reject(() => Start(0), 403);
        await Reject(() => Start(7), 409);
        await Do<CommitteeManagement>(async s => {
            var page = await s.PageAsync(Actor(0), tenderId);
            await s.SaveAsync(Actor(0), tenderId, new CommitteeForm { Version = page.Form.Version,
                ManagerUserId = users[0], DepartmentRepresentativeId = users[3], FinancialRepresentativeId = users[4], TechnicalRepresentativeId = users[5] });
        });
        await With<ApplicationDbContext, bool>(async db => {
            db.EvaluationCriteria.AddRange(
                new EvaluationCriterion { Id = Guid.NewGuid(), TenderId = tenderId, Name = "Fixture technical", Kind = EvaluationKind.Technical, Weight = 60m, ApprovedByUserId = users[0], ApprovedAt = DateTimeOffset.UtcNow },
                new EvaluationCriterion { Id = Guid.NewGuid(), TenderId = tenderId, Name = "Fixture financial", Kind = EvaluationKind.Financial, Weight = 40m, ApprovedByUserId = users[0], ApprovedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); return true;
        });
        await Reject(() => Start(7), 409);
        await Do<IComplianceManagement>(async s => {
            var page = await s.PageAsync(Actor(7), bidId);
            await s.AskAsync(Actor(7), bidId, new ClarificationForm { Version = page.Version, Subject = "Fixture outstanding clarification", Description = "Clarify the offered specification." });
        });
        await Do<IComplianceManagement>(async s => {
            var page = await s.PageAsync(Actor(7), bidId);
            await s.DecideAsync(Actor(7), bidId, new ComplianceDecisionForm { Version = page.Version, Outcome = ComplianceStatus.Compliant });
        });
        await Reject(() => Start(7), 409);
        await Do<IComplianceManagement>(async s => {
            var supplierPage = await s.PageAsync(Actor(1), bidId);
            var question = supplierPage.Threads.Single().Request.Id;
            await s.ReplyAsync(Actor(1), bidId, question, new ClarificationReplyForm { Version = supplierPage.Version, Response = "Fixture response" });
            var officerPage = await s.PageAsync(Actor(7), bidId);
            await s.ResolveAsync(Actor(7), bidId, question, officerPage.Version);
        });
        await Start(7);
        await Reject(() => Start(7), 409);
        var first = await Page(0);
        Check(first.CanEdit && first.Tender.Status == TenderStatus.UnderEvaluation, "Explicit officer action starts evaluation for assigned manager");
        await Reject(() => Save(7, first.Form), 403);
        var technicalId = first.Criteria.Single(x => x.Kind == EvaluationKind.Technical).Id;
        var financialId = first.Criteria.Single(x => x.Kind == EvaluationKind.Financial).Id;
        first.Form.Items.Single(x => x.CriterionId == technicalId).Score = 80m;
        await Save(0, first.Form);
        await Reject(() => Save(0, first.Form), 409);
        var partial = await Page(0);
        Check(partial.Form.Items.Single(x => x.CriterionId == technicalId).Score == 80m && partial.Form.Items.Single(x => x.CriterionId == financialId).Score == null,
            "Partial draft persists without inventing missing scores");
        await Reject(() => Save(0, partial.Form, true), 400);
        foreach (var invalid in new[] { 101m, -0.1m, 100.000001m, 0.0000001m }) {
            partial.Form.Items[0].Score = invalid;
            await Reject(() => Save(0, partial.Form), 400);
        }
        partial = await Page(0);
        partial.Form.Items[0].CriterionId = Guid.NewGuid();
        await Reject(() => Save(0, partial.Form), 400);
        async Task<EvaluationForm> Form(int actor, decimal technical, decimal financial) {
            var page = await Page(actor);
            foreach (var line in page.Form.Items) { line.Score = line.CriterionId == technicalId ? technical : financial; line.Notes = "Fixture committee assessment"; }
            return page.Form;
        }
        var own = await Form(0, 80m, 70m);
        failAudit(true);
        try { await Save(0, own, true); throw new Exception("Expected audit save failure"); }
        catch (Exception e) when (e.GetType().Name == "InjectedAuditFailure") { Check(true, "Injected confirmation audit failure aborts transaction"); }
        finally { failAudit(false); }
        var rolledBack = await Page(0);
        Check(!rolledBack.Confirmed && rolledBack.Form.Items.Single(x => x.CriterionId == financialId).Score == null,
            "Failed confirmation rolls back new scores and confirmation timestamps");
        await Save(0, await Form(0, 80m, 70m), true);
        await Reject(() => Save(0, own, true), 409);
        await Save(4, await Form(4, 70m, 90m), true);
        await Save(5, await Form(5, 100m, 60m), true);
        Check((await Page(0)).Results.Count == 0, "Combined score withheld until all four current members confirm");
        var concurrent = await Form(3, 90m, 80m);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
            try { await Save(3, concurrent, true); return 200; } catch (BidException e) { return e.Status; }
        }));
        Check(results.Count(x => x == 200) == 1 && results.Count(x => x == 409) == 1, "Concurrent confirmation succeeds exactly once");
        var complete = await Page(0);
        Check(complete.ConfirmedMembers == 4 && complete.Results.Sum(x => x.WeightedPoints) == 81m, "Four-member average and 60/40 weighting produce exactly 81 points");
        Check(!complete.CanEdit && complete.Confirmed, "Confirmed assessment becomes read-only");
        await Do<CommitteeManagement>(async s => {
            var page = await s.PageAsync(Actor(0), tenderId); page.Form.TechnicalRepresentativeId = users[6]; page.Form.Reason = "Fixture evaluator replacement";
            await s.SaveAsync(Actor(0), tenderId, page.Form);
        });
        await Reject(() => Page(5), 404);
        Check((await Page(0)).Results.Count == 0 && (await Page(0)).ConfirmedMembers == 3, "Replacement invalidates completeness until the new member confirms");
        await Save(6, await Form(6, 90m, 70m), true);
        var replaced = await Page(0);
        Check(replaced.Results.Sum(x => x.WeightedPoints) == 80.5m, "New membership contributes its own scores while old scores stay excluded");
        await With<ApplicationDbContext, bool>(async db => {
            Check(await db.TechnicalEvaluations.CountAsync(x => x.BidId == bidId && x.EvaluatorUserId == users[5] && x.SubmittedAt != null) == 1,
                "Former evaluator's confirmed score remains in SQL history");
            Check(await db.FinancialEvaluations.CountAsync(x => x.BidId == bidId && x.EvaluatorUserId == users[5] && x.SubmittedAt != null) == 1,
                "Former financial assessment remains in SQL history");
            Check(await db.Notifications.CountAsync(x => x.TenderId == tenderId && x.Subject == "Evaluation started") == 4, "Start action persists four committee notifications once");
            return true;
        });
        Console.WriteLine($"PASS: {count} SQL evaluation checks. Shared fixture cleanup removes all temporary rows.");
        if (reportChecks) await UnifiedReportSqlChecks.VerifyAsync(provider, users, tenderId, bidId, failAudit);
    }
}
