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
public sealed class EvaluationsController(IEvaluationManagement evaluations) : Controller
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
    [HttpGet] public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await evaluations.PageAsync(User, id, ct));
    [HttpPost, Authorize(Roles = ApplicationRoles.SourcingOfficer)]
    public async Task<IActionResult> Start(Guid id, string version, CancellationToken ct)
    {
        await evaluations.StartAsync(User, id, version, ct); TempData["UiFeedback"] = "Evaluation started.";
        return RedirectToAction("Index", "ReviewBids", new { tenderId = id });
    }
    [HttpPost, Authorize(Roles = ApplicationRoles.SourcingManager + "," + ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> Save(Guid id, [Bind(Prefix = "Form")] EvaluationForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            try { await evaluations.SaveAsync(User, id, form, false, ct); TempData["UiFeedback"] = "Evaluation draft saved."; return RedirectToAction(nameof(Details), new { id }); }
            catch (BidException e) when (e.Status == 400) { ModelState.AddModelError(string.Empty, e.Message); }
        }
        Response.StatusCode = 400;
        return View("Details", await evaluations.PageAsync(User, id, ct));
    }
    [HttpGet, Authorize(Roles = ApplicationRoles.SourcingManager + "," + ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct) => View(await evaluations.PageAsync(User, id, ct));
    [HttpPost, ActionName("Confirm"), Authorize(Roles = ApplicationRoles.SourcingManager + "," + ApplicationRoles.EvaluationCommittee)]
    public async Task<IActionResult> ConfirmPost(Guid id, string version, bool accept, CancellationToken ct)
    {
        BidRules.Require(accept, 400, "Confirm that your scores are final before submitting.");
        var page = await evaluations.PageAsync(User, id, ct);
        page.Form.Version = version;
        await evaluations.SaveAsync(User, id, page.Form, true, ct); TempData["UiFeedback"] = "Evaluation confirmed.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
