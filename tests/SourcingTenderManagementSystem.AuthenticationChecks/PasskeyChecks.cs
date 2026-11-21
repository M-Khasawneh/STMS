using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
public sealed class PasskeyFixtureEligibility:IEmployeeEligibility {
 public HashSet<string> Denied=[];
 public Task<bool> IsEligibleAsync(IdentityUser user)=>Task.FromResult(!Denied.Contains(user.Id));
}
// Test assembly only; never registered by the production web application.
[AllowAnonymous]
public sealed class PasskeyFixtureController(MemoryUsers users,PasskeyFixtureEligibility eligibility):Controller {
 [HttpPost]public IActionResult State(string name,string mode){
  if(Request.Headers["X-STMS-Fixture"]!="memory-only")return NotFound();var user=users.Users.Values.Single(u=>u.UserName==name);
  if(mode=="disabled")user.LockoutEnd=DateTimeOffset.UtcNow.AddHours(1);
  else if(mode=="ineligible")eligibility.Denied.Add(user.Id);
  else if(mode=="stamp")user.SecurityStamp=Guid.NewGuid().ToString();
  else if(mode=="enabled"){user.LockoutEnd=null;eligibility.Denied.Remove(user.Id);}
  return NoContent();
 }
}
internal static class PasskeyChecks {
 internal const string Password="Fixture-Only!9427";
 internal static async Task Run(bool browser, bool faceChecks=false){
  var clock=TimeProvider.System;var tickets=new PasskeyCeremonies(clock);int count=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);count++;}
  var token=tickets.Issue(new("login","challenge",null,null,null,clock.GetUtcNow().AddMinutes(1)));Check(tickets.Take(token)!=null&&tickets.Take(token)==null,"one-use ticket");
  Check(tickets.Take(tickets.Issue(new("login","challenge",null,null,null,clock.GetUtcNow().AddMinutes(-1))))==null,"expired ticket");
  token=tickets.Issue(new("login","challenge",null,null,null,clock.GetUtcNow().AddMinutes(1)));Check((await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>tickets.Take(token)!=null)))).Count(x=>x)==1,"atomic replay prevention");
  var site=new PasskeySite("https://localhost:7162");Check(site.ValidOrigin("https://localhost:7162")&&!site.ValidOrigin("https://evil.localhost:7162")&&!site.ValidOrigin("http://localhost:7162")&&!site.ValidOrigin("https://localhost:7163"),"exact origin scope");
  using var key=RSA.Create(2048);var certificateRequest=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var generated=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null);
  var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();var url="https://localhost:"+port;
  var source=Path.GetFullPath("src/SourcingTenderManagementSystem.Web");var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(AccountController).Assembly.GetName().Name,EnvironmentName="Development",ContentRootPath=source,WebRootPath=Path.Combine(source,"wwwroot")});builder.Logging.ClearProviders();
  builder.WebHost.ConfigureKestrel(o=>o.Listen(IPAddress.Loopback,port,l=>l.UseHttps(certificate)));builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
  builder.Services.AddControllersWithViews().AddApplicationPart(typeof(PasskeyFixtureController).Assembly);var memory=new MemoryUsers();builder.Services.AddSingleton(memory);builder.Services.AddSingleton<IUserStore<IdentityUser>>(memory);builder.Services.AddSingleton<IRoleStore<IdentityRole>,MemoryRoles>();
  builder.Services.AddIdentity<IdentityUser,IdentityRole>().AddDefaultTokenProviders();builder.Services.AddAuthenticationFoundation();builder.Services.AddStmsPasskeys(new PasskeySite(url));builder.Services.AddSingleton<TimeProvider>(clock);
  builder.Services.RemoveAll<IEmployeeEligibility>();builder.Services.AddSingleton<PasskeyFixtureEligibility>();builder.Services.AddSingleton<IEmployeeEligibility>(sp=>sp.GetRequiredService<PasskeyFixtureEligibility>());
  builder.Services.AddStmsFaceRecognition(builder.Configuration);
  builder.Services.Configure<FaceOptions>(options=>options.Enabled=faceChecks);
  builder.Services.RemoveAll<IFaceCredentialStore>();builder.Services.AddSingleton<MemoryFaceCredentials>();builder.Services.AddSingleton<IFaceCredentialStore>(sp=>sp.GetRequiredService<MemoryFaceCredentials>());
  // This long deterministic flow tests application rules, not elapsed wall-clock throttling.
  if(faceChecks){builder.Services.RemoveAll<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>();builder.Services.AddRateLimiter(options=>{foreach(var policy in new[]{"passkey","face-settings","face-login","public-contact"})options.AddPolicy(policy,_=>System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("memory-fixture"));});}
  builder.Services.AddSingleton<IRoleDashboard,TestRoleDashboard>();builder.Services.AddSingleton<IAdminDashboard>(sp=>new TestAdminDashboard(sp.GetRequiredService<IHttpContextAccessor>(),clock));
