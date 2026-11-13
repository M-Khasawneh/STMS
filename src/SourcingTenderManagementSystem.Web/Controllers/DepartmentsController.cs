using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Departments;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = ApplicationRoles.Admin)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class DepartmentsController(IDepartmentStore departments) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await departments.ListAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new DepartmentFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(DepartmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        await departments.CreateAsync(model.Name, cancellationToken);
        TempData["DepartmentStatus"] = "Department created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return NotFound();
        var department = await departments.FindAsync(id, cancellationToken);
        if (department is null) return NotFound();
        ViewData["DepartmentId"] = id;
        return View(new DepartmentFormViewModel { Name = department.Name });
    }

    [HttpPost]
    public async Task<IActionResult> Edit([FromRoute] Guid id, DepartmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return NotFound();
        if (await departments.FindAsync(id, cancellationToken) is null) return NotFound();
        ViewData["DepartmentId"] = id;
        if (!ModelState.IsValid) return View(model);
        if (!await departments.RenameAsync(id, model.Name, cancellationToken)) return NotFound();
        TempData["DepartmentStatus"] = "Department updated.";
        return RedirectToAction(nameof(Index));
    }
}
