using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Tenders;
public sealed class TenderForm
{
    [Required] public Guid? RequisitionId { get; set; }
    [Required, StringLength(256)] public string Title { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public string Terms { get; set; } = "";
    [Required, Display(Name = "Submission start (Asia/Amman)")] public DateTime? Start { get; set; }
    [Required, Display(Name = "Submission deadline (Asia/Amman)")] public DateTime? Deadline { get; set; }
    public string? Version { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public Guid? Id { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public IReadOnlyList<PurchaseRequisition> EligibleRequests { get; set; } = [];
}
public sealed class CriterionForm
{
    public Guid? Id { get; set; }
    [Required, StringLength(256)] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public EvaluationKind? Kind { get; set; }
    [Required, Range(typeof(decimal), "0", "100")] public decimal? Weight { get; set; }
}
public sealed class CriteriaForm
{
    [Required] public string Version { get; set; } = "";
    public List<CriterionForm> Items { get; set; } = [];
}
public sealed class InviteForm
{
    [Required] public string Version { get; set; } = "";
    public List<Guid> SupplierIds { get; set; } = [];
}
public sealed record TenderActor(string Id, bool Officer, bool Admin, bool Manager, bool Requester, Guid? SupplierId)
{
    public bool Manage => Officer && !Admin && SupplierId is null;
    public bool Approve => Manager && !Admin && SupplierId is null;
    public bool Internal => SupplierId is null && (Admin || Officer || Manager);
}
public sealed record TenderDetails(Tender Tender, PurchaseRequisition Source, IReadOnlyList<PurchaseRequisitionItem> Items,
    IReadOnlyList<EvaluationCriterion> Criteria, string Version, TenderActor Actor);
public sealed record InvitationRow(Invitation Invitation, Tender Tender, SupplierProfile Supplier);
public sealed record TenderInvitations(TenderDetails Details, IReadOnlyList<InvitationRow> Invitations, IReadOnlyList<SupplierProfile> Eligible);
