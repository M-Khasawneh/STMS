using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class CommitteeSqlChecks
{
    public static async Task VerifyAsync(IServiceProvider provider, string[] users, Guid tenderId, Guid bidId, Action<bool> failAudit)
    {
        var checks = 0;
        void Check(bool valid, string message) {
            if (!valid) throw new Exception(message);
            checks++; Console.WriteLine("PASS: " + message);
        }
        ClaimsPrincipal Actor(int i) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, users[i])], "SqlTest"));
        async Task<CommitteePage> Page() {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<CommitteeManagement>().PageAsync(Actor(0), tenderId);
        }
        async Task Save(CommitteeForm form, int actor = 0) {
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CommitteeManagement>().SaveAsync(Actor(actor), tenderId, form);
        }
        async Task Reject(Func<Task> action, int status) {
            try { await action(); throw new Exception("Expected rejection"); }
            catch (BidException e) { Check(e.Status == status, "Committee service rejects " + status); }
        }
        var page = await Page();
        var form = page.Form;
        form.ManagerUserId = users[0]; form.DepartmentRepresentativeId = users[3]; form.FinancialRepresentativeId = users[4]; form.TechnicalRepresentativeId = users[5];
        await Reject(() => Save(form, 1), 403);
        form.TechnicalRepresentativeId = users[4];
        await Reject(() => Save(form), 400);
        form.TechnicalRepresentativeId = users[5];
        var originalVersion = form.Version;
        await Save(form);
        page = await Page();
        Check(page.History.Count == 4 && page.Changes.Count == 4, "Four distinct members and appointment audit records persist");
        await Reject(() => Save(form), 409);
        form = page.Form;
        form.TechnicalRepresentativeId = users[6];
        await Save(form);
        page = await Page();
        Check(page.History.Count == 5 && page.History.Single(x => x.Member.UserId == users[5]).Member.EffectiveTo.HasValue, "Pre-evaluation replacement retains expired membership");
        using (var scope = provider.CreateScope()) {
            var data = scope.ServiceProvider.GetRequiredService<IBidData>();
            var now = DateTimeOffset.UtcNow;
            var previous = await data.AccountAsync(Actor(5), now, default);
            var current = await data.AccountAsync(Actor(6), now, default);
            Check(await data.ReadAsync(bidId, previous!, now, default) is null, "Replaced evaluator immediately loses bid access");
            Check(await data.ReadAsync(bidId, current!, now, default) is not null, "New evaluator gains only assigned tender access");
            Check(await data.ReadAsync(Guid.NewGuid(), current!, now, default) is null, "Unknown bid remains isolated");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var criterion = new EvaluationCriterion { Id = Guid.NewGuid(), TenderId = tenderId, Name = "Temporary verification criterion", Kind = EvaluationKind.Technical, Weight = 100m };
            db.EvaluationCriteria.Add(criterion);
            db.TechnicalEvaluations.Add(new TechnicalEvaluation { Id = Guid.NewGuid(), TenderId = tenderId, BidId = bidId, EvaluationCriterionId = criterion.Id,
                RecordedByUserId = users[5], EvaluatorUserId = users[5], Score = 1m, Notes = "Preserve this test evaluation", EvaluatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        page = await Page();
        Check(page.EvaluationStarted, "First persisted evaluation locks unreasoned replacement");
        form = page.Form;
        form.TechnicalRepresentativeId = users[5];
        await Reject(() => Save(form), 400);
        form.Reason = "Documented replacement after evaluation started";
        failAudit(true);
        try { await Save(form); throw new Exception("Expected injected audit failure"); }
        catch (Exception e) when (e.GetType().Name == "InjectedAuditFailure") { Check(true, "Injected appointment audit failure aborts save"); }
        finally { failAudit(false); }
        page = await Page();
        Check(page.Form.TechnicalRepresentativeId == users[6] && page.History.Count == 5, "Failed replacement rolls back both membership changes");
        var version = page.Form.Version;
        form.Version = version;
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
            try { await Save(form); return 200; }
            catch (BidException e) { return e.Status; }
        }));
        Check(attempts.Count(x => x == 200) == 1 && attempts.Count(x => x == 409) == 1, "Concurrent replacements commit once with independent SQL transactions");
        page = await Page();
        Check(page.History.Count == 6 && page.Changes.Any(x => x.Reason!.Contains(form.Reason)), "Post-evaluation replacement keeps reason and membership history");
        using (var scope = provider.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check(await db.TechnicalEvaluations.CountAsync(x => x.TenderId == tenderId && x.Notes == "Preserve this test evaluation") == 1, "Prior evaluator's scores remain intact");
            Check(await db.CommitteeAssignments.CountAsync(x => x.TenderId == tenderId) == 1, "Concurrent updates never duplicate the committee");
        }
        var candidate = page.Candidates.Single(x => x.Id == users[3]);
        var changed = page.Form;
        changed.Reason = "Check source department validation";
        var wrongDepartment = page.Candidates.Select(x => x.Id == candidate.Id ? x with { DepartmentId = Guid.NewGuid() } : x).ToArray();
        try { CommitteeRules.Validate(changed, wrongDepartment, page.SourceDepartmentId, true); throw new Exception("Wrong department accepted"); }
        catch (BidException e) { Check(e.Status == 400, "Source department rule rejects unrelated representative"); }
        Check(originalVersion != page.Form.Version, "Membership changes invalidate original forms");
        Console.WriteLine($"PASS: {checks} SQL committee checks; cleanup follows in the shared isolated fixture.");
    }
}
