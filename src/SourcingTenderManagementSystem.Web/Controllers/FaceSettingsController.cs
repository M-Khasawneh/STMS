using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = "SignedInAccount"), AutoValidateAntiforgeryToken, EnableRateLimiting("face-settings")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true), RequestSizeLimit(32768)]
public sealed class FaceSettingsController(FaceRecognition faces) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!Request.IsHttps) { context.Result = StatusCode(400); return; }
        var result = await next();
        if (result.Exception is BidException e) {
            result.ExceptionHandled = true;
            result.Result = e.Status == 403 ? Forbid() : StatusCode(e.Status, new { error = e.Message });
        }
    }
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await faces.Page(User, ct));
    [HttpPost] public async Task<IActionResult> Begin([FromBody] FaceOperation request)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "Face operation details are invalid." });
        return Json(new { token = await faces.Begin(User, request) });
    }
    [HttpPost] public async Task<IActionResult> Complete([FromBody] FaceCapture capture, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "The face capture is invalid. Start again." });
        return Json(new { matched = await faces.Complete(User, capture, ct) });
    }
}
