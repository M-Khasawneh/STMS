using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = "SignedInAccount")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class WorkspaceController(IAdminDashboard dashboard) : Controller
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!User.IsInRole(ApplicationRoles.Admin)) return View();
        try { return View("Admin", await dashboard.ReadAsync(User, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
