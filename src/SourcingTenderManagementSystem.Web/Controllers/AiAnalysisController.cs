using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Analysis;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Roles=ApplicationRoles.SourcingOfficer+","+ApplicationRoles.SourcingManager+","+ApplicationRoles.EvaluationCommittee+","+ApplicationRoles.TopCommittee),AutoValidateAntiforgeryToken,ResponseCache(Duration=0,NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AiAnalysisController(IAiBidManagement analysis):Controller {
 public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next){var done=await next();if(done.Exception is not BidException e)return;done.ExceptionHandled=true;if(e.Status==403)done.Result=Forbid();else if(e.Status==404)done.Result=NotFound();else{Response.StatusCode=e.Status;done.Result=View("Problem",e.Message);}}
 [HttpGet]public async Task<IActionResult> Details(Guid id,CancellationToken ct)=>View(await analysis.PageAsync(User,id,ct));
 [HttpPost]public async Task<IActionResult> Run(Guid id,AiRunForm form,CancellationToken ct){if(ModelState.IsValid){try{await analysis.RunAsync(User,id,form,ct);return RedirectToAction(nameof(Details),new{id});}catch(BidException e)when(e.Status==400){ModelState.AddModelError("",e.Message);}}Response.StatusCode=400;return View("Details",await analysis.PageAsync(User,id,ct));}
}
