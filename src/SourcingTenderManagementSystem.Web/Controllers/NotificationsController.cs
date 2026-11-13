using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
namespace SourcingTenderManagementSystem.Web.Controllers;
[Authorize(Policy="SignedInAccount")]
[ResponseCache(Duration=0,Location=ResponseCacheLocation.None,NoStore=true)]
public sealed class NotificationsController(ApplicationDbContext db) : Controller
{
    [HttpGet] public async Task<IActionResult> Index() => View(await db.Notifications.AsNoTracking().Where(x=>x.RecipientUserId==User.FindFirstValue(ClaimTypes.NameIdentifier)).OrderByDescending(x=>x.CreatedAt).ToListAsync());
}
