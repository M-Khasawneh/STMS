using System.Diagnostics;
using SourcingTenderManagementSystem.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SourcingTenderManagementSystem.Web.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace SourcingTenderManagementSystem.Web.Controllers;

public class HomeController(IPublicStatistics statistics) : Controller
{
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Workspace");
        return View(await statistics.ReadAsync(cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult About() => View();

    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Contact() => View(new ContactMessageForm());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("public-contact")]
    [RequestSizeLimit(64 * 1024)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Contact([Bind("SenderName,Email,Subject,Message")] ContactMessageForm form,
        [FromServices] ContactMessages messages, [FromServices] ILogger<HomeController> logger, CancellationToken ct)
    {
        if (!ModelState.IsValid) { Response.StatusCode = 400; return View(form); }
        try {
            if (await messages.SendAsync(form, ct)) {
                TempData["ContactSent"] = "Your message has been sent to the STMS administrator.";
                return RedirectToAction(nameof(Contact));
            }
        }
        catch (Exception error) when (error is Microsoft.EntityFrameworkCore.DbUpdateException or System.Data.Common.DbException or TimeoutException) {
            logger.LogWarning("Public contact delivery is temporarily unavailable.");
        }
        Response.StatusCode = 503;
        ModelState.AddModelError(string.Empty, "Your message could not be delivered. Please try again later.");
        return View(form);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
