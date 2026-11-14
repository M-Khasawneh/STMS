using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Loaded only by the explicit memory preview host, never by the production web assembly.
[Authorize]
public sealed class NotesPreviewController : Controller
{
    [HttpGet] public IActionResult ComposeRequest()
    {
        RouteData.Values["controller"] = "Requisitions"; RouteData.Values["action"] = "Create";
        return View("~/Views/Requisitions/Create.cshtml", new RequisitionForm { DepartmentName = "Fixture IT department", Today = DateOnly.FromDateTime(DateTime.Today),
            Items = [new RequisitionItemForm { Quantity = 1m }] });
    }
    [HttpGet] public IActionResult Record(PurchaseRequestStatus status = PurchaseRequestStatus.Approved)
    {
        RouteData.Values["controller"] = "Requisitions"; RouteData.Values["action"] = "Details";
        var now = DateTimeOffset.UtcNow;
        var department = Guid.NewGuid(); var id = Guid.NewGuid();
        var request = new PurchaseRequisition { Id = id, Title = "Fixture network renewal", DepartmentId = department, CreatedByUserId = "requester", Status = status,
            Priority = "High", TargetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)), CreatedAt = now.AddDays(-3), SubmittedAt = now.AddDays(-2), Reason = "Replace end-of-life network equipment." };
        var reviews = new[] { new PurchaseRequisitionReview { Outcome = PurchaseRequestStatus.UnderReview, ReviewedAt = now.AddDays(-1), Comments = "Fixture review started" }, new PurchaseRequisitionReview { Outcome = PurchaseRequestStatus.Approved, ReviewedAt = now, Comments = "Fixture approved" } };
        var history = new[] { "Created", "Submitted", "Reviewed", "Approved" }.Select((action, i) => new AuditLog { Id = Guid.NewGuid(), Action = action, PreviousStatus = new[] { "Draft", "Draft", "Submitted", "UnderReview" }[i], NewStatus = new[] { "Draft", "Submitted", "UnderReview", "Approved" }[i], OccurredAt = now.AddDays(i - 3) }).ToArray();
        return View("~/Views/Requisitions/Details.cshtml", new RequisitionDetails(request, [new PurchaseRequisitionItem { Description = "Managed network switch", Specifications = "24 ports, warranty included", Quantity = 12m }], reviews, history, "Fixture IT department", "Fixture requester", "fixture-version", new RequisitionActor("requester", department, true, false, false, false)));
    }
    [HttpGet] public IActionResult Success()
    {
        TempData["UiFeedback"] = "Fixture item saved successfully.";
        return RedirectToAction(nameof(ComposeRequest));
    }
}
internal sealed class NotesPresentation : IWorkspacePresentation
{
    private readonly Notification[] notifications = Enumerable.Range(0, 3).Select(_ => new Notification { RecipientUserId = "admin", Subject = "Fixture notification" }).ToArray();
    public bool HasAvatar(ClaimsPrincipal user) => false;
    public Task<int?> UnreadAsync(ClaimsPrincipal user) => Task.FromResult<int?>(notifications.Count(x => x.RecipientUserId == user.FindFirstValue(ClaimTypes.NameIdentifier) && x.ReadAt == null));
}
