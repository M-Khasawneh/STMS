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
  if(p.IsInRole(ApplicationRoles.Supplier))return original;
  var section=original.Sections.Single();
  return original with{Sections=[section with{Records=[
   new(Guid.NewGuid(),"Network renewal", "Submitted", "Requisitions"),
   new(Guid.NewGuid(),"Finance review", "Returned", "Requisitions"),
   new(Guid.NewGuid(),"مراجعة الأجهزة", "Under review", "Requisitions")
  ]}]};
 }
}
internal static class VoiceSearchChecks {
 internal static async Task Mvc(HttpClient client){
  var checks=0;
  foreach(var actor in new[]{"admin","supplier","requester","officer","manager","evaluator","top","anonymous"}){
   client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");client.DefaultRequestHeaders.Add("X-Bid-Test-Actor",actor);
   var response=await client.GetAsync("/Workspace");var html=await response.Content.ReadAsStringAsync();
   var allowed=actor is not("admin" or "supplier" or "anonymous");
   if(html.Contains("data-dashboard-search")!=allowed||html.Contains("dashboard-search.js")!=allowed)throw new Exception("Voice search audience "+actor);checks++;
   if(allowed){if(response.StatusCode!=HttpStatusCode.OK||!html.Contains("Network renewal")||!html.Contains("data-work-record")||!html.Contains("ar-JO")||!html.Contains("role=\"status\"")||response.Headers.CacheControl?.NoStore!=true)throw new Exception("Voice view data/markup "+actor);checks++;}
   if(actor=="anonymous"&&response.StatusCode!=HttpStatusCode.Unauthorized)throw new Exception("Anonymous workspace allowed");
  }
  Console.WriteLine($"PASS: {checks} database-free employee voice-search audience, compiled Razor and no-store checks.");
 }
}
