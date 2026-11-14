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
    [HttpGet] public async Task<IActionResult> Details(Guid id, Guid? reportId, CancellationToken ct) => View(await reports.PageAsync(User, id, ct, reportId));
    [HttpGet, Authorize(Roles = ApplicationRoles.SourcingManager)]
    public async Task<IActionResult> Review(Guid id, CancellationToken ct)
    {
        var page = await reports.PageAsync(User, id, ct);
        BidRules.Require(page.CanReview, 403, "Only the appointed manager may review a current submitted report awaiting decision.");
        return View(page);
    }
    [HttpPost, Authorize(Roles = ApplicationRoles.SourcingManager)]
    public async Task<IActionResult> Review(Guid id, [Bind(Prefix = "DecisionForm")] ReportDecisionForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            try {
                await reports.DecideAsync(User, id, form, ct);
                TempData["UiFeedback"] = "Manager decision recorded.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BidException error) when (error.Status == 400) { ModelState.AddModelError(string.Empty, error.Message); }
        }
        var page = await reports.PageAsync(User, id, ct);
        BidRules.Require(page.CanReview, 403, "This report is not available for your decision.");
        Response.StatusCode = 400;
        return View(page with { DecisionForm = form });
    }
    [HttpPost, Authorize(Roles = ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> Save(Guid id, [Bind(Prefix = "Form")] UnifiedReportForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            try { await reports.SaveAsync(User, id, form, false, ct); TempData["UiFeedback"] = "Report draft saved."; return RedirectToAction(nameof(Details), new { id }); }
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
        await reports.SaveAsync(User, id, page.Form, true, ct); TempData["UiFeedback"] = "Unified report submitted for manager review.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
