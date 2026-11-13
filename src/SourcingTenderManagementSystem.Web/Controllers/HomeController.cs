using System.Diagnostics;
using SourcingTenderManagementSystem.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SourcingTenderManagementSystem.Web.Models;

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
    public IActionResult Contact() => View();

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
