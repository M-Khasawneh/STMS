using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public static class EvaluationRules
{
    public static void Validate(EvaluationForm form, IReadOnlyList<EvaluationCriterion> criteria, bool complete)
    {
        BidRules.Require(TenderManagement.Approved(criteria), 409, "Approved criteria totaling exactly 100% are required.");
        BidRules.Require(form.Items is not null && form.Items.All(x => x is not null) &&
            form.Items.Count == criteria.Count && form.Items.Select(x => x.CriterionId).Distinct().Count() == criteria.Count &&
            form.Items.Select(x => x.CriterionId).Order().SequenceEqual(criteria.Select(x => x.Id).Order()), 400,
            "Include every approved criterion exactly once. Foreign or duplicate criteria are rejected.");
        foreach (var line in form.Items)
        {
            BidRules.Require(line.Score is null || line.Score >= 0 && line.Score <= 100 && decimal.Round(line.Score.Value, 6) == line.Score,
                400, "Scores must be from 0 to 100 with at most six decimal places.");
            BidRules.Require((line.Notes?.Length ?? 0) <= 8000 && (line.NonCompliance?.Length ?? 0) <= 8000, 400, "Evaluation notes are too long.");
            BidRules.Require(line.Score.HasValue || !complete && string.IsNullOrWhiteSpace(line.Notes) && string.IsNullOrWhiteSpace(line.NonCompliance),
                400, complete ? "Score every approved criterion before confirming." : "Enter a score before saving notes for a criterion.");
        }
    }
    public static string Version(Tender tender, Bid bid, Guid? member, IEnumerable<EvaluationScore> scores) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            tender.Status, BidStatus = bid.Status, Member = member,
            Scores = scores.OrderBy(x => x.Id)
        })));
    public static bool Complete(IReadOnlyList<EvaluationScore> scores, Guid memberId, IReadOnlyList<EvaluationCriterion> criteria) =>
        criteria.Count > 0 && criteria.All(c => scores.Count(x => x.MemberId == memberId && x.CriterionId == c.Id && x.SubmittedAt.HasValue) == 1);
    public static IReadOnlyList<CriterionResult> Aggregate(IReadOnlyList<EvaluationScore> scores, IReadOnlyList<CommitteeMember> members, IReadOnlyList<EvaluationCriterion> criteria)
    {
        if (members.Count != 4 || members.Select(x => x.UserId).Distinct().Count() != 4 || members.Any(m => !Complete(scores, m.Id, criteria))) return [];
        return criteria.Select(c => {
            var average = members.Average(m => scores.Single(x => x.MemberId == m.Id && x.CriterionId == c.Id).Score);
            return new CriterionResult(c, average, average * c.Weight / 100m);
        }).ToArray();
    }
}
