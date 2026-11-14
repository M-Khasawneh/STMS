using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.SourcingOfficer + "," + ApplicationRoles.SourcingManager + "," + ApplicationRoles.EvaluationCommittee + "," + ApplicationRoles.TopCommittee)]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UnifiedReportsController(IUnifiedReportManagement reports) : Controller
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
    [HttpGet] public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await reports.PageAsync(User, id, ct));
    [HttpPost, Authorize(Roles = ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> Save(Guid id, [Bind(Prefix = "Form")] UnifiedReportForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            try { await reports.SaveAsync(User, id, form, false, ct); return RedirectToAction(nameof(Details), new { id }); }
            catch (BidException error) when (error.Status == 400) { ModelState.AddModelError(string.Empty, error.Message); }
        }
        Response.StatusCode = 400;
        return View("Details", await reports.PageAsync(User, id, ct));
    }
    [HttpGet, Authorize(Roles = ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) => View(await reports.PageAsync(User, id, ct));
    [HttpPost, ActionName("Submit"), Authorize(Roles = ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> SubmitPost(Guid id, string version, bool accept, CancellationToken ct)
    {
        BidRules.Require(accept, 400, "Confirm that the saved report is ready for manager review.");
        var page = await reports.PageAsync(User, id, ct);
        page.Form.Version = version;
        await reports.SaveAsync(User, id, page.Form, true, ct);
        return RedirectToAction(nameof(Details), new { id });
    }
}
