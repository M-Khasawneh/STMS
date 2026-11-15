using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Supplier)]
[AutoValidateAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class BidsController(SupplierBidPages pages, BidManagement bids, BidDocumentManagement documents) : Controller
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try { await pages.RequireSupplierAsync(User, HttpContext.RequestAborted); }
        catch (BidException e) { context.Result = Failure(e); return; }
        var executed = await next();
        if (executed.Exception is BidException error)
        {
            executed.ExceptionHandled = true;
            executed.Result = Failure(error);
        }
    }
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await pages.ListAsync(User, ct));
    [HttpGet] public async Task<IActionResult> Details([FromRoute] Guid id, CancellationToken ct) => View(await pages.DetailsAsync(User, id, ct));
    [HttpGet] public async Task<IActionResult> Create(Guid tenderId, CancellationToken ct) => View("Editor", await pages.CreateAsync(User, tenderId, null, ct));
    [HttpPost] public async Task<IActionResult> Create(Guid tenderId, [Bind(Prefix = "Form")] BidDraftForm form, string? command, int? removeCharge, CancellationToken ct)
    {
        if (command == "AddCharge" || removeCharge.HasValue)
            return ChargeEditor(await pages.CreateAsync(User, tenderId, form, ct), command, removeCharge);
        if (command != "Save") return BadRequest();
        if (ModelState.IsValid)
        {
            try { var id = await bids.CreateDraftAsync(User, tenderId, form, ct); return Saved(id, "Draft saved. Add your documents before final submission.", "Edit"); }
            catch (BidException e) when (e.Status == 400) { ModelState.AddModelError("", e.Message); }
        }
        return View("Editor", await pages.CreateAsync(User, tenderId, form, ct));
    }
    [HttpGet] public async Task<IActionResult> Edit([FromRoute] Guid id, CancellationToken ct) => View("Editor", await pages.EditAsync(User, id, null, ct));
    [HttpPost] public async Task<IActionResult> Edit([FromRoute] Guid id, [Bind(Prefix = "Form")] BidDraftForm form, string? command, int? removeCharge, CancellationToken ct)
    {
        if (command == "AddCharge" || removeCharge.HasValue)
            return ChargeEditor(await pages.EditAsync(User, id, form, ct), command, removeCharge);
        if (command != "Save") return BadRequest();
        if (ModelState.IsValid)
        {
            try { await bids.SaveDraftAsync(User, id, form, ct); return Saved(id, "Draft saved.", "Edit"); }
            catch (BidException e) when (e.Status == 400) { ModelState.AddModelError("", e.Message); }
        }
        return View("Editor", await pages.EditAsync(User, id, form, ct));
    }
    private IActionResult ChargeEditor(SupplierBidEditor editor, string? command, int? remove)
    {
        if (remove.HasValue)
        {
            if (remove.Value < 0 || remove.Value >= editor.Form.Charges.Count) return BadRequest();
            editor.Form.Charges.RemoveAt(remove.Value);
        }
        else if (command == "AddCharge") editor.Form.Charges.Add(new());
        ModelState.Clear(); // Reindex the non-persisted optional rows after an explicit add/remove.
        return View("Editor", editor);
    }
    [HttpGet] public async Task<IActionResult> Submit([FromRoute] Guid id, CancellationToken ct) => View(await pages.ConfirmationAsync(User, id, ct));
    [HttpPost] public async Task<IActionResult> Submit([FromRoute] Guid id, BidSubmitForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(await pages.ConfirmationAsync(User, id, ct));
        await bids.SubmitAsync(User, id, form.Version, ct);
        return Saved(id, "Your offer was submitted. Its content and documents are now read-only.");
    }
    [HttpPost, RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromRoute] Guid id, BidDocumentForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Failure(new(400, "Choose a document type and a PDF, JPG or PNG file up to 5 MB. Reload the draft before retrying."));
        await documents.UploadAsync(User, id, form.Version, form.DocumentType, form.File!, ct);
        return Saved(id, "Document uploaded.", "Edit");
    }
    [HttpPost, RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Replace([FromRoute] Guid id, Guid documentId, BidDocumentForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Failure(new(400, "Choose a replacement document type and a PDF, JPG or PNG file up to 5 MB. Reload the draft before retrying."));
        await documents.ReplaceAsync(User, id, documentId, form.Version, form.DocumentType, form.File!, ct);
        return Saved(id, "Document replaced.", "Edit");
    }
    [HttpPost] public async Task<IActionResult> RemoveDocument([FromRoute] Guid id, Guid documentId, string version, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        await documents.DeleteAsync(User, id, documentId, version, ct);
        return Saved(id, "Document removed.", "Edit");
    }
    [HttpGet] public async Task<IActionResult> Document([FromRoute] Guid id, Guid documentId, CancellationToken ct)
    {
        var download = await documents.DownloadAsync(User, id, documentId, ct);
        try
        {
            foreach (var header in download.Headers) Response.Headers[header.Key] = header.Value;
            // FileStreamResult owns/disposes the verified stream after writing the attachment.
            return File(download.Content, download.ContentType, download.OriginalName, enableRangeProcessing: false);
        }
        catch { await download.DisposeAsync(); throw; }
    }
    [HttpGet] public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        View(await bids.HistoryAsync(User, id, ct));
    [HttpGet] public async Task<IActionResult> Summary([FromRoute] Guid id, CancellationToken ct)
    {
        var original = await pages.SummaryAsync(User, id, ct);
        var stamp = AccessStamp.Create(User, HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
        var bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(original) + "\n\nPersonal copy: " + stamp.Label + "\n");
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        return File(bytes, "text/plain; charset=utf-8", $"STMS-offer-{id:N}.txt");
    }
    private IActionResult Saved(Guid id, string message, string action = "Details")
    { TempData["BidStatus"] = message; return RedirectToAction(action, new { id }); }
    private IActionResult Failure(BidException e)
    {
        if (e.Status == 403) return Forbid();
        if (e.Status == 404) return NotFound();
        Response.StatusCode = e.Status; return View("Problem", e.Message);
    }
}
