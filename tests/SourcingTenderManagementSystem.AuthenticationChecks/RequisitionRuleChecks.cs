using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;
public static class RequisitionRuleChecks
{
    public static void Run()
    {
        var department = Guid.NewGuid(); var other = Guid.NewGuid();
        var owner = new RequisitionActor("owner", department, true, false, false, false);
        var colleague = owner with { UserId = "colleague" };
        var officer = new RequisitionActor("officer", other, false, true, false, false);
        var admin = new RequisitionActor("admin", other, true, true, true, false);
        var supplier = new RequisitionActor("supplier", department, true, true, true, true);
        var request = new PurchaseRequisition { CreatedByUserId = "owner", DepartmentId = department, Status = PurchaseRequestStatus.Draft };
        Check(owner.CanCreate && owner.CanRead(request) && owner.CanEdit(request), "owner can compose own draft");
        Check(!colleague.CanRead(request) && !colleague.CanEdit(request), "same-department colleague is isolated");
        Check(officer.CanRead(request) && !officer.CanEdit(request) && !officer.CanCreate && officer.CanReview(request), "officer view/review is distinct from content editing");
        Check(!(officer with { DepartmentId = department }).CanReview(request), "officer cannot review own department");
        Check(!(officer with { UserId = "owner" }).CanReview(request), "officer cannot review own request even in another department");
        Check(admin.CanRead(request) && !admin.CanEdit(request) && !admin.CanReview(request) && !admin.CanCreate, "Admin remains oversight-only with multiple roles");
        Check(!supplier.CanRead(request) && !supplier.CanReview(request), "Supplier role excludes requisition access");
        foreach (var state in Enum.GetValues<PurchaseRequestStatus>())
        {
            request.Status = state;
            Check(owner.CanEdit(request) == (state is PurchaseRequestStatus.Draft or PurchaseRequestStatus.Returned), "owner editing state: " + state);
            foreach (var decision in Enum.GetValues<PurchaseRequestStatus>())
            {
                var expected = state == PurchaseRequestStatus.Submitted && decision == PurchaseRequestStatus.UnderReview || state == PurchaseRequestStatus.UnderReview && decision is PurchaseRequestStatus.Approved or PurchaseRequestStatus.Rejected or PurchaseRequestStatus.Returned;
                if (RequisitionActor.ReviewTransition(state, decision) != expected) throw new Exception("Invalid review state matrix.");
            }
        }
        Console.WriteLine("PASS: all review transition pairs match approved rules");
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var service = new RequisitionManagement(db, null!, TimeProvider.System);
        var form = new RequisitionForm { Title = "Need", Reason = "Reason", Priority = "Medium", TargetDate = service.Today };
        service.Validate(form, false); Reject(form, true, "submission requires an item");
        form.Items.Add(new() { Description = "Material", Quantity = .125m, Specifications = "Required specification" });
        service.Validate(form, true); Console.WriteLine("PASS: positive fractional quantity and today's date accepted");
        form.TargetDate = service.Today.AddDays(-1); service.Validate(form, false); Reject(form, true, "past date blocked only at submission"); form.TargetDate = service.Today;
        foreach (var priority in new[] { "Invalid", "", "medium" }) { form.Priority = priority; Reject(form, false, "invalid priority refused"); } form.Priority = "Urgent";
        foreach (var quantity in new[] { 0m, -1m, .0000001m, 1000000000000m }) { form.Items[0].Quantity = quantity; Reject(form, false, "unrepresentable or nonpositive quantity refused"); } form.Items[0].Quantity = 1m;
        form.Items[0].Specifications = " "; Reject(form, true, "specifications required");
        void Reject(RequisitionForm input, bool submit, string message) { try { service.Validate(input, submit); throw new Exception("Expected rejection: " + message); } catch (RequisitionException) { Console.WriteLine("PASS: " + message); } }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}
