using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

// Deliberately no supplier, status, currency, quantity or calculated-total inputs.
public sealed class BidDraftForm
{
    public string? Version { get; set; }
    [Required] public string DeliveryTimeframe { get; set; } = "";
    [Required] public string TechnicalDetails { get; set; } = "";
    [Required] public string PaymentTerms { get; set; } = "";
    [Required] public DateOnly? OfferValidUntil { get; set; }
    public List<BidLineForm> Items { get; set; } = [];
    public List<BidChargeForm> Charges { get; set; } = [];
}
public sealed class BidLineForm
{
    public Guid ItemId { get; set; }
    public decimal UnitPrice { get; set; }
}
public sealed class BidChargeForm
{
    public string CostType { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
public sealed record BidAccount(string UserId, string[] Roles, Guid? SupplierId, bool SupplierApproved,
    bool Enabled, bool EmployeeDepartment, Guid[] EvaluationTenders, Guid[] TopTenders)
{
    public bool Supplier => Enabled && SupplierApproved && SupplierId.HasValue && Roles.Length == 1 && Roles[0] == ApplicationRoles.Supplier;
    public bool Internal => Enabled && EmployeeDepartment && SupplierId is null && !Roles.Contains(ApplicationRoles.Supplier) && !Roles.Contains(ApplicationRoles.Admin);
}
public sealed record BidContext(Tender Tender, IReadOnlyList<PurchaseRequisitionItem> Items, IReadOnlyList<EvaluationCriterion> Criteria);
public sealed record BidParticipation(TenderSupplier Link, Invitation Invitation);
public sealed record BidSnapshot(Bid Bid, IReadOnlyList<BidItem> Items, IReadOnlyList<BidCostComponent> Charges, IReadOnlyList<BidDocument> Documents);
public sealed record BidDetails(BidSnapshot Snapshot, string Version, decimal Total);
