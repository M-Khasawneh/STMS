using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed record TopCommitteeTally(bool Complete, DecisionStatus? Outcome);
// Pure approved domain rules; no endpoint, persistence, automatic decision or seeding is enabled here.
public static class TopCommitteeVotingRules
{
    public static readonly DecisionStatus[] Choices = [DecisionStatus.CommitteeApproved, DecisionStatus.CommitteeRejected, DecisionStatus.ReturnedForReview];
    public static bool Eligible(BidAccount account, IReadOnlySet<string> excludedUsers) => account.Internal && account.Roles.Contains(ApplicationRoles.TopCommittee) && !excludedUsers.Contains(account.UserId);
    public static void Selection(IReadOnlyList<string> users, IReadOnlySet<string> eligibleUsers) => BidRules.Require(users.Count == 4 && users.Distinct(StringComparer.Ordinal).Count() == 4 && users.All(eligibleUsers.Contains), 400, "Select four distinct eligible Top Committee employees.");
    public static Guid[] Members(TenderApproval round)
    {
        try
        {
            var members = JsonSerializer.Deserialize<Guid[]>(round.RoundMemberSnapshot ?? "null");
            BidRules.Require(members is { Length: 4 } && members.Distinct().Count() == 4 && members.All(id => id != Guid.Empty), 409, "The fixed voting-round membership snapshot is invalid.");
            return members;
        }
        catch (JsonException) { throw new BidException(409, "The fixed voting-round membership snapshot is invalid."); }
    }
    public static void Reason(string? reason) => ComplianceRules.Text(reason, 8000, "A documented justification");
    public static void Vote(BidAccount actor, CommitteeAssignment assignment, CommitteeMember member, Tender tender, TenderApproval round,
        IReadOnlyCollection<Guid> fixedMembers, IReadOnlyList<CommitteeVote> votes, IReadOnlySet<string> excludedUsers, DecisionStatus choice, string? reason, DateTimeOffset now)
    {
        BidRules.Require(Eligible(actor, excludedUsers) && assignment.Kind == CommitteeKind.TopCommittee && assignment.TenderId == tender.Id &&
            member.TenderId == tender.Id && member.CommitteeAssignmentId == assignment.Id && member.UserId == actor.UserId && CommitteeRules.Effective(member, now), 403, "Only an eligible appointed member may vote.");
        BidRules.Require(tender.Status == TenderStatus.AwaitingCommitteeDecision && round.TenderId == tender.Id && round.Stage == ApprovalStage.TopCommittee &&
            round.CommitteeAssignmentId == assignment.Id && round.Decision == DecisionStatus.RecommendationPending && round.RoundOpenedAt.HasValue && round.RoundOpenedAt <= now &&
            round.RoundClosedAt is null && round.DecidedAt is null, 409, "This voting round is not open.");
        BidRules.Require(fixedMembers.Count == 4 && fixedMembers.Distinct().Count() == 4 && fixedMembers.Contains(member.Id), 409, "The round requires its four fixed membership records.");
        BidRules.Require(votes.All(v => v.TenderId == tender.Id && v.TenderApprovalId == round.Id) && !votes.Any(v => v.CommitteeMemberId == member.Id), 409, "A confirmed vote cannot be changed or repeated.");
        BidRules.Require(Choices.Contains(choice), 400, "Choose approval, rejection or a request for further details."); Reason(reason);
        _ = Tally(round, tender.Id, fixedMembers, votes);
    }
    public static TopCommitteeTally Tally(TenderApproval round, Guid tenderId, IReadOnlyCollection<Guid> fixedMembers, IReadOnlyList<CommitteeVote> votes)
    {
        BidRules.Require(round.TenderId == tenderId && round.Stage == ApprovalStage.TopCommittee && Members(round).Order().SequenceEqual(fixedMembers.Order()) && fixedMembers.Count == 4 && fixedMembers.Distinct().Count() == 4 && votes.Count <= 4 && votes.Select(v => v.CommitteeMemberId).Distinct().Count() == votes.Count &&
            votes.All(v => v.TenderId == tenderId && v.TenderApprovalId == round.Id && fixedMembers.Contains(v.CommitteeMemberId) && round.RoundOpenedAt.HasValue && v.VotedAt >= round.RoundOpenedAt && Choices.Any(c => c.ToString() == v.Decision) && !string.IsNullOrWhiteSpace(v.Justification) && v.Justification.Length <= 8000), 409, "Voting records conflict with the fixed round membership or allowed choices.");
        if (votes.Count < 4) return new(false, null);
        var majority = Choices.Where(c => votes.Count(v => v.Decision == c.ToString()) >= 3).ToArray();
        return new(true, majority.Length == 1 ? majority[0] : null);
    }
    public static IReadOnlyList<CommitteeVote> VisibleVotes(TenderApproval round, Guid tenderId, Guid? ownMembership, IReadOnlyList<CommitteeVote> votes) =>
        Tally(round, tenderId, Members(round), votes).Complete ? votes.ToArray() : votes.Where(v => v.CommitteeMemberId == ownMembership).ToArray();
    public static TenderStatus AfterDecision(DecisionStatus? outcome) => outcome switch
    {
        null or DecisionStatus.CommitteeApproved => TenderStatus.AwaitingCommitteeDecision,
        DecisionStatus.CommitteeRejected or DecisionStatus.ReturnedForReview => TenderStatus.UnderEvaluation,
        _ => throw new BidException(409, "Invalid collective outcome.")
    };
    public static void Award(BidAccount actor, Tender tender, TenderApproval decision, EvaluationReport report, Bid recommended, bool awardExists, IReadOnlyList<CommitteeVote> votes)
    {
        BidRules.Require(actor.Internal && actor.Roles.Contains(ApplicationRoles.SourcingOfficer) && actor.UserId == tender.CreatedByUserId, 403, "Only the responsible Sourcing Officer may record the award.");
        var tally = Tally(decision, tender.Id, Members(decision), votes);
        BidRules.Require(tally.Complete && tally.Outcome == DecisionStatus.CommitteeApproved && !awardExists && tender.Status == TenderStatus.AwaitingCommitteeDecision && decision.TenderId == tender.Id && decision.Stage == ApprovalStage.TopCommittee &&
            decision.Decision == DecisionStatus.CommitteeApproved && decision.DecidedAt.HasValue && decision.RoundClosedAt.HasValue && decision.EvaluationReportId == report.Id &&
            report.TenderId == tender.Id && report.SubmittedAt.HasValue && report.RecommendedBidId == recommended.Id && recommended.TenderId == tender.Id && recommended.SubmittedAt.HasValue && recommended.Status == BidStatus.Compliant,
            409, "A current approved recommendation and an unawarded tender are required.");
    }
}
