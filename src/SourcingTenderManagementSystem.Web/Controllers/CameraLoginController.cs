using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[AllowAnonymous, AutoValidateAntiforgeryToken, EnableRateLimiting("face-login"), RequestSizeLimit(32768)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CameraLoginController(CameraLogin login) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!Request.IsHttps) { context.Result = BadRequest(); return; }
        var result = await next();
        if (result.Exception is BidException e) {
            result.ExceptionHandled = true;
            result.Result = StatusCode(e.Status, new { error = e.Message });
        }
    }
    [HttpPost] public async Task<IActionResult> Begin([FromBody] FaceLoginStart request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "Enter your username before using the camera." });
        return Json(new { token = await login.Begin(request.UserName, ct) });
    }
    [HttpPost] public async Task<IActionResult> Complete([FromBody] FaceCapture request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "Unable to complete camera sign-in. Use your password or passkey." });
        await login.Complete(request, ct);
        TempData["SignedInWelcome"] = true;
        return Json(new { redirectUrl = Url.IsLocalUrl(request.ReturnUrl) ? request.ReturnUrl : Url.Action("Index", "Workspace") });
    }
}
