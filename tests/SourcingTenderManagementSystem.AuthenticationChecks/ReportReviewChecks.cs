using System.Text.Json;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class ReportReviewChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool valid) { if (!valid) throw new Exception("Manager review rule check failed"); count++; }
        void Denied(Action action, int status) { try { action(); throw new Exception("Expected rejection"); } catch (BidException e) { Check(e.Status == status); } }
        var now = DateTimeOffset.UtcNow;
        var tender = new Tender { Id = Guid.NewGuid(), Status = TenderStatus.AwaitingManagerReview };
        var report = new EvaluationReport { Id = Guid.NewGuid(), TenderId = tender.Id, PreparedByUserId = "technical", SubmittedAt = now, Comparison = "frozen", Recommendation = "Original" };
        var member = new CommitteeMember { Id = Guid.NewGuid(), TenderId = tender.Id, UserId = "manager", Position = CommitteeRules.Positions[0], EffectiveFrom = now.AddDays(-1) };
        var actor = new BidAccount("manager", [ApplicationRoles.SourcingManager], null, false, true, true, [tender.Id], []);
        Check(ReportReviewRules.Manager(actor, member, tender.Id, now));
        foreach (var other in new[] { actor with { UserId = "another-manager" }, actor with { Roles = [ApplicationRoles.Admin, ApplicationRoles.SourcingManager] },
            actor with { Roles = [ApplicationRoles.Supplier, ApplicationRoles.SourcingManager] }, actor with { Enabled = false }, actor with { EmployeeDepartment = false }, actor with { Roles = [ApplicationRoles.SourcingOfficer] } })
            Check(!ReportReviewRules.Manager(other, member, tender.Id, now));
        Check(!ReportReviewRules.Manager(actor, member, Guid.NewGuid(), now));
        member.EffectiveTo = now; Check(!ReportReviewRules.Manager(actor, member, tender.Id, now)); member.EffectiveTo = null;
        foreach (var status in Enum.GetValues<TenderStatus>()) {
            tender.Status = status;
            var form = new ReportDecisionForm { Accept = true, Decision = DecisionStatus.ManagerApproved };
            if (status == TenderStatus.AwaitingManagerReview) { ReportReviewRules.Validate(tender, report, form); Check(true); }
            else Denied(() => ReportReviewRules.Validate(tender, report, form), 409);
        }
        tender.Status = TenderStatus.AwaitingManagerReview;
        foreach (var decision in Enum.GetValues<DecisionStatus>().Append((DecisionStatus)999)) {
            var form = new ReportDecisionForm { Accept = true, Decision = decision, Comments = "Documented reason" };
            if (decision is DecisionStatus.ManagerApproved or DecisionStatus.ManagerRejected or DecisionStatus.ReturnedForReview) { ReportReviewRules.Validate(tender, report, form); Check(true); }
            else Denied(() => ReportReviewRules.Validate(tender, report, form), 400);
        }
        foreach (var decision in new[] { DecisionStatus.ManagerRejected, DecisionStatus.ReturnedForReview }) {
            Denied(() => ReportReviewRules.Validate(tender, report, new() { Decision = decision, Accept = true, Comments = " " }), 400);
        }
        Denied(() => ReportReviewRules.Validate(tender, report, new() { Decision = DecisionStatus.ManagerApproved }), 400);
        Denied(() => ReportReviewRules.Validate(tender, report, new() { Decision = DecisionStatus.ManagerApproved, Accept = true, Comments = new string('x', 8001) }), 400);
        var before = JsonSerializer.Serialize(report);
        var revision = ReportReviewRules.Revision(report);
        Check(revision.Id != report.Id && revision.TenderId == tender.Id && revision.SubmittedAt is null && revision.Comparison == "frozen");
        revision.Recommendation = "Revised"; Check(JsonSerializer.Serialize(report) == before);
        Check(ReportReviewRules.Current([report], []) == report);
        Denied(() => ReportReviewRules.Current([report, revision], []), 409);
        var rejection = new TenderApproval { Id = Guid.NewGuid(), TenderId = tender.Id, EvaluationReportId = report.Id, Stage = ApprovalStage.Manager, Decision = DecisionStatus.ManagerRejected };
        Check(ReportReviewRules.Current([report, revision], [rejection]) == revision);
        Denied(() => ReportReviewRules.Current([report], [rejection]), 409);
        Denied(() => ReportReviewRules.Current([report, revision], [rejection, rejection]), 409);
        rejection.Decision = DecisionStatus.ManagerApproved;
        Check(ReportReviewRules.Current([report], [rejection]) == report);
        Denied(() => ReportReviewRules.Current([report, revision], [rejection]), 409);
        var version = ReportReviewRules.Version(tender, report, member);
        member.Id = Guid.NewGuid(); Check(version != ReportReviewRules.Version(tender, report, member));
        report.Recommendation = "Changed"; Check(version != ReportReviewRules.Version(tender, report, member));
        Console.WriteLine($"PASS: {count} database-free manager review authorization, lifecycle, immutable revision and conflict checks.");
    }
}
