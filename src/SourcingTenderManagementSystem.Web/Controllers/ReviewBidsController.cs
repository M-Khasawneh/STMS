using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.SourcingOfficer + "," + ApplicationRoles.SourcingManager + "," +
    ApplicationRoles.EvaluationCommittee + "," + ApplicationRoles.TopCommittee)]
[AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ReviewBidsController(BidReviewPages pages, BidManagement bids, BidDocumentManagement documents) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try { await pages.RequireReviewerAsync(User, context.HttpContext.RequestAborted); }
        catch (BidException e) { context.Result = Failure(e); return; }
        var executed = await next();
        if (executed.Exception is BidException error) {
            executed.ExceptionHandled = true;
            executed.Result = Failure(error);
        }
    }
    [HttpGet] public async Task<IActionResult> Index(Guid? tenderId, CancellationToken ct) =>
        View(await pages.ListAsync(User, tenderId, ct));
    [HttpGet] public async Task<IActionResult> Details(Guid id, CancellationToken ct) =>
        View(await pages.DetailsAsync(User, id, ct));
    [HttpGet] public async Task<IActionResult> Compare(Guid tenderId, CancellationToken ct) =>
        View(await pages.CompareAsync(User, tenderId, ct));
    [HttpGet] public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        View("~/Views/Bids/History.cshtml", await bids.HistoryAsync(User, id, ct));
    [HttpGet] public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken ct)
    {
        var download = await documents.DownloadAsync(User, id, documentId, ct);
        try {
            foreach (var header in download.Headers) Response.Headers[header.Key] = header.Value;
            return File(download.Content, download.ContentType, download.OriginalName, enableRangeProcessing: false);
        }
        catch { await download.DisposeAsync(); throw; }
    }
    private IActionResult Failure(BidException error)
    {
        if (error.Status == 403) return Forbid();
        if (error.Status == 404) return NotFound();
        Response.StatusCode = error.Status;
        return View("Problem", error.Message);
    }
}
