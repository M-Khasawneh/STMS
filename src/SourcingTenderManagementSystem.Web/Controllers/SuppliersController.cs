using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SuppliersController(ApplicationDbContext database, SupplierRegistration registration,
    SupplierReview review, IPrivateSupplierFiles files) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Register() => View(new SupplierRegistrationViewModel());

    [AllowAnonymous, HttpPost, RequestSizeLimit(17 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> Register(SupplierRegistrationViewModel model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
            ModelState.AddModelError("", "Log out before registering a separate supplier account.");
        if (ModelState.IsValid)
        {
            var result = await registration.RegisterAsync(model, cancellationToken);
            if (result.Succeeded) { TempData["SupplierRegistered"] = true; return RedirectToAction(nameof(Confirmation)); }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }
        model.Password = model.ConfirmPassword = "";
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));
        return View(model);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Confirmation() => View(); // Generic receipt; no identifying information or bearer identifier.

    [Authorize(Roles = ApplicationRoles.Supplier), HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var supplier = await database.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        if (supplier is null) return NotFound();
        return View(await DetailsAsync(supplier, cancellationToken));
    }

    [Authorize(Policy = ApplicationRoles.Admin), HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await database.SupplierProfiles.AsNoTracking().OrderBy(x => x.Status).ThenBy(x => x.CompanyName).ToListAsync(cancellationToken));

    [Authorize(Policy = ApplicationRoles.Admin), HttpGet]
    public async Task<IActionResult> Details([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var supplier = await database.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return supplier is null ? NotFound() : View(await DetailsAsync(supplier, cancellationToken));
    }

    [Authorize(Policy = ApplicationRoles.Admin), HttpPost]
    public async Task<IActionResult> Review([FromRoute] Guid id, SupplierReviewViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var error = await review.DecideAsync(id, model.ExpectedStatus!.Value, model.Decision!.Value, model.Reason,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
            if (error is null) { TempData["SupplierStatus"] = "Supplier decision saved. Account access has been updated."; return RedirectToAction(nameof(Details), new { id }); }
            ModelState.AddModelError("", error);
        }
        var supplier = await database.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null) return NotFound();
        var details = await DetailsAsync(supplier, cancellationToken);
        details.Reason = model.Reason;
        // Keep expected status from the submitted form; stale reviews must be reloaded, never silently retried.
        details.ExpectedStatus = model.ExpectedStatus;
        return View("Details", details);
    }

    [Authorize(Roles = ApplicationRoles.Admin + "," + ApplicationRoles.Supplier), HttpGet]
    public async Task<IActionResult> Document([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var query = database.SupplierDocuments.AsNoTracking().Where(x => x.Id == id);
        if (!User.IsInRole(ApplicationRoles.Admin))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(x => database.SupplierProfiles.Any(p => p.Id == x.SupplierProfileId && p.UserId == userId && p.Status == SupplierStatus.Approved));
        }
        var document = await query.SingleOrDefaultAsync(cancellationToken);
        if (document is null) return NotFound();
        try
        {
            var stream = await files.OpenAsync(document.StorageReference, cancellationToken);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
            return File(stream, document.ContentType, document.OriginalName);
        }
        catch (IOException) { return StatusCode(503, "Document is temporarily unavailable."); }
        catch (UnauthorizedAccessException) { return StatusCode(503, "Document is temporarily unavailable."); }
    }

    private async Task<SupplierReviewViewModel> DetailsAsync(SupplierProfile supplier, CancellationToken cancellationToken) => new()
    {
        Supplier = supplier, ExpectedStatus = supplier.Status,
        Documents = await database.SupplierDocuments.AsNoTracking()
        .Where(x => x.SupplierProfileId == supplier.Id)
        .OrderBy(x => x.DocumentType)
        .ToListAsync(cancellationToken)
    };
}
