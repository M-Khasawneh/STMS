using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Models.Employees;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = ApplicationRoles.Admin)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class EmployeesController(EmployeeManagement employees) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? q = null)
    {
        var rows = await employees.ListAsync(page, q, HttpContext.RequestAborted);
        ViewData["Pagination"] = (rows as SourcingTenderManagementSystem.Web.Models.IPagedResults)?.Info;
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Create() => View(new EmployeeCreateViewModel { Departments = await employees.DepartmentsAsync() });

    [HttpPost]
    public async Task<IActionResult> Create(EmployeeCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await employees.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["EmployeeStatus"] = "Employee account created.";
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        model.Password = model.ConfirmPassword = string.Empty;
        ModelState.SetModelValue(nameof(model.Password), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(model.ConfirmPassword), string.Empty, string.Empty);
        model.Departments = await employees.DepartmentsAsync();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var employee = await employees.GetAsync(id);
        if (employee is null) return NotFound();
        return View(await EditModelAsync(employee));
    }

    [HttpPost]
    public async Task<IActionResult> Edit([FromRoute] string id, EmployeeEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        model.Employee = await employees.GetAsync(id);
        if (model.Employee is null) return NotFound();
        model.Departments = await employees.DepartmentsAsync();
        if (!model.UpdateRoles)
        {
            model.Roles = model.Employee.Roles.ToArray();
            ModelState.Remove(nameof(model.Roles));
        }
        if (ModelState.IsValid)
        {
            var result = await employees.EditAsync(id, model);
            if (result.Succeeded)
            {
                TempData["EmployeeStatus"] = "Employee account updated. Existing sessions must sign in again.";
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SetEnabled([FromRoute] string id, bool? enabled)
    {
        if (!ModelState.IsValid || enabled is null) return BadRequest();
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var employee = await employees.GetAsync(id);
        if (employee is null) return NotFound();
        var result = await employees.SetEnabledAsync(id, enabled.Value);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View("Edit", await EditModelAsync(employee));
        }
        TempData["EmployeeStatus"] = enabled.Value ? "Employee enabled." : "Employee disabled. Existing sessions have been revoked.";
        return RedirectToAction(nameof(Index));
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }

    private async Task<EmployeeEditViewModel> EditModelAsync(EmployeeDetails employee) => new()
    {
        Employee = employee, Email = employee.Email, PhoneNumber = employee.PhoneNumber,
        Roles = employee.Roles.ToArray(), DepartmentId = employee.DepartmentId,
        Departments = await employees.DepartmentsAsync()
    };
}
