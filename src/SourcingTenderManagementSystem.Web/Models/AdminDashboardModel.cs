using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models;

public sealed record DashboardStatus(string Label, long Count, string Tone);
public sealed record DashboardDeadline(Guid Id, string Title, DateTimeOffset Deadline);
public sealed record DashboardActivity(Guid RecordId, string Title, string Controller, string Action, string? Status, DateTimeOffset At)
{
    public string Description => Action switch {
        "Requisition.Created" => "Requisition created", "Requisition.Edited" => "Requisition updated",
        "Requisition.Submitted" => "Submitted for review", "Requisition.Reviewed" => "Review decision recorded",
        "Requisition.ConvertedToTender" => "Converted to tender", "Tender.Created" => "Tender created",
        "Tender.Edited" => "Tender updated", "Tender.StatusChanged" => "Tender status changed",
        "Tender.CriteriaSaved" => "Criteria configured", "Tender.CriteriaApproved" => "Criteria approved",
        "Tender.InvitationsSent" => "Supplier invitations sent", _ => "Record updated" };
}
public sealed record AdminDashboardModel
{
    public bool Available { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
    public long Users { get; init; }
    public long Employees { get; init; }
    public long Departments { get; init; }
    public long Messages { get; init; }
    public IReadOnlyList<DashboardStatus> Suppliers { get; init; } = [];
    public IReadOnlyList<DashboardStatus> Requisitions { get; init; } = [];
    public IReadOnlyList<DashboardStatus> Tenders { get; init; } = [];
    public long PendingSuppliers { get; init; }
    public long RequestsInReview { get; init; }
    public long ApprovedRequests { get; init; }
    public long PastDeadlineOpenTenders { get; init; }
    public IReadOnlyList<DashboardDeadline> Deadlines { get; init; } = [];
    public IReadOnlyList<DashboardActivity> Activity { get; init; } = [];
    public static AdminDashboardModel Unavailable => new();
    public static string Label<T>(T status) where T : Enum => StatusLabel(status.ToString());
    public static string StatusLabel(string status) => status switch {
        "UnderReview" => "Under review", "ConvertedToTender" => "Converted to tender", "UnderEvaluation" => "Under evaluation",
        "AwaitingManagerReview" => "Awaiting manager review", "AwaitingCommitteeDecision" => "Awaiting committee decision", var value => value };
    public static string Tone(string status) => status switch {
        "Approved" or "Awarded" or "ConvertedToTender" => "cyan", "Pending" or "Submitted" or "Published" => "amber",
        "UnderReview" or "Open" or "UnderEvaluation" or "AwaitingManagerReview" or "AwaitingCommitteeDecision" => "blue",
        "Rejected" or "Suspended" or "Cancelled" => "rose", _ => "slate" };
    public static IReadOnlyList<DashboardStatus> Distribution<T>(IReadOnlyDictionary<T, long> counts) where T : struct, Enum =>
        Enum.GetValues<T>().Concat(counts.Keys).Distinct().Where(x => typeof(T) != typeof(TenderStatus) || Convert.ToInt32(x) <= 3 || counts.GetValueOrDefault(x) > 0)
            .Select(x => new DashboardStatus(Label(x), counts.GetValueOrDefault(x), Tone(x.ToString()))).ToArray();
}