BrowserViewCoverage.Register(builder.Services);
  await using var app=builder.Build();app.Use(async(context,next)=>{context.Response.Headers["X-STMS-Passkey-Fixture"]="memory-only";await next();});app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
  using(var scope=app.Services.CreateScope()){var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();await roles.CreateAsync(new(ApplicationRoles.SourcingOfficer));var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();foreach(var name in new[]{"passkey-user","other-passkey-user"}){var user=new IdentityUser(name){LockoutEnabled=true};var result=await users.CreateAsync(user,Password);if(!result.Succeeded)throw new Exception("Fixture user failed");await users.AddToRoleAsync(user,ApplicationRoles.SourcingOfficer);}}
  await app.StartAsync();try{
   if(browser){Console.WriteLine("PASSKEY_BROWSER_FIXTURE "+url+" memory-only");await app.WaitForShutdownAsync();return;}
   using var handler=new HttpClientHandler{AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=HttpClientHandler.DangerousAcceptAnyServerCertificateValidator};using var client=new HttpClient(handler){BaseAddress=new(url)};
   async Task<string> Token(string path){var response=await client.GetAsync(path);var html=await response.Content.ReadAsStringAsync();return WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);}
   var anti=await Token("/Account/Login");Check(!string.IsNullOrEmpty(anti),"login csrf present");
   foreach(var action in new[]{"LoginOptions","Login","Cancel"})Check((await client.PostAsync("/Passkeys/"+action,new FormUrlEncodedContent([]))).StatusCode==HttpStatusCode.BadRequest,"passkey csrf required "+action);
   var response=await client.PostAsync("/Passkeys/LoginOptions",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",anti}}));Check(response.StatusCode==HttpStatusCode.OK&&response.Headers.CacheControl?.NoStore==true,"anonymous options/no-store");
   var options=await response.Content.ReadAsStringAsync();Check(options.Contains("required")&&options.Contains("localhost")&&!options.Contains("passkey-user"),"verification required and no account enumeration");
   response=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",anti},{"UserName","passkey-user"},{"Password",Password}}));Check(response.StatusCode==HttpStatusCode.Redirect,"existing password login preserved");var welcome=await client.GetStringAsync("/Passkeys/Index");Check(welcome.Contains("data-signin-welcome")&&welcome.Contains("Welcome, passkey-user."),"successful sign-in shows the authenticated account welcome");Check(!(await client.GetStringAsync("/Passkeys/Index")).Contains("data-signin-welcome"),"welcome is consumed once, not on every navigation");anti=await Token("/Passkeys/Index");
   if(faceChecks){FaceSettingsChecks.Rules();await FaceSettingsChecks.Mvc(client,app.Services);return;}
   response=await client.PostAsync("/Passkeys/EnrollOptions",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",anti},{"Name","My test key"},{"CurrentPassword","wrong-password"}}));Check(response.StatusCode==HttpStatusCode.BadRequest&&!((await response.Content.ReadAsStringAsync()).Contains("wrong-password")),"enrollment reauthentication and password redaction");
   response=await client.PostAsync("/Passkeys/EnrollOptions",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",anti},{"Name","My test key"},{"CurrentPassword",Password}}));Check(response.StatusCode==HttpStatusCode.OK&&(await response.Content.ReadAsStringAsync()).Contains("pubKeyCredParams"),"native Identity creation options");
   Check((await client.PostAsync("/Passkeys/Enroll",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",anti},{"CredentialJson","{}"}}))).StatusCode==HttpStatusCode.BadRequest,"malformed credential JSON rejected safely");
   Check(memory.Passkeys.Count==0,"options never persist credentials");
   Check((await client.GetAsync("/Passkeys/Remove")).StatusCode==HttpStatusCode.MethodNotAllowed,"no modifying GET");
   Console.WriteLine($"PASS: {count} memory-only passkey ticket, origin, native Identity options, MVC antiforgery and password regression checks. No SQL writes.");
  }finally{await app.StopAsync();}
 }
}
