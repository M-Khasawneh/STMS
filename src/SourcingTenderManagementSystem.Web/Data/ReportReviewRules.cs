using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public static class ReportReviewRules
{
    public static bool Manager(BidAccount actor, CommitteeMember member, Guid tenderId, DateTimeOffset now) =>
        actor.Internal && actor.Roles.Contains(ApplicationRoles.SourcingManager) && member.UserId == actor.UserId &&
        member.TenderId == tenderId && member.Position == CommitteeRules.Positions[0] && CommitteeRules.Effective(member, now);

    // Previous versions remain immutable rows. Every earlier version must have one documented return/rejection.
    public static EvaluationReport? Current(IReadOnlyList<EvaluationReport> reports, IReadOnlyList<TenderApproval> approvals, IReadOnlyList<TenderApproval>? committeeRounds = null)
    {
        var decisions = approvals.Where(x => x.Stage == ApprovalStage.Manager).ToArray();
        BidRules.Require(decisions.All(x => reports.Any(r => r.Id == x.EvaluationReportId && r.TenderId == x.TenderId && r.SubmittedAt.HasValue)) &&
            decisions.GroupBy(x => x.EvaluationReportId).All(x => x.Count() == 1), 409, "Report decision records conflict. Resolve the data conflict first.");
        var active = reports.Where(r => !decisions.Any(d => d.EvaluationReportId == r.Id)).ToArray();
        BidRules.Require(active.Length <= 1, 409, "Multiple unresolved report records exist. Resolve the data conflict first.");
        var terminal = (committeeRounds ?? []).Where(x => x.Stage == ApprovalStage.TopCommittee && x.Decision is DecisionStatus.CommitteeApproved or DecisionStatus.CommitteeRejected or DecisionStatus.ReturnedForReview).ToArray();
        BidRules.Require(terminal.All(x => x.DecidedAt.HasValue && x.RoundClosedAt.HasValue && decisions.Any(m => m.EvaluationReportId == x.EvaluationReportId && m.TenderId == x.TenderId && m.Decision == DecisionStatus.ManagerApproved)) && terminal.GroupBy(x => x.EvaluationReportId).All(g => g.Count() == 1), 409, "Collective report decisions conflict with manager approvals.");
        var returned = terminal.Where(x => x.Decision is DecisionStatus.CommitteeRejected or DecisionStatus.ReturnedForReview).Select(x => x.EvaluationReportId).ToHashSet();
        var accepted = decisions.Where(x => x.Decision == DecisionStatus.ManagerApproved && !returned.Contains(x.EvaluationReportId)).ToArray();
        BidRules.Require(accepted.Length <= 1 && (accepted.Length == 0 || active.Length == 0 && reports.Count == decisions.Length), 409, "Approved report and revision records conflict.");
        BidRules.Require(decisions.All(x => x.Decision is DecisionStatus.ManagerApproved or DecisionStatus.ManagerRejected or DecisionStatus.ReturnedForReview), 409, "Invalid manager decision record.");
        return active.SingleOrDefault() ?? (accepted.Length == 1 ? reports.Single(x => x.Id == accepted[0].EvaluationReportId) : reports.Count == 0 ? null : throw new BidException(409, "A returned report is missing its revision."));
    }
    public static string Version(Tender tender, EvaluationReport report, CommitteeMember manager) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { tender.Status, Report = report, Manager = manager })));
    public static void Validate(Tender tender, EvaluationReport report, ReportDecisionForm form)
    {
        BidRules.Require(tender.Status == TenderStatus.AwaitingManagerReview && report.SubmittedAt.HasValue && report.TenderId == tender.Id,
            409, "Only the current submitted report awaiting manager review may be decided.");
        BidRules.Require(form.Decision is DecisionStatus.ManagerApproved or DecisionStatus.ManagerRejected or DecisionStatus.ReturnedForReview, 400, "Choose a manager decision.");
        BidRules.Require((form.Comments?.Length ?? 0) <= 8000 &&
            (form.Decision == DecisionStatus.ManagerApproved || !string.IsNullOrWhiteSpace(form.Comments)), 400, "Rejection and return require a reason of at most 8000 characters.");
        BidRules.Require(form.Accept, 400, "Explicitly confirm your decision.");
    }
    public static EvaluationReport Revision(EvaluationReport original) => new() {
        Id = Guid.NewGuid(), TenderId = original.TenderId, PreparedByUserId = original.PreparedByUserId,
        RecommendedBidId = original.RecommendedBidId, TechnicalSummary = original.TechnicalSummary, FinancialSummary = original.FinancialSummary,
        Observations = original.Observations, NonCompliance = original.NonCompliance, Comparison = original.Comparison,
        FinalResult = original.FinalResult, Recommendation = original.Recommendation
    };
}
