using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.SourcingManager)]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommitteesController(CommitteeManagement committees) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        if (result.Exception is not BidException error) return;
        result.ExceptionHandled = true;
        if (error.Status == 403) result.Result = Forbid();
        else if (error.Status == 404) result.Result = NotFound();
        else {
            Response.StatusCode = error.Status;
            result.Result = View("Problem", error.Message);
        }
    }
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct) => View(await committees.PageAsync(User, id, ct));
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, [Bind(Prefix = "Form")] CommitteeForm form, CancellationToken ct)
    {
        if (ModelState.IsValid) {
            try {
                await committees.SaveAsync(User, id, form, ct);
                TempData["CommitteeMessage"] = "Evaluation committee membership saved.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (BidException error) when (error.Status == 400) { ModelState.AddModelError(string.Empty, error.Message); }
        }
        Response.StatusCode = 400;
        return View(await committees.PageAsync(User, id, ct));
    }
}
