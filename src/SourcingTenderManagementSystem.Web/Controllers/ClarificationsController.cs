using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Clarifications;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.RequestingDepartment + "," + ApplicationRoles.SourcingOfficer + "," + ApplicationRoles.SourcingManager + "," + ApplicationRoles.EvaluationCommittee + "," + ApplicationRoles.TopCommittee)]
[AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ClarificationsController(IInternalClarifications clarifications) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not BidException error) return;
        executed.ExceptionHandled = true;
        if (error.Status == 403) executed.Result = Forbid();
        else if (error.Status == 404) executed.Result = NotFound();
        else { Response.StatusCode = error.Status; executed.Result = View("Problem", error.Message); }
    }
    [HttpGet] public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await clarifications.InboxAsync(User, page, ct));
    [HttpGet] public async Task<IActionResult> Details(Guid id, bool requisition = false, CancellationToken ct = default) => View(await clarifications.PageAsync(User, requisition, id, ct));
    [HttpPost]
    public async Task<IActionResult> Ask(Guid id, bool requisition, InternalQuestionForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Invalid(id, requisition, ct);
        await clarifications.AskAsync(User, requisition, id, form, ct); return Done(id, requisition, "Clarification sent.");
    }
    [HttpPost]
    public async Task<IActionResult> Reply(Guid id, bool requisition, Guid requestId, InternalReplyForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Invalid(id, requisition, ct);
        await clarifications.ReplyAsync(User, requisition, id, requestId, form, ct); return Done(id, requisition, "Your response has been recorded and cannot be edited.");
    }
    [HttpPost]
    public async Task<IActionResult> Resolve(Guid id, bool requisition, Guid requestId, string version, CancellationToken ct)
    {
        await clarifications.ResolveAsync(User, requisition, id, requestId, version, ct); return Done(id, requisition, "Clarification resolved.");
    }
    private IActionResult Done(Guid id, bool requisition, string message) { TempData["ClarificationMessage"] = message; return RedirectToAction(nameof(Details), new { id, requisition }); }
    private async Task<IActionResult> Invalid(Guid id, bool requisition, CancellationToken ct) { Response.StatusCode = 400; return View("Details", await clarifications.PageAsync(User, requisition, id, ct)); }
}
