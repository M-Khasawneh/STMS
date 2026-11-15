using System.Net;
using System.Security.Claims;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Fixture-only rows; no SQL provider, real users, recordings or external speech service.
public sealed class VoiceDashboardFixture : IRoleDashboard {
 public async Task<RoleDashboardModel> ReadAsync(ClaimsPrincipal p,CancellationToken ct){
  var original=await new TestRoleDashboard().ReadAsync(p,ct);
  var section=original.Sections.Single();
  WorkspaceMetric[] metrics=section.Title switch {
   "My requisitions"=>[new("Draft & returned",2,"Requisitions"),new("In review",1,"Requisitions"),new("Approved",4,"Requisitions")],
   "Sourcing operations"=>[new("Eligible for review",2,"Requisitions"),new("Approved requests",4,"Requisitions"),new("Open tenders",1,"Tenders")],
   "Sourcing management"=>[new("Unapproved criteria",2,"Tenders"),new("Published tenders",1,"Tenders"),new("Open tenders",4,"Tenders")],
   "Supplier workspace"=>[new("Pending invitations",2,"Tenders","Inbox"),new("Draft offers",1,"Bids"),new("Submitted offers",4,"Bids")],
   _=>[new("Available assigned tenders",2)]};
  return original with{Sections=[section with{Metrics=metrics,Records=[
   new(Guid.NewGuid(),"Network renewal", "Submitted", "Requisitions"),
   new(Guid.NewGuid(),"Finance review", "Returned", "Requisitions"),
   new(Guid.NewGuid(),"مراجعة الأجهزة", "Under review", "Requisitions")
  ]},new("My notifications","Synthetic messages addressed to this fixture account.",[new("Unread notifications",1,"Notifications")],[new(Guid.NewGuid(),"Your workflow update","Unread","Notifications","Index",DateTimeOffset.Parse("2026-11-15T12:00:00Z"))])]};
 }
}
internal static class VoiceSearchChecks {
 internal static async Task Mvc(HttpClient client){
  var checks=0;
  foreach(var actor in new[]{"admin","supplier","requester","officer","manager","evaluator","top","anonymous"}){
   client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");client.DefaultRequestHeaders.Add("X-Bid-Test-Actor",actor);
   var response=await client.GetAsync("/Workspace");var html=await response.Content.ReadAsStringAsync();
   var allowed=actor!="anonymous";
   if(html.Contains("data-dashboard-search")!=allowed||html.Contains("dashboard-search.js")!=allowed)throw new Exception("Voice search audience "+actor);checks++;
   if(allowed){if(response.StatusCode!=HttpStatusCode.OK||!html.Contains("data-work-search-form")||!html.Contains("/Work/Search")||!html.Contains("ar-JO")||!html.Contains("role=\"status\"")||response.Headers.CacheControl?.NoStore!=true)throw new Exception("Voice view data/markup "+actor);checks++;
    foreach(var path in new[]{"/Work/Tasks","/Work/Search?q=network","/Reports"}){var page=await client.GetAsync(path);var body=await page.Content.ReadAsStringAsync();if(page.StatusCode!=HttpStatusCode.OK||!body.Contains("data-work-search-toggle")||!body.Contains("data-work-search-form"))throw new Exception("Global search missing: "+actor+path);checks++;}
   }
   if(actor=="anonymous"&&response.StatusCode!=HttpStatusCode.Unauthorized)throw new Exception("Anonymous workspace allowed");
  }
  Console.WriteLine($"PASS: {checks} database-free global voice-search audience, compiled Razor and no-store checks.");
 }
}
