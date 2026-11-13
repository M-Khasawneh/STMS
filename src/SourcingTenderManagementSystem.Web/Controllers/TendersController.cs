using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.SourcingOfficer + "," + ApplicationRoles.SourcingManager + "," + ApplicationRoles.RequestingDepartment + "," + ApplicationRoles.Supplier)]
[ResponseCache(Duration=0,Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class TendersController(TenderManagement tenders) : Controller
{
    private TenderActor actor = null!;
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try { actor = await tenders.ActorAsync(User); }
        catch (TenderException exception) { context.Result = StatusCode(exception.Status); return; }
        var executed = await next();
        var sql = executed.Exception as Microsoft.Data.SqlClient.SqlException ?? (executed.Exception as Microsoft.EntityFrameworkCore.DbUpdateException)?.InnerException as Microsoft.Data.SqlClient.SqlException;
        if(sql?.Number is 2601 or 2627 or 1205)
        {
            executed.ExceptionHandled = true;
            executed.Result = Failure(new TenderException(409,"A concurrent update prevented this action. Reload before trying again."));
        }
    }
    [HttpGet] public async Task<IActionResult> Index() { ViewData["Actor"] = actor; return View(await tenders.ListAsync(actor)); }
    [HttpGet] public async Task<IActionResult> Details([FromRoute] Guid id)
    { try { return View(await tenders.DetailsAsync(id,actor)); } catch (TenderException exception) { return Failure(exception); } }
    [HttpGet] public async Task<IActionResult> Create(Guid? requisitionId)
    {
        if (!actor.Manage) return Forbid();
        var eligible = await tenders.EligibleRequestsAsync(actor);
        return View(new TenderForm { RequisitionId=requisitionId, EligibleRequests=eligible });
    }
    [HttpPost] public async Task<IActionResult> Create(TenderForm model)
    {
        if (!actor.Manage) return Forbid();
        if (ModelState.IsValid) { try { var id = await tenders.SaveAsync(null,actor,model); return Saved(id); } catch (TenderException exception) { ModelState.AddModelError("",exception.Message); if(exception.Status==409) Response.StatusCode=409; } }
        model.EligibleRequests = await tenders.EligibleRequestsAsync(actor); return View(model);
    }
    [HttpGet] public async Task<IActionResult> Edit([FromRoute] Guid id)
    {
        if (!actor.Manage) return Forbid();
        try { var d=await tenders.DetailsAsync(id,actor); if(d.Tender.Status!=TenderStatus.Draft) throw new TenderException(400,"Only Draft tender content can be edited.");
            return View(new TenderForm { Id=id, RequisitionId=d.Source.Id, Title=d.Tender.Title, Description=d.Tender.Description, Terms=d.Tender.Terms, Start=TenderTime.Local(d.Tender.SubmissionStart), Deadline=TenderTime.Local(d.Tender.SubmissionDeadline), Version=d.Version }); }
        catch (TenderException exception) { return Failure(exception); }
    }
    [HttpPost] public async Task<IActionResult> Edit([FromRoute] Guid id,TenderForm model)
    {
        if (!actor.Manage) return Forbid(); model.Id=id;
        if(ModelState.IsValid) { try { await tenders.SaveAsync(id,actor,model); return Saved(id); } catch(TenderException exception) { if(exception.Status is 403 or 404) return Failure(exception); ModelState.AddModelError("",exception.Message); if(exception.Status==409)Response.StatusCode=409; } }
        return View(model);
    }
    [HttpPost] public async Task<IActionResult> ChangeStatus([FromRoute] Guid id,string version,TenderStatus? decision)
    { if(!ModelState.IsValid||decision is null)return BadRequest(); try { await tenders.ChangeStatusAsync(id,actor,version,decision.Value); return Saved(id); } catch(TenderException exception){return Failure(exception);} }
    [HttpGet] public async Task<IActionResult> Criteria([FromRoute] Guid id)
    {
        if(!actor.Internal)return Forbid();
        try { var d=await tenders.DetailsAsync(id,actor); ViewData["Details"]=d; return View(new CriteriaForm { Version=d.Version, Items=d.Criteria.Select(x=>new CriterionForm { Id=x.Id,Name=x.Name,Description=x.Description,Kind=x.Kind,Weight=x.Weight }).ToList() }); } catch(TenderException exception){return Failure(exception);}
    }
    [HttpPost] public async Task<IActionResult> Criteria([FromRoute] Guid id,CriteriaForm model,string? command,int? removeItem)
    {
        if(!actor.Manage)return Forbid();
        try {
            var d=await tenders.DetailsAsync(id,actor); ViewData["Details"]=d;
            if(d.Criteria.Any(x=>x.ApprovedAt.HasValue)||d.Tender.Status is not(TenderStatus.Draft or TenderStatus.Published)) throw new TenderException(400,"Approved criteria are read-only.");
            if(command=="AddItem") { model.Items.Add(new()); ModelState.Clear(); return View(model); }
            if(removeItem.HasValue) { if(removeItem>=0&&removeItem<model.Items.Count)model.Items.RemoveAt(removeItem.Value); ModelState.Clear(); return View(model); }
            if(command!="Save")return BadRequest();
            if(ModelState.IsValid) { await tenders.SaveCriteriaAsync(id,actor,model); return RedirectToAction(nameof(Criteria),new{id}); }
            return View(model);
        } catch(TenderException exception){return Failure(exception);}
    }
    [HttpPost] public async Task<IActionResult> ApproveCriteria([FromRoute] Guid id,string version)
    { try { await tenders.ApproveCriteriaAsync(id,actor,version); return Saved(id); }catch(TenderException exception){return Failure(exception);} }
    [HttpGet] public async Task<IActionResult> Invitations([FromRoute] Guid id)
    { try{return View(await tenders.InvitationsAsync(id,actor));}catch(TenderException exception){return Failure(exception);} }
    [HttpPost] public async Task<IActionResult> Invite([FromRoute] Guid id,InviteForm model)
    { if(!ModelState.IsValid)return BadRequest(); try{await tenders.InviteAsync(id,actor,model);return RedirectToAction(nameof(Invitations),new{id});}catch(TenderException exception){return Failure(exception);} }
    [HttpGet] public async Task<IActionResult> Inbox()
    {try{return View(await tenders.InboxAsync(actor));}catch(TenderException exception){return Failure(exception);} }
    [HttpGet] public async Task<IActionResult> Invitation([FromRoute] Guid id)
    {try{return View(await tenders.InvitationAsync(id,actor));}catch(TenderException exception){return Failure(exception);} }
    [HttpPost] public async Task<IActionResult> Respond([FromRoute] Guid id,string version,InvitationStatus? decision)
    {if(!ModelState.IsValid||decision is null)return BadRequest();try{await tenders.RespondAsync(id,actor,version,decision.Value);return RedirectToAction(nameof(Invitation),new{id});}catch(TenderException exception){return Failure(exception);} }
    private IActionResult Saved(Guid id) {TempData["TenderStatus"]="Tender action saved.";return RedirectToAction(nameof(Details),new{id});}
    private IActionResult Failure(TenderException exception)
    {if(exception.Status==403)return Forbid();if(exception.Status==404)return NotFound();Response.StatusCode=exception.Status;return View("Problem",exception.Message);}
}
