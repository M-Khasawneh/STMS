using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = ApplicationRoles.Admin), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MessagesController(NotificationInbox inbox) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        if (result.Exception is UnauthorizedAccessException) { result.ExceptionHandled = true; result.Result = Forbid(); }
    }
    [HttpGet] public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await inbox.PageAsync(User, page, true, ct));
    [HttpGet] public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var message = await inbox.MessageAsync(User, id, ct);
        return message is null ? NotFound() : View(message);
    }
    [HttpPost] public async Task<IActionResult> Open(Guid id, CancellationToken ct)
    {
        if (await inbox.MessageAsync(User, id, ct) is null) return NotFound();
        if (!await inbox.MarkReadAsync(User, id, ct)) return NotFound();
        return RedirectToAction(nameof(Details), new { id });
    }
}
