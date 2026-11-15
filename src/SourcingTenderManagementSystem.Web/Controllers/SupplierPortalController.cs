using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Policy="SignedInAccount",Roles=ApplicationRoles.Supplier)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SupplierPortalController(ISupplierTenderPortal portal):Controller
{
    [HttpGet] public async Task<IActionResult> Details(Guid id,CancellationToken ct)
    {
        try{return View(await portal.ReadAsync(User,id,ct));}
        catch(BidException e){return Failure(e.Status,e.Message);}
        catch(TenderException e){return Failure(e.Status,e.Message);}
        catch(Exception e)when(e is System.Data.Common.DbException or TimeoutException){return Failure(503,"Your tender workspace is temporarily unavailable. Please try again.");}
    }
    private IActionResult Failure(int status,string message){if(status==403)return Forbid();if(status==404)return NotFound();Response.StatusCode=status;return View("~/Views/Shared/WorkspaceProblem.cshtml",message);}
}
