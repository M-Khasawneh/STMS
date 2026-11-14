using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Supplier + "," + ApplicationRoles.SourcingOfficer + "," +
    ApplicationRoles.SourcingManager + "," + ApplicationRoles.EvaluationCommittee + "," + ApplicationRoles.TopCommittee)]
[AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ComplianceController(IComplianceManagement compliance) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not BidException error) return;
        executed.ExceptionHandled = true;
        if (error.Status == 403) executed.Result = Forbid();
        else if (error.Status == 404) executed.Result = NotFound();
        else if (error.Status == 400 && context.ActionArguments.TryGetValue("id", out var value) && value is Guid bidId) {
            ModelState.AddModelError(string.Empty, error.Message);
            executed.Result = await Invalid(bidId, context.HttpContext.RequestAborted);
        }
        else {
            Response.StatusCode = error.Status;
            executed.Result = View("Problem", error.Message);
        }
    }
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await compliance.PageAsync(User, id, ct));

    [HttpPost, Authorize(Roles = ApplicationRoles.SourcingOfficer)]
    public async Task<IActionResult> Decide(Guid id, ComplianceDecisionForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Invalid(id, ct);
        await compliance.DecideAsync(User, id, form, ct);
        return Done(id, "Compliance decision recorded.");
    }
    [HttpPost, Authorize(Roles = ApplicationRoles.SourcingOfficer)]
    public async Task<IActionResult> Ask(Guid id, ClarificationForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Invalid(id, ct);
        await compliance.AskAsync(User, id, form, ct);
        return Done(id, "Clarification sent to the supplier.");
    }
    [HttpPost, Authorize(Roles = ApplicationRoles.Supplier), RequestSizeLimit(54 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 54 * 1024 * 1024)]
    public async Task<IActionResult> Reply(Guid id, Guid requestId, ClarificationReplyForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Invalid(id, ct);
        await compliance.ReplyAsync(User, id, requestId, form, ct);
        return Done(id, "Your response and attachments have been recorded.");
    }
    [HttpPost, Authorize(Roles = ApplicationRoles.SourcingOfficer)]
    public async Task<IActionResult> Resolve(Guid id, Guid requestId, string version, CancellationToken ct)
    {
        await compliance.ResolveAsync(User, id, requestId, version, ct);
        return Done(id, "Clarification resolved. Record the compliance outcome when your review is complete.");
    }
    [HttpGet]
    public async Task<IActionResult> Document(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var download = await compliance.DownloadAsync(User, id, attachmentId, ct);
        try {
            foreach (var header in download.Headers) Response.Headers[header.Key] = header.Value;
            return File(download.Content, download.ContentType, download.OriginalName, enableRangeProcessing: false);
        }
        catch { await download.DisposeAsync(); throw; }
    }
    private IActionResult Done(Guid id, string message)
    {
        TempData["ComplianceMessage"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }
    private async Task<IActionResult> Invalid(Guid id, CancellationToken ct)
    {
        Response.StatusCode = 400;
        return View("Details", await compliance.PageAsync(User, id, ct));
    }
}
