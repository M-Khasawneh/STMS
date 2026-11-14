using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

public sealed class EvaluationForm
{
    [Required] public string Version { get; set; } = "";
    public List<EvaluationLine> Items { get; set; } = [];
}
public sealed class EvaluationLine
{
    public Guid CriterionId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? Score { get; set; }
    [StringLength(8000)] public string? Notes { get; set; }
    [StringLength(8000)] public string? NonCompliance { get; set; }
}
public sealed record EvaluationScore(Guid Id, Guid? MemberId, Guid CriterionId, decimal Score, string Notes, string? NonCompliance, DateTimeOffset At, DateTimeOffset? SubmittedAt);
public sealed record CriterionResult(EvaluationCriterion Criterion, decimal Average, decimal WeightedPoints);
public sealed record EvaluationPage(Bid Bid, Tender Tender, string SupplierName, IReadOnlyList<EvaluationCriterion> Criteria,
    EvaluationForm Form, bool CanEdit, bool Confirmed, int ConfirmedMembers, IReadOnlyList<CriterionResult> Results,
    IReadOnlyList<EvaluationScore> MyHistory);
