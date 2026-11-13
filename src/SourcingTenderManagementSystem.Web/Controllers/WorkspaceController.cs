using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Controllers;

[Authorize(Policy = "SignedInAccount")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class WorkspaceController(IAdminDashboard dashboard, IRoleDashboard roles) : Controller
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return User.IsInRole(ApplicationRoles.Admin) ? View("Admin", await dashboard.ReadAsync(User, ct)) : View("Roles", await roles.ReadAsync(User, ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
