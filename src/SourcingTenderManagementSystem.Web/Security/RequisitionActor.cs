using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Security;

// Restrictive roles take precedence: Admin is oversight-only; Officer never changes request content.
public sealed record RequisitionActor(string UserId, Guid DepartmentId, bool Requester, bool Officer, bool Admin, bool Supplier)
{
    public bool CanAccess => !Supplier && (Requester || Officer || Admin);
    public bool CanCreate => CanAccess && Requester && !Officer && !Admin;
    public bool CanRead(PurchaseRequisition request) => CanAccess && (Admin || Officer || Requester && request.CreatedByUserId == UserId);
    public bool CanEdit(PurchaseRequisition request) => CanCreate && request.CreatedByUserId == UserId &&
        request.Status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.Returned;
    public bool CanReview(PurchaseRequisition request) => CanAccess && Officer && !Admin && request.CreatedByUserId != UserId && request.DepartmentId != DepartmentId;
    public static bool ReviewTransition(PurchaseRequestStatus from, PurchaseRequestStatus to) =>
        from == PurchaseRequestStatus.Submitted && to == PurchaseRequestStatus.UnderReview ||
        from == PurchaseRequestStatus.UnderReview && to is PurchaseRequestStatus.Approved or PurchaseRequestStatus.Rejected or PurchaseRequestStatus.Returned;
}
