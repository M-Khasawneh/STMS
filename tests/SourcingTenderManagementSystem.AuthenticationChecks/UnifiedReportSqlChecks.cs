using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class UnifiedReportSqlChecks
{
    public static async Task VerifyAsync(IServiceProvider provider, string[] users, Guid tenderId, Guid bidId, Action<bool> failAudit)
    {
        var count = 0;
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); count++; Console.WriteLine("PASS: " + label); }
        ClaimsPrincipal Actor(int i) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, users[i])], "SqlTest"));
        async Task<T> With<TService, T>(Func<TService, Task<T>> action) where TService : notnull {
            using var scope = provider.CreateScope(); return await action(scope.ServiceProvider.GetRequiredService<TService>());
        }
        Task<UnifiedReportPage> Page(int i) => With<IUnifiedReportManagement, UnifiedReportPage>(s => s.PageAsync(Actor(i), tenderId));
        async Task Save(int i, UnifiedReportForm form, bool submit = false) =>
            _ = await With<IUnifiedReportManagement, bool>(async s => { await s.SaveAsync(Actor(i), tenderId, form, submit); return true; });
        async Task Reject(Func<Task> action, int status) {
            try { await action(); throw new Exception("Expected report rejection"); }
            catch (BidException e) { Check(e.Status == status, "Report guard rejects " + status); }
        }
        await With<ApplicationDbContext, bool>(async db => {
            var source = await db.TenderPurchaseRequisitions.Where(x => x.TenderId == tenderId).Select(x => x.PurchaseRequisitionId).SingleAsync();
            var item = new PurchaseRequisitionItem { Id = Guid.NewGuid(), PurchaseRequisitionId = source, Description = "Fixture precision item", Specifications = "Fixture authoritative specification", Quantity = 2.5m };
            db.PurchaseRequisitionItems.Add(item);
            db.BidItems.Add(new BidItem { Id = Guid.NewGuid(), BidId = bidId, PurchaseRequisitionItemId = item.Id, Description = item.Description,
                Specifications = item.Specifications, OfferedQuantity = item.Quantity, UnitPrice = 12.345678m });
            db.BidCostComponents.Add(new BidCostComponent { Id = Guid.NewGuid(), BidId = bidId, CostType = "Fixture fee", Currency = "JOD", Amount = 7.125m });
            await db.SaveChangesAsync(); return true;
        });
        await Reject(() => Page(1), 403);
        await Reject(() => Page(2), 403);
        await Reject(() => Page(5), 404);
        var initial = await Page(6);
        Check(initial.CanEdit && initial.Ready && initial.Report is null, "Current technical representative can prepare the ready report");
        Check(initial.Snapshot!.Offers.Single().Total == 37.989195m && initial.Snapshot.Offers.Single().Items.Single().Quantity == 2.5m,
            "Report comparison uses persisted exact-decimal prices, fees and quantities");
        Check(initial.Snapshot.Offers.Single().Notes.Count == 8 && initial.Snapshot.Offers.Single().Results.Sum(x => x.WeightedPoints) == 80.5m,
            "Report includes all eight current confirmed assessments and the computed weighted result");
        Check(initial.Snapshot.Members.All(x => x.UserId != users[5]), "Replaced membership is excluded from the current report snapshot");
        var form = initial.Form;
        form.RecommendedBidId = bidId; form.Observations = "<script>fixture-observation</script>";
        form.FinalResult = "Fixture final result based on the saved committee assessments.";
        form.Recommendation = "Fixture substantiated recommendation with documented technical and commercial reasons.";
        await Reject(() => Save(0, form), 403);
        await Reject(() => Save(3, form), 403);
        await Reject(() => Save(7, form), 403);
        form.RecommendedBidId = Guid.NewGuid();
        await Reject(() => Save(6, form), 400);
        form.RecommendedBidId = bidId;
        await Save(6, form);
        await Reject(() => Save(6, form), 409);
        var draft = await Page(6);
        Check(draft.Report is { SubmittedAt: null } && draft.Report.PreparedByUserId == users[6], "Draft persists with technical author and no submission timestamp");
        Check(!(await Page(0)).CanEdit && (await Page(7)).Ready, "Manager and officer can read while authoring stays restricted");
        Check(!draft.Report!.Comparison.Contains("StorageReference") && !draft.Report.Comparison.Contains("Password"), "Snapshot omits private storage references and account secrets");
        var incomplete = draft.Form;
        incomplete.Recommendation = " ";
        await Reject(() => Save(6, incomplete, true), 400);
        await With<ApplicationDbContext, bool>(async db => {
            await db.EvaluationReports.Where(x => x.TenderId == tenderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Comparison, "Legacy draft comparison snapshot"));
            return true;
        });
        var outdated = (await Page(6)).Form;
        await Reject(() => Save(6, outdated, true), 409);
        await Save(6, outdated);
        var ready = (await Page(6)).Form;
        failAudit(true);
        try { await Save(6, ready, true); throw new Exception("Expected injected report audit failure"); }
        catch (Exception e) when (e.GetType().Name == "InjectedAuditFailure") { Check(true, "Injected report audit failure aborts submission"); }
        finally { failAudit(false); }
        var rollback = await Page(6);
        Check(rollback.Report!.SubmittedAt is null && rollback.Tender.Status == TenderStatus.UnderEvaluation,
            "Submission failure rolls back report timestamp and tender transition");
        await With<ApplicationDbContext, bool>(async db => {
            Check(!await db.Notifications.AnyAsync(x => x.TenderId == tenderId && x.Subject == "Unified report submitted"), "Failed report submission creates no manager notification");
            return true;
        });
        var concurrent = rollback.Form;
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
            try { await Save(6, concurrent, true); return 200; } catch (BidException e) { return e.Status; }
        }));
        Check(outcomes.Count(x => x == 200) == 1 && outcomes.Count(x => x == 409) == 1, "Concurrent report submissions commit exactly once");
        var submitted = await Page(0);
        Check(submitted.Report!.SubmittedAt.HasValue && submitted.Tender.Status == TenderStatus.AwaitingManagerReview,
            "Submission atomically fixes the report and advances to Awaiting Manager Review");
        var snapshotText = submitted.Report.Comparison;
        var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(snapshotText)!;
        Check(snapshot.Offers.Single().Total == 37.989195m && submitted.Report.RecommendedBidId == bidId, "Stored submission snapshot preserves commercial inputs and selected recommendation");
        await Reject(() => Save(6, concurrent), 409);
        await With<ApplicationDbContext, bool>(async db => {
            Check(await db.EvaluationReports.CountAsync(x => x.TenderId == tenderId) == 1, "Duplicate requests never create a second report");
            Check(await db.Notifications.CountAsync(x => x.TenderId == tenderId && x.Subject == "Unified report submitted") == 1, "Manager receives one report notification");
            Check(await db.AuditLogs.CountAsync(x => x.RecordType == "EvaluationReport" && x.RecordId == tenderId.ToString() && x.Action == "ReportSubmitted") == 1,
                "Exactly one submission decision is audited");
            return true;
        });
        await With<CommitteeManagement, bool>(async s => {
            var page = await s.PageAsync(Actor(0), tenderId);
            page.Form.TechnicalRepresentativeId = users[5]; page.Form.Reason = "Fixture post-submission replacement";
            await s.SaveAsync(Actor(0), tenderId, page.Form); return true;
        });
        var frozen = await Page(0);
        Check(frozen.Report!.Comparison == snapshotText && frozen.Snapshot!.Members.Any(x => x.UserId == users[6]),
            "Submitted report remains frozen after later committee membership changes");
        Check((await Page(5)).Snapshot!.Offers.Single().Results.Sum(x => x.WeightedPoints) == 80.5m,
            "New member reads the original submitted result without recalculating it");
        await Reject(() => Save(5, frozen.Form), 409);
        await Reject(() => Page(6), 404);
        async Task Decide(int actor, ReportDecisionForm decision) =>
            _ = await With<IUnifiedReportManagement, bool>(async service => { await service.DecideAsync(Actor(actor), tenderId, decision); return true; });
        Check(frozen.CanReview && !(await Page(7)).CanReview, "Only the appointed manager sees decision controls");
        await With<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>, bool>(async usersService => {
            var unassigned = await usersService.FindByIdAsync(users[7]);
            var result = await usersService.AddToRoleAsync(unassigned!, "SourcingManager");
            Check(result.Succeeded, "Fixture creates an unassigned manager role for server-side authorization testing"); return true;
        });
        Check(!(await Page(7)).CanReview, "Unassigned Sourcing Manager remains read-only");
        var decisionForm = frozen.DecisionForm;
        decisionForm.Accept = true; decisionForm.Decision = DecisionStatus.ReturnedForReview;
        await Reject(() => Decide(0, decisionForm), 400);
        decisionForm.Comments = "Fixture correction requested";
        await Reject(() => Decide(7, decisionForm), 403);
        await Reject(() => Decide(1, decisionForm), 403);
        var stale = new ReportDecisionForm { Version = "stale", Decision = DecisionStatus.ManagerApproved, Accept = true };
        await Reject(() => Decide(0, stale), 409);
        failAudit(true);
        try { await Decide(0, decisionForm); throw new Exception("Expected injected manager audit failure"); }
        catch (Exception e) when (e.GetType().Name == "InjectedAuditFailure") { Check(true, "Manager decision audit failure aborts transaction"); }
        finally { failAudit(false); }
        Check((await Page(0)).CanReview, "Failed decision leaves the submitted report awaiting review");
        await With<ApplicationDbContext, bool>(async db => {
            Check(!await db.TenderApprovals.AnyAsync(x => x.TenderId == tenderId) && await db.EvaluationReports.CountAsync(x => x.TenderId == tenderId) == 1,
                "Failure rolls back decision, revision and status writes"); return true;
        });
        await Decide(0, decisionForm);
        await Reject(() => Decide(0, decisionForm), 409);
        var returned = await Page(5);
        Check(returned.CanEdit && returned.Report!.Id != frozen.Report.Id && returned.Report.SubmittedAt is null && returned.Tender.Status == TenderStatus.UnderEvaluation,
            "Return creates a separate editable draft for the currently appointed technical representative");
        Check(returned.Report!.Comparison == snapshotText && returned.Snapshot!.Offers.Single().Results.Sum(x => x.WeightedPoints) == 80.5m,
            "Revised draft preserves original evaluation results after membership changes");
        var archived = await With<IUnifiedReportManagement, UnifiedReportPage>(s => s.PageAsync(Actor(0), tenderId, reportId: frozen.Report.Id));
        Check(archived.Archived && !archived.CanEdit && !archived.CanReview && archived.Report!.Comparison == snapshotText,
            "Previous submitted version remains readable and immutable");
        returned.Form.Recommendation = "Fixture corrected recommendation";
        await Save(5, returned.Form);
        await Save(5, (await Page(5)).Form, true);
        var corrected = await Page(0);
        var rejectForm = corrected.DecisionForm;
        rejectForm.Accept = true; rejectForm.Decision = DecisionStatus.ManagerRejected; rejectForm.Comments = "Fixture alternative recommendation required";
        await Decide(0, rejectForm);
        var alternative = await Page(5);
        Check(alternative.CanEdit && alternative.Decisions.Count == 2 && alternative.Versions.Count == 2,
            "Rejection preserves the rejected version and opens an alternative recommendation without cancelling the tender");
        await Save(5, alternative.Form);
        await Save(5, (await Page(5)).Form, true);
        var finalReview = await Page(0);
        var approval = finalReview.DecisionForm;
        approval.Accept = true; approval.Decision = DecisionStatus.ManagerApproved;
        var decisions = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
            try { await Decide(0, approval); return 200; } catch (BidException e) { return e.Status; }
        }));
        Check(decisions.Count(x => x == 200) == 1 && decisions.Count(x => x == 409) == 1, "Concurrent manager approvals commit exactly once");
        var approved = await Page(0);
        Check(approved.Tender.Status == TenderStatus.AwaitingCommitteeDecision && !approved.CanReview && !approved.CanEdit,
            "Approval forwards to Top Committee without awarding or allowing further manager decisions");
        await With<ApplicationDbContext, bool>(async db => {
            Check(await db.TenderApprovals.CountAsync(x => x.TenderId == tenderId) == 3 && await db.EvaluationReports.CountAsync(x => x.TenderId == tenderId) == 3,
                "All three report versions and distinct decisions persist");
            Check(await db.TechnicalEvaluations.Where(x => x.TenderId == tenderId && x.SubmittedAt != null).CountAsync() == 5,
                "Manager actions preserve confirmed technical assessments");
            Check(await db.Notifications.AnyAsync(x => x.TenderId == tenderId && x.RecipientUserId == users[5] && x.Subject == "Manager report decision"),
                "Technical representative receives the real decision notification");
            Check(await db.AuditLogs.CountAsync(x => x.RecordType == "Tender" && x.RecordId == tenderId.ToString() && x.Action == "Tender.ManagerDecision") == 3,
                "Each committed manager decision has a tender audit record");
            Check(!await db.TenderAwards.AnyAsync(x => x.TenderId == tenderId), "Manager approval never creates an award");
            return true;
        });
        Console.WriteLine($"PASS: {count} SQL unified-report checks. Shared fixture cleanup follows.");
    }
}
