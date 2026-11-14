using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

public sealed class UnifiedReportForm
{
    [Required] public string Version { get; set; } = "";
    public Guid? RecommendedBidId { get; set; }
    [StringLength(12000)] public string? Observations { get; set; }
    [StringLength(12000)] public string? NonCompliance { get; set; }
    [StringLength(12000)] public string? FinalResult { get; set; }
    [StringLength(12000)] public string? Recommendation { get; set; }
}
public sealed record ReportMember(Guid Id, string UserId, string Name, string Position);
public sealed record ReportCriterion(Guid Id, string Name, EvaluationKind Kind, decimal Weight);
public sealed record ReportPoints(Guid CriterionId, decimal Average, decimal WeightedPoints);
public sealed record ReportLine(string Description, string Specifications, decimal Quantity, decimal UnitPrice, decimal Total);
public sealed record ReportCharge(string Type, decimal Amount);
public sealed record ReportNote(Guid MemberId, string MemberName, Guid CriterionId, decimal Score, string Notes, string? NonCompliance, DateTimeOffset ConfirmedAt);
public sealed record ReportOffer(Guid BidId, string Supplier, BidStatus Status, int ConfirmedMembers, decimal Total,
    string PaymentTerms, string Delivery, DateOnly? ValidUntil, IReadOnlyList<ReportLine> Items, IReadOnlyList<ReportCharge> Charges,
    IReadOnlyList<ReportPoints> Results, IReadOnlyList<ReportNote> Notes, IReadOnlyList<string> ComplianceReasons);
public sealed record ReportSnapshot(int FormatVersion, Guid TenderId, string TenderTitle, IReadOnlyList<ReportMember> Members,
    IReadOnlyList<ReportCriterion> Criteria, IReadOnlyList<ReportOffer> Offers);
public sealed record UnifiedReportPage(Tender Tender, EvaluationReport? Report, ReportSnapshot? Snapshot, UnifiedReportForm Form,
    bool CanEdit, bool Ready, IReadOnlyList<AuditLog> History)
{
    public bool CanReview { get; init; }
    public bool Archived { get; init; }
    public ReportDecisionForm DecisionForm { get; init; } = new();
    public IReadOnlyList<EvaluationReport> Versions { get; init; } = [];
    public IReadOnlyList<TenderApproval> Decisions { get; init; } = [];
}
public sealed class ReportDecisionForm
{
    [Required] public string Version { get; set; } = "";
    public DecisionStatus Decision { get; set; } = DecisionStatus.RecommendationPending;
    [StringLength(8000)] public string? Comments { get; set; }
    public bool Accept { get; set; }
}
