using System.Text.Json;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class TopCommitteeRuleChecks
{
    public static void Run()
    {
        int n = 0; void Check(bool yes) { if (!yes) throw new Exception("Top Committee check " + n); n++; }
        void Deny(Action a) { try { a(); throw new Exception("Expected refusal"); } catch (BidException) { n++; } }
        var now = DateTimeOffset.UtcNow; var tender = new Tender { Id = Guid.NewGuid(), CreatedByUserId = "officer", Status = TenderStatus.AwaitingCommitteeDecision }; var assignment = new CommitteeAssignment { Id = Guid.NewGuid(), TenderId = tender.Id, Kind = CommitteeKind.TopCommittee };
        var members = Enumerable.Range(0, 4).Select(i => new CommitteeMember { Id = Guid.NewGuid(), TenderId = tender.Id, CommitteeAssignmentId = assignment.Id, UserId = "member" + i, Position = "Member " + i, EffectiveFrom = now.AddDays(-1) }).ToArray(); var ids = members.Select(m => m.Id).ToArray();
        var actor = new BidAccount(members[0].UserId, [ApplicationRoles.TopCommittee], null, false, true, true, [], [tender.Id]); var excluded = new HashSet<string> { "officer", "manager", "evaluator" };
        var round = new TenderApproval { Id = Guid.NewGuid(), TenderId = tender.Id, CommitteeAssignmentId = assignment.Id, Stage = ApprovalStage.TopCommittee, Decision = DecisionStatus.RecommendationPending, RoundOpenedAt = now.AddMinutes(-1), RoundMemberSnapshot = JsonSerializer.Serialize(ids) };
        CommitteeVote V(int member, DecisionStatus choice) => new() { Id = Guid.NewGuid(), TenderId = tender.Id, TenderApprovalId = round.Id, CommitteeMemberId = members[member].Id, Decision = choice.ToString(), Justification = "Documented reason", VotedAt = now };
        foreach (var a in new[] { actor with { Enabled = false }, actor with { EmployeeDepartment = false }, actor with { Roles = [ApplicationRoles.Admin, ApplicationRoles.TopCommittee] }, actor with { Roles = [ApplicationRoles.Supplier, ApplicationRoles.TopCommittee] }, actor with { UserId = "manager" } }) Check(!TopCommitteeVotingRules.Eligible(a, excluded));
        TopCommitteeVotingRules.Selection(members.Select(m => m.UserId).ToArray(), members.Select(m => m.UserId).ToHashSet()); n++; Deny(() => TopCommitteeVotingRules.Selection(["a", "a", "b", "c"], new HashSet<string> { "a", "b", "c" }));
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) for (int z = 0; z < 3; z++) for (int w = 0; w < 3; w++)
        {
            var choices = new[] { x, y, z, w }.Select(i => TopCommitteeVotingRules.Choices[i]).ToArray(); var votes = choices.Select((c, i) => V(i, c)).ToArray(); var tally = TopCommitteeVotingRules.Tally(round, tender.Id, ids, votes); Check(tally.Complete && tally.Outcome == TopCommitteeVotingRules.Choices.Cast<DecisionStatus?>().SingleOrDefault(c => choices.Count(v => v == c) >= 3));
        }
        var partial = new[] { V(0, DecisionStatus.CommitteeApproved), V(1, DecisionStatus.CommitteeApproved), V(2, DecisionStatus.CommitteeApproved) }; Check(!TopCommitteeVotingRules.Tally(round, tender.Id, ids, partial).Complete); Check(TopCommitteeVotingRules.VisibleVotes(round, tender.Id, ids[0], partial).Count == 1); Check(TopCommitteeVotingRules.VisibleVotes(round, tender.Id, null, partial).Count == 0);
        var complete = partial.Append(V(3, DecisionStatus.CommitteeRejected)).ToArray(); Check(TopCommitteeVotingRules.VisibleVotes(round, tender.Id, ids[0], complete).Count == 4);
        TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, [], excluded, DecisionStatus.CommitteeApproved, "Reason", now); n++;
        Deny(() => TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, partial, excluded, DecisionStatus.CommitteeApproved, "Reason", now));
        foreach (var state in Enum.GetValues<TenderStatus>().Where(s => s != TenderStatus.AwaitingCommitteeDecision)) { tender.Status = state; Deny(() => TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, [], excluded, DecisionStatus.CommitteeApproved, "Reason", now)); }
        tender.Status = TenderStatus.AwaitingCommitteeDecision;
        foreach (var choice in Enum.GetValues<DecisionStatus>().Where(c => !TopCommitteeVotingRules.Choices.Contains(c))) Deny(() => TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, [], excluded, choice, "Reason", now));
        foreach (var reason in new[] { "", "   ", new string('x', 8001) }) Deny(() => TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, [], excluded, DecisionStatus.CommitteeApproved, reason, now));
        round.RoundClosedAt = now; Deny(() => TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, [], excluded, DecisionStatus.CommitteeApproved, "Reason", now)); round.RoundClosedAt = null;
        members[0].EffectiveTo = now; Deny(() => TopCommitteeVotingRules.Vote(actor, assignment, members[0], tender, round, ids, [], excluded, DecisionStatus.CommitteeApproved, "Reason", now)); members[0].EffectiveTo = null;
        Deny(() => TopCommitteeVotingRules.Tally(round, tender.Id, ids, [V(0, DecisionStatus.CommitteeApproved), V(0, DecisionStatus.CommitteeRejected)])); Deny(() => TopCommitteeVotingRules.Tally(round, tender.Id, ids, [V(0, DecisionStatus.ManagerApproved)]));
        Check(TopCommitteeVotingRules.AfterDecision(DecisionStatus.CommitteeApproved) == TenderStatus.AwaitingCommitteeDecision); Check(TopCommitteeVotingRules.AfterDecision(DecisionStatus.CommitteeRejected) == TenderStatus.UnderEvaluation); Check(TopCommitteeVotingRules.AfterDecision(DecisionStatus.ReturnedForReview) == TenderStatus.UnderEvaluation); Check(TopCommitteeVotingRules.AfterDecision(null) == TenderStatus.AwaitingCommitteeDecision);
        var report = new EvaluationReport { Id = Guid.NewGuid(), TenderId = tender.Id, SubmittedAt = now }; var bid = new Bid { Id = Guid.NewGuid(), TenderId = tender.Id, SubmittedAt = now, Status = BidStatus.Compliant }; report.RecommendedBidId = bid.Id; round.EvaluationReportId = report.Id; round.Decision = DecisionStatus.CommitteeApproved; round.DecidedAt = now; round.RoundClosedAt = now; var officer = actor with { UserId = "officer", Roles = [ApplicationRoles.SourcingOfficer] }; TopCommitteeVotingRules.Award(officer, tender, round, report, bid, false, complete); n++;
        Deny(() => TopCommitteeVotingRules.Award(actor, tender, round, report, bid, false, complete)); Deny(() => TopCommitteeVotingRules.Award(officer, tender, round, report, bid, true, complete)); bid.Status = BidStatus.NonCompliant; Deny(() => TopCommitteeVotingRules.Award(officer, tender, round, report, bid, false, complete));
        var saved = round.RoundMemberSnapshot;
        foreach (var bad in new[] { "null", "{}", "not-json", "[]", JsonSerializer.Serialize(new[] { ids[0], ids[0], ids[2], ids[3] }) }) { round.RoundMemberSnapshot = bad; Deny(() => TopCommitteeVotingRules.Members(round)); }
        round.RoundMemberSnapshot = saved;
        Deny(() => TopCommitteeVotingRules.Tally(round, Guid.NewGuid(), ids, complete));
        Deny(() => TopCommitteeVotingRules.Tally(round, tender.Id, ids.Skip(1).Append(Guid.NewGuid()).ToArray(), complete));
        bid.Status = BidStatus.Compliant; Deny(() => TopCommitteeVotingRules.Award(officer, tender, round, report, bid, false, partial));
        Console.WriteLine($"PASS: {n} database-free Top Committee voting, confidentiality and award rule checks. No endpoints or database writes enabled.");
    }
}
