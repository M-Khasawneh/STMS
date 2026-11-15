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
    private async Task<bool> LiveAccess(IBidData accounts,TimeProvider clock,CancellationToken ct) => WorkspaceDiscovery.Eligible(await accounts.AccountAsync(User,clock.GetUtcNow(),ct));
    [HttpGet] public async Task<IActionResult> Menu([FromServices] IBidData accounts,[FromServices] TimeProvider clock,string? returnUrl,CancellationToken ct)
    {
        if(!await LiveAccess(accounts,clock,ct))return Forbid();
        Response.Headers["X-STMS-Notification-Menu"]="1";
        return PartialView("_NotificationMenu",(await inbox.MenuAsync(User,ct)) with {ReturnUrl=Url.IsLocalUrl(returnUrl)?returnUrl:null});
    }
    [HttpGet] public async Task<IActionResult> Watch([FromServices] IBidData accounts,[FromServices] TimeProvider clock,[FromServices] NotificationWatchGate gate,CancellationToken ct)
    {
        if(!await LiveAccess(accounts,clock,ct))return Forbid();
        var id=User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        using var lease=gate.Enter(id);if(lease is null)return StatusCode(429);
        Response.ContentType="text/event-stream";Response.Headers["X-Accel-Buffering"]="no";Response.Headers.CacheControl="no-store, private";
        HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
        try{
            string? previous=null;
            // Short-lived streams reconnect natively. Recheck account status/roles on every committed read.
            for(var n=0;n<6;n++){
                if(!await LiveAccess(accounts,clock,ct)){await Response.WriteAsync("event: revoked\ndata: {}\n\n",ct);await Response.Body.FlushAsync(ct);break;}
                var menu=await inbox.MenuAsync(User,ct);
                if(!menu.Available){await Response.WriteAsync("event: unavailable\ndata: {}\n\n",ct);await Response.Body.FlushAsync(ct);break;}
                var version=NotificationWatchGate.Version(menu);
                if(version!=previous){await Response.WriteAsync("event: changed\ndata: {}\n\n",ct);previous=version;}
                else await Response.WriteAsync(": heartbeat\n\n",ct);
                await Response.Body.FlushAsync(ct);if(n<5)await Task.Delay(TimeSpan.FromSeconds(15),ct);
                else{await Response.WriteAsync("event: reconnect\ndata: {}\n\n",ct);await Response.Body.FlushAsync(ct);}
            }
        }catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch(Exception e)when(e is System.Data.Common.DbException or TimeoutException){if(!Response.HasStarted)return StatusCode(503);}
        return new EmptyResult();
    }
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
