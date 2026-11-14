using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public sealed class TestRoleDashboard : IRoleDashboard
{
    public Task<RoleDashboardModel> ReadAsync(ClaimsPrincipal p, CancellationToken ct)
    {
        if (p.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException();
        var role = p.FindFirstValue(ClaimTypes.Role) ?? "";
        var title = role switch { ApplicationRoles.Supplier => "Supplier workspace", ApplicationRoles.RequestingDepartment => "My requisitions", ApplicationRoles.SourcingOfficer => "Sourcing operations", ApplicationRoles.SourcingManager => "Sourcing management", ApplicationRoles.EvaluationCommittee => "Evaluation committee", ApplicationRoles.TopCommittee => "Top committee", _ => "My workspace" };
        return Task.FromResult(new RoleDashboardModel(true, p.Identity.Name ?? "", [role], [new(title, "Isolated test fixture; no production data.", [new("Available records", 0)], [])]));
    }
}
public sealed class MemoryProfileTransactions(IEmployeeData employees) : IProfileTransactions
{
    public Task<IEmployeeTransaction> BeginAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return employees.BeginAsync(); }
}
public static class ExperienceChecks
{
    public static void QueryChecks()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var service = new RoleDashboard(db, null!, null!, TimeProvider.System, NullLogger<RoleDashboard>.Instance);
        var own = service.OwnRequests("owner-only").ToQueryString();
        if (!own.Contains("CreatedByUserId") || !own.Contains("owner-only")) throw new Exception("Missing ownership predicate");
        var review = service.ReviewQueue("reviewer-only", Guid.NewGuid()).ToQueryString();
        if (!review.Contains("CreatedByUserId] <>") || !review.Contains("DepartmentId] <>") || !review.Contains("UnderReview")) throw new Exception("Missing review exclusions");
        var offers = service.OwnOffers(Guid.NewGuid()).ToQueryString();
        if (!offers.Contains("SupplierProfileId") || !offers.Contains("TenderSupplierId") || !offers.Contains("TenderId")) throw new Exception("Missing supplier scope");
        foreach (var kind in new[] { CommitteeKind.Evaluation, CommitteeKind.TopCommittee }) {
            var sql = service.AssignedTenders("member-only", kind, DateTimeOffset.UtcNow).ToQueryString();
            if (!sql.Contains("CommitteeMembers") || !sql.Contains("CommitteeAssignments") || !sql.Contains("EffectiveTo") || !sql.Contains("TenderId") || !sql.Contains("Status")) throw new Exception("Missing tender/stage/effective assignment guard");
        }
        Console.WriteLine("PASS: role dashboard ownership, tender-stage and committee SQL scopes translate without opening a connection.");
    }
    public static async Task ProfileAsync(HttpClient client, Func<HttpClient> anotherClient, MemoryUsers memory, string currentPassword)
    {
        var checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
        async Task<string> Token(HttpClient browser, string path) {
            var response = await browser.GetAsync(path); if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString == "/Workspace") response = await browser.GetAsync("/Workspace");
            var html = await response.Content.ReadAsStringAsync(); var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            if (!match.Success) throw new Exception("Missing token"); return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        async Task Login(HttpClient browser, string password) {
            var response = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["UserName"]="employee",["Password"]=password,["__RequestVerificationToken"]=await Token(browser,"/Account/Login") }));
            Check(response.StatusCode == HttpStatusCode.Redirect,"Identity login succeeds for temporary account");
        }
        Check((await client.GetAsync("/Profile")).StatusCode == HttpStatusCode.Redirect, "anonymous profile denied");
        await Login(client, currentPassword); using var other = anotherClient(); await Login(other, currentPassword);
        var employee = memory.Users.Values.Single(x => x.UserName == "employee"); var admin = memory.Users.Values.Single(x => x.UserName == "admin");
        var adminHash = admin.PasswordHash; var adminEmail = admin.Email; var roles = memory.Roles[employee.Id].ToArray();
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==");
        Check((await client.PostAsync("/Profile/Picture", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "picture upload requires antiforgery");
        using (var upload = new MultipartFormDataContent()) {
            upload.Add(new StringContent(await Token(client, "/Profile")), "__RequestVerificationToken");
            upload.Add(new ByteArrayContent(png), "picture", "avatar.png");
            Check((await client.PostAsync("/Profile/Picture", upload)).StatusCode == HttpStatusCode.Redirect, "authenticated picture upload succeeds");
        }
        var picture = await client.GetAsync("/Profile/Avatar");
        Check(picture.StatusCode == HttpStatusCode.OK && (await picture.Content.ReadAsByteArrayAsync()).SequenceEqual(png) && picture.Content.Headers.ContentType?.MediaType == "image/png", "own protected picture returns verified image bytes");
        using (var separate = anotherClient()) {
            var login = new Dictionary<string, string> { ["UserName"] = "admin", ["Password"] = currentPassword, ["__RequestVerificationToken"] = await Token(separate, "/Account/Login") };
            await separate.PostAsync("/Account/Login", new FormUrlEncodedContent(login));
            Check((await separate.GetAsync("/Profile/Avatar?userId=" + employee.Id)).StatusCode == HttpStatusCode.NotFound, "another employee cannot select someone else's picture");
        }
        Check((await client.PostAsync("/Profile/RemovePicture", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token(client, "/Profile") }))).StatusCode == HttpStatusCode.Redirect, "picture removal requires authenticated POST");
        Check((await client.GetAsync("/Profile/Avatar")).StatusCode == HttpStatusCode.NotFound, "removed picture is unavailable");
        var email = "self@example.test";
        Check((await client.PostAsync("/Profile/Password",new FormUrlEncodedContent([]))).StatusCode==HttpStatusCode.BadRequest,"password change requires antiforgery");
        var contact = new Dictionary<string,string> { ["Contact.Email"]=email,["Contact.PhoneNumber"]="+962790000000",["UserId"]=admin.Id,["Roles[0]"]=ApplicationRoles.Admin };
        Check((await client.PostAsync("/Profile/Contact",new FormUrlEncodedContent(contact))).StatusCode == HttpStatusCode.BadRequest,"contact change requires antiforgery");
        contact["__RequestVerificationToken"]=await Token(client,"/Profile");
        Check((await client.PostAsync("/Profile/Contact",new FormUrlEncodedContent(contact))).StatusCode == HttpStatusCode.Redirect,"self contact update succeeds");
        Check(memory.Users[employee.Id].Email==email&&memory.Users[admin.Id].Email==adminEmail&&memory.Roles[employee.Id].SetEquals(roles),"forged user/role fields cannot change another account or permissions");
        Check((await client.GetAsync("/Profile")).StatusCode==HttpStatusCode.OK,"current cookie refreshed after contact change");
        Check((await other.GetAsync("/Profile")).StatusCode==HttpStatusCode.Redirect,"previous session revoked after contact change");
        async Task<HttpResponseMessage> Password(string old,string next,string confirm) => await client.PostAsync("/Profile/Password",new FormUrlEncodedContent(new Dictionary<string,string> { ["Password.CurrentPassword"]=old,["Password.NewPassword"]=next,["Password.ConfirmPassword"]=confirm,["__RequestVerificationToken"]=await Token(client,"/Profile") }));
        var nextPassword="Aa1!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var wrong=await Password("wrong-current",nextPassword,nextPassword); var wrongHtml=await wrong.Content.ReadAsStringAsync();
        Check(wrong.StatusCode==HttpStatusCode.OK&&wrongHtml.Contains("Incorrect password")&&!wrongHtml.Contains(nextPassword)&&!wrongHtml.Contains("value=\"wrong-current"),"wrong current password rejected and secrets never echoed");
        var before=memory.Users[employee.Id].PasswordHash;
        await Password(currentPassword,"weak","weak");Check(memory.Users[employee.Id].PasswordHash==before,"Identity password policy enforced");
        await Password(currentPassword,nextPassword,nextPassword+"x");Check(memory.Users[employee.Id].PasswordHash==before,"mismatched confirmation cannot change password");
        Check((await Password(currentPassword,nextPassword,nextPassword)).StatusCode==HttpStatusCode.Redirect,"valid password change succeeds through Identity");
        Check((await client.GetAsync("/Profile")).StatusCode==HttpStatusCode.OK,"current session survives password change");
        Check(memory.Users[admin.Id].PasswordHash==adminHash&&memory.Users[employee.Id].UserName=="employee"&&memory.Roles[employee.Id].SetEquals(roles),"password operation preserves other users, login name and roles");
        using var fresh=anotherClient();await Login(fresh,nextPassword);
        Console.WriteLine($"PASS: {checks} profile HTTPS/Identity checks; memory stores only, no SQL writes.");
    }
}
