using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class WorkspaceUiChecks
{
    public static async Task RunAsync(HttpClient client)
    {
        var checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
        foreach (var actor in new[] { "admin", "supplier", "requester", "officer", "manager", "evaluator", "top" })
        {
            client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor);
            var home = await client.GetAsync("/");
            Check(home.StatusCode == HttpStatusCode.Redirect && home.Headers.Location?.OriginalString == "/Workspace", actor + " cannot render public Home while signed in");
            var dashboard = await client.GetAsync("/Workspace"); var html = await dashboard.Content.ReadAsStringAsync();
            Check(dashboard.StatusCode == HttpStatusCode.OK && html.Contains(actor == "admin" ? "Administration overview" : "My dashboard") && !html.Contains("stms-landing") && !html.Contains("Unified Platform for"), actor + " gets internal dashboard only");
            Check(html.Contains("workspace-ui.js") && html.Contains("workspace-ui.css") && html.Contains("Dashboard</a>"), actor + " has shared dialogs and Dashboard navigation");
            Check(html.Contains("href=\"/Departments\"") == (actor == "admin"), "department links match role " + actor);
            Check(html.Contains("href=\"/Bids\"") == (actor == "supplier"), "My Offers links match role " + actor);
            var login = await client.GetAsync("/Account/Login");
            Check(login.StatusCode == HttpStatusCode.Redirect && login.Headers.Location?.OriginalString == "/Workspace", "signed-in Login route returns " + actor + " to dashboard");
        }
        client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", "anonymous");
        Check((await client.GetAsync("/Workspace")).StatusCode == HttpStatusCode.Unauthorized, "anonymous dashboard access denied");
        var publicPage = await client.GetStringAsync("/");
        Check(publicPage.Contains("stms-landing") && !publicPage.Contains("workspace-ui.js"), "anonymous homepage preserved without internal dialogs");
        var statistics = new CountStatistics();
        var controller = new HomeController(statistics) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "memory"));
        Check(await controller.Index(default) is RedirectToActionResult { ControllerName: "Workspace" } && statistics.Reads == 0, "signed-in Home redirects before public statistics queries");
        Console.WriteLine($"PASS: {checks} database-free workspace routing/role checks.");
    }
    private sealed class CountStatistics : IPublicStatistics
    {
        public int Reads;
        public Task<PublicHomeViewModel> ReadAsync(CancellationToken cancellationToken) { Reads++; return Task.FromResult(new PublicHomeViewModel(null, null, null)); }
    }
}
