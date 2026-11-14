using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Reports;
namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ReportsController(IReporting reports, ILogger<ReportsController> logger) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) { var executed = await next(); if (executed.Exception is not BidException error) return; executed.ExceptionHandled = true; if (error.Status == 403) executed.Result = Forbid(); else if (error.Status == 404) executed.Result = NotFound(); else { Response.StatusCode = error.Status; executed.Result = View("Problem", error.Message); } }
    [HttpGet] public async Task<IActionResult> Index([FromQuery] ReportFilter filter, CancellationToken ct) { if (!ModelState.IsValid) return BadRequest("Invalid report filters."); return View(await reports.ReadAsync(User, filter, ct)); }
    [HttpGet]
    public async Task<IActionResult> Print([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Invalid report filters.");
        var page = await reports.ReadAsync(User, filter, ct);
        BidRules.Require(page.Document.Tables.Count > 0, 400, "Select a report context before printing.");
        return View(page.Document);
    }
    [HttpGet]
    public async Task<IActionResult> Download([FromQuery] ReportFilter filter, string format, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Invalid report filters.");
        if (format is not ("xlsx" or "pdf")) return BadRequest("Choose Excel or PDF.");
        var page = await reports.ReadAsync(User, filter, ct);
        BidRules.Require(page.Document.Tables.Count > 0, 400, "Choose a populated report context before exporting.");
        byte[] content;
        try { content = format == "xlsx" ? ReportExports.Excel(page.Document) : ReportExports.Pdf(page.Document); }
        catch (BidException) { throw; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { logger.LogWarning(e, "Report export unavailable for {Category}.", filter.Category); throw new BidException(503, "Report export is temporarily unavailable. Please try again or contact the administrator."); }
        // Re-authorize after rendering; export generation must not outlive a role/stage revocation.
        var fresh = await reports.ReadAsync(User, filter, ct);
        BidRules.Require(fresh.ScopeKey == page.ScopeKey, 409, "Report access or matching records changed during export. Refresh and try again.");
        Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers["Content-Security-Policy"] = "sandbox";
        return File(content, format == "xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf", $"STMS-{page.Filter.Category}.{format}");
    }
}
