using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.TopCommittee;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize,AutoValidateAntiforgeryToken,ResponseCache(Duration=0,NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class TopCommitteeController(ITopCommitteeManagement committee):Controller {
 private const string Readers=ApplicationRoles.SourcingManager+","+ApplicationRoles.SourcingOfficer+","+ApplicationRoles.TopCommittee;
 public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next){var executed=await next();if(executed.Exception is not BidException error)return;executed.ExceptionHandled=true;if(error.Status==403)executed.Result=Forbid();else if(error.Status==404)executed.Result=NotFound();else{Response.StatusCode=error.Status;executed.Result=View("Problem",error.Message);}}
 [HttpGet,Authorize(Roles=Readers)] public async Task<IActionResult> Index(CancellationToken ct)=>View(await committee.ListAsync(User,ct));
 [HttpGet,Authorize(Roles=Readers)] public async Task<IActionResult> Details(Guid id,CancellationToken ct)=>View(await committee.PageAsync(User,id,ct));
 [HttpGet] public async Task<IActionResult> Outcome(Guid id,CancellationToken ct)=>View(await committee.OutcomeAsync(User,id,ct));
 [HttpPost,Authorize(Roles=ApplicationRoles.SourcingManager)] public async Task<IActionResult> Assign(Guid id,TopMembershipForm form,CancellationToken ct){if(!ModelState.IsValid)return await Invalid(id,ct);try{await committee.AssignAsync(User,id,form,ct);}catch(BidException e)when(e.Status==400){ModelState.AddModelError("",e.Message);return await Invalid(id,ct);}return Done(id,"Committee membership saved.");}
 [HttpPost,Authorize(Roles=ApplicationRoles.SourcingManager)] public async Task<IActionResult> Open(Guid id,TopActionForm form,CancellationToken ct){if(!ModelState.IsValid)return await Invalid(id,ct);try{await committee.OpenAsync(User,id,form,ct);}catch(BidException e)when(e.Status==400){ModelState.AddModelError("",e.Message);return await Invalid(id,ct);}return Done(id,"Voting round opened.");}
 [HttpPost,Authorize(Roles=ApplicationRoles.TopCommittee)] public async Task<IActionResult> Vote(Guid id,TopActionForm form,CancellationToken ct){if(!ModelState.IsValid)return await Invalid(id,ct);try{await committee.VoteAsync(User,id,form,ct);}catch(BidException e)when(e.Status==400){ModelState.AddModelError("",e.Message);return await Invalid(id,ct);}TempData["TopMessage"]="Your final vote was recorded. Review the tenders currently assigned to you for the next stage.";return RedirectToAction(nameof(Index));}
 [HttpPost,Authorize(Roles=ApplicationRoles.SourcingOfficer)] public async Task<IActionResult> Award(Guid id,TopActionForm form,CancellationToken ct){if(!ModelState.IsValid)return await Invalid(id,ct);try{await committee.AwardAsync(User,id,form,ct);}catch(BidException e)when(e.Status==400){ModelState.AddModelError("",e.Message);return await Invalid(id,ct);}return Done(id,"Final award recorded. In-app outcome notifications were saved.");}
 private IActionResult Done(Guid id,string message){TempData["TopMessage"]=message;return RedirectToAction(nameof(Details),new{id});}
 private async Task<IActionResult> Invalid(Guid id,CancellationToken ct){Response.StatusCode=400;return View("Details",await committee.PageAsync(User,id,ct));}
}
