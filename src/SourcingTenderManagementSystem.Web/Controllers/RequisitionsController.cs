using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.RequestingDepartment + "," + ApplicationRoles.SourcingOfficer)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class RequisitionsController(RequisitionManagement requisitions) : Controller
{
    private RequisitionActor actor = null!;
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try { actor = await requisitions.ActorAsync(User); }
        catch (RequisitionException exception) { context.Result = StatusCode(exception.Status); return; }
        await next();
    }
    [HttpGet]
    public async Task<IActionResult> Index() => View(await requisitions.ListAsync(actor));
    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] Guid id)
    {
        try { return View(await requisitions.DetailsAsync(id, actor)); }
        catch (RequisitionException exception) { return StatusCode(exception.Status); }
    }
    [HttpGet] public async Task<IActionResult> Qr(Guid id, [FromServices] QrLinks codes) {
        try { _ = await requisitions.DetailsAsync(id,actor); var bytes=codes.Png("Requisitions",id); _ = await requisitions.DetailsAsync(id,actor); Response.Headers["X-Content-Type-Options"]="nosniff"; return File(bytes,"image/png",$"STMS-Requisitions-{id:N}-QR.png"); }
        catch (RequisitionException e) { return StatusCode(e.Status); }
        catch (BidException e) { return StatusCode(e.Status,e.Message); }
    }
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!actor.CanCreate) return Forbid();
        return View(await PrepareAsync(new RequisitionForm { Items = [new()] }));
    }
    [HttpPost]
    public async Task<IActionResult> Create(RequisitionForm model, string? command, int? removeItem)
    {
        if (!actor.CanCreate) return Forbid();
        if (AdjustItems(model, command, removeItem)) return View(await PrepareAsync(model));
        if (command is not ("SaveDraft" or "Submit")) return BadRequest();
        if (ModelState.IsValid)
        {
            try { var id = await requisitions.SaveAsync(null, actor, model, command == "Submit"); return Saved(id); }
            catch (RequisitionException exception) { AddError(exception); }
        }
        return View(await PrepareAsync(model));
    }
    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] Guid id)
    {
        try
        {
            var details = await requisitions.DetailsAsync(id, actor);
            if (!actor.CanEdit(details.Request)) return Forbid();
            var request = details.Request;
            return View(await PrepareAsync(new RequisitionForm { Id = id, DepartmentName = details.DepartmentName, Version = details.Version, Title = request.Title, Reason = request.Reason, Priority = request.Priority, TargetDate = request.TargetDate,
                Items = details.Items.Select(x => new RequisitionItemForm { Id = x.Id, Description = x.Description, Quantity = x.Quantity, Specifications = x.Specifications }).ToList() }));
        }
        catch (RequisitionException exception) { return StatusCode(exception.Status); }
    }
    [HttpPost]
    public async Task<IActionResult> Edit([FromRoute] Guid id, RequisitionForm model, string? command, int? removeItem)
    {
        model.Id = id;
        try
        {
            var details = await requisitions.DetailsAsync(id, actor);
            model.DepartmentName = details.DepartmentName;
            if (!actor.CanEdit(details.Request)) return Forbid();
            if (AdjustItems(model, command, removeItem)) return View(await PrepareAsync(model));
            if (command is not ("SaveDraft" or "Submit")) return BadRequest();
            if (ModelState.IsValid) { await requisitions.SaveAsync(id, actor, model, command == "Submit"); return Saved(id); }
        }
        catch (RequisitionException exception)
        {
            if (exception.Status is 403 or 404) return StatusCode(exception.Status);
            AddError(exception);
        }
        return View(await PrepareAsync(model));
    }
    [HttpPost]
    public async Task<IActionResult> Submit([FromRoute] Guid id, string? version)
    {
        try { await requisitions.SubmitAsync(id, actor, version); return Saved(id); }
        catch (RequisitionException exception) { return await ActionError(id, exception); }
    }
    [HttpPost]
    public async Task<IActionResult> Review([FromRoute] Guid id, RequisitionActionForm model)
    {
        try
        {
            if (!ModelState.IsValid) throw new RequisitionException(400, "Select a valid review action and reload if the form is outdated.");
            await requisitions.ReviewAsync(id, actor, model); return Saved(id);
        }
        catch (RequisitionException exception) { return await ActionError(id, exception); }
    }
    private async Task<IActionResult> ActionError(Guid id, RequisitionException exception)
    {
        if (exception.Status is 403 or 404) return StatusCode(exception.Status);
        AddError(exception);
        // Retain the original version after an error; never silently refresh a stale review.
        try
        {
            var details = await requisitions.DetailsAsync(id, actor);
            return View("Details", details with { Version = ModelState["Version"]?.AttemptedValue ?? "" });
        }
        catch (RequisitionException missing) { return StatusCode(missing.Status); }
    }
    private void AddError(RequisitionException exception)
    {
        ModelState.AddModelError("", exception.Message);
        if (exception.Status == 409) Response.StatusCode = 409;
    }
    private IActionResult Saved(Guid id)
    {
        TempData["RequisitionStatus"] = "Requisition saved. Its current status and history are shown below.";
        return RedirectToAction(nameof(Details), new { id });
    }
    private async Task<RequisitionForm> PrepareAsync(RequisitionForm model)
    {
        model.Today = requisitions.Today;
        if (string.IsNullOrEmpty(model.DepartmentName)) model.DepartmentName = await requisitions.DepartmentNameAsync(actor.DepartmentId);
        return model;
    }
    private bool AdjustItems(RequisitionForm model, string? command, int? removeItem)
    {
        if (command == "AddItem") { model.Items.Add(new()); ModelState.Clear(); return true; }
        if (removeItem.HasValue)
        {
            if (removeItem >= 0 && removeItem < model.Items.Count) model.Items.RemoveAt(removeItem.Value);
            ModelState.Clear(); return true;
        }
        return false;
    }
}
