using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Policy="SignedInAccount")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class WorkController(IWorkspaceDiscovery discovery) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var done=await next();
        if(done.Exception is BidException e){done.ExceptionHandled=true;if(e.Status==403)done.Result=Forbid();else {Response.StatusCode=e.Status;done.Result=View("~/Views/Shared/WorkspaceProblem.cshtml",e.Message);}}
        else if(done.Exception is System.Data.Common.DbException or TimeoutException){done.ExceptionHandled=true;Response.StatusCode=503;done.Result=View("~/Views/Shared/WorkspaceProblem.cshtml","Your workspace data is temporarily unavailable. Please try again.");}
    }
    [HttpGet] public async Task<IActionResult> Tasks(string? q,string? category,int page=1,CancellationToken ct=default)=>View("Index",await discovery.ReadAsync(User,true,q,category,page,ct));
    [HttpGet] public async Task<IActionResult> Search(string? q,string? category,int page=1,CancellationToken ct=default)=>View("Index",await discovery.ReadAsync(User,false,q,category,page,ct));
    [HttpGet,Authorize(Roles=ApplicationRoles.Admin)] public async Task<IActionResult> Audit(string? q,string? type,DateOnly? from,DateOnly? to,int page=1,CancellationToken ct=default)
    {if(!ModelState.IsValid)return BadRequest();return View(await discovery.AuditAsync(User,q,type,from,to,page,ct));}
}
