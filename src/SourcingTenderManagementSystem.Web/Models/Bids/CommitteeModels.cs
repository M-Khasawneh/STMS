using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

public sealed class CommitteeForm
{
    [Required] public string Version { get; set; } = "";
    [Required] public string ManagerUserId { get; set; } = "";
    [Required] public string DepartmentRepresentativeId { get; set; } = "";
    [Required] public string FinancialRepresentativeId { get; set; } = "";
    [Required] public string TechnicalRepresentativeId { get; set; } = "";
    [StringLength(8000)] public string? Reason { get; set; }
}
public sealed record CommitteeCandidate(string Id, string Name, Guid DepartmentId, string[] Roles);
public sealed record CommitteeHistory(CommitteeMember Member, string Name);
public sealed record CommitteePage(Tender Tender, Guid SourceDepartmentId, CommitteeForm Form, bool EvaluationStarted,
    IReadOnlyList<CommitteeCandidate> Candidates, IReadOnlyList<CommitteeHistory> History,
    IReadOnlyList<AuditLog> Changes);
