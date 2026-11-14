using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Policy="SignedInAccount"), AutoValidateAntiforgeryToken]
[ResponseCache(Duration=0,Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class NotificationsController(NotificationInbox inbox) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        if (result.Exception is UnauthorizedAccessException) { result.ExceptionHandled = true; result.Result = Forbid(); }
    }
    [HttpGet] public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await inbox.PageAsync(User, page, false, ct));
    [HttpPost] public async Task<IActionResult> Read(Guid id, string? returnUrl, CancellationToken ct)
    {
        if (!await inbox.MarkReadAsync(User, id, ct)) return NotFound();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") {
            Response.Headers["X-STMS-Notification-Menu"] = "1";
            return PartialView("_NotificationMenu", (await inbox.MenuAsync(User, ct)) with { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
        }
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
