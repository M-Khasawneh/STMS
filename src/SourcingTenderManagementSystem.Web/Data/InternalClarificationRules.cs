using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Clarifications;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Data;

public static class InternalClarificationRules
{
    public static bool Allowed(InternalClarificationContext c, ClarificationPerson a, ClarificationPerson b, DateTimeOffset now, bool opening)
    {
        if (a.Id == b.Id || !Employee(a) || !Employee(b)) return false;
        bool Role(ClarificationPerson p, string r) => p.Roles.Contains(r);
        if (c.Requisition)
        {
            var q = c.Request!;
            if (opening && q.Status is not (PurchaseRequestStatus.Submitted or PurchaseRequestStatus.UnderReview or PurchaseRequestStatus.Returned)) return false;
            bool Owner(ClarificationPerson p) => p.Id == q.CreatedByUserId && p.DepartmentId == q.DepartmentId && Role(p, ApplicationRoles.RequestingDepartment);
            bool Reviewer(ClarificationPerson p) => Role(p, ApplicationRoles.SourcingOfficer) && p.Id != q.CreatedByUserId && p.DepartmentId != q.DepartmentId && (c.ReviewerId is null || c.ReviewerId == p.Id);
            return Owner(a) && Reviewer(b) || Owner(b) && Reviewer(a);
        }
        var t = c.Tender!;
        bool Member(ClarificationPerson p, IReadOnlyList<CommitteeMember> members, bool evaluation) => members.Any(m => m.TenderId == t.Id && m.UserId == p.Id && CommitteeRules.Effective(m, now) &&
            (evaluation ? m.Position == CommitteeRules.Positions[0] ? Role(p, ApplicationRoles.SourcingManager) : CommitteeRules.Positions.Skip(1).Contains(m.Position) && Role(p, ApplicationRoles.EvaluationCommittee) && (m.Position != CommitteeRules.Positions[1] || p.DepartmentId == c.SourceDepartment) : Role(p, ApplicationRoles.TopCommittee)));
        bool Manager(ClarificationPerson p) => Role(p, ApplicationRoles.SourcingManager) && c.Evaluation.Any(m => m.UserId == p.Id && m.TenderId == t.Id && m.Position == CommitteeRules.Positions[0] && CommitteeRules.Effective(m, now));
        bool Officer(ClarificationPerson p) => p.Id == t.CreatedByUserId && Role(p, ApplicationRoles.SourcingOfficer);
        var officerPair = Officer(a) && Member(b, c.Evaluation, true) || Officer(b) && Member(a, c.Evaluation, true);
        var managerPair = Manager(a) && Member(b, c.Evaluation, true) || Manager(b) && Member(a, c.Evaluation, true);
        var topPair = Manager(a) && Member(b, c.Top, false) || Manager(b) && Member(a, c.Top, false);
        return officerPair && (!opening || t.Status == TenderStatus.UnderEvaluation) ||
            managerPair && (!opening || t.Status is TenderStatus.UnderEvaluation or TenderStatus.AwaitingManagerReview) ||
            topPair && (!opening || t.Status == TenderStatus.AwaitingCommitteeDecision);
    }
    public static bool Employee(ClarificationPerson p) => !p.Roles.Contains(ApplicationRoles.Supplier) && !p.Roles.Contains(ApplicationRoles.Admin);
    public static string Version(InternalClarificationContext c, IEnumerable<ClarificationPerson> people, IEnumerable<ClarificationRequest> questions, IEnumerable<ClarificationResponse> responses) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Context = c, People = people.OrderBy(p => p.Id), Questions = questions.OrderBy(q => q.Id), Responses = responses.OrderBy(r => r.Id) })));
    public static void Reply(ClarificationRequest q, string actor, bool hasResponse) => BidRules.Require(q.RecipientUserId == actor && q.Status == ClarificationStatus.Pending && !hasResponse, 409, "Only the recipient may answer a pending question once.");
    public static void Resolve(ClarificationRequest q, string actor, bool hasResponse) => BidRules.Require(q.SenderUserId == actor && q.Status == ClarificationStatus.Answered && hasResponse, 409, "Only the sender may resolve an answered question.");
}
