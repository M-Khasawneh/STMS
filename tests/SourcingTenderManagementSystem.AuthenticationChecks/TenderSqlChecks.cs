using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Models.Tenders;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
public static class TenderSqlChecks
{
    public static async Task RunAsync()
    {
        using var settings=JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection=settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
        var target=new SqlConnectionStringBuilder(connection);
        if(!target.IntegratedSecurity||target.DataSource!=@"localhost\MSSQLSERVER02"||target.InitialCatalog!="SourcingTenderDB")throw new Exception("Unapproved SQL target.");
        using var key=RSA.Create(2048);var certRequest=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var generated=certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
        using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null);
        var clock=new TenderRuleChecks.TestClock(DateTimeOffset.UtcNow);
        var failure=new AuditFailure();
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(AccountController).Assembly.GetName().Name,EnvironmentName="Development",WebRootPath=Path.GetFullPath("src/SourcingTenderManagementSystem.Web/wwwroot")});
        builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(o=>o.Listen(IPAddress.Loopback,0,l=>l.UseHttps(certificate)));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();builder.Services.AddControllersWithViews();
        builder.Services.AddDbContext<ApplicationDbContext>(o=>o.UseSqlServer(connection).AddInterceptors(failure));
        builder.Services.AddIdentity<IdentityUser,IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();builder.Services.AddAuthenticationFoundation();
        builder.Services.AddSingleton<TimeProvider>(clock);builder.Services.AddScoped<TenderManagement>();builder.Services.AddScoped<RequisitionManagement>();
        builder.Services.AddScoped<IEmployeeData,EmployeeData>();builder.Services.AddScoped<EmployeeManagement>();builder.Services.AddScoped<IDepartmentStore,DepartmentStore>();builder.Services.AddScoped<IPublicStatistics,PublicStatistics>();
        await using var app=builder.Build();app.UseStaticFiles();app.UseAuthentication();app.UseAuthorization();app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
        Dictionary<string,long> baseline;string originalUsers,originalRoles;Guid department;
        using(var scope=app.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check((await db.Database.GetAppliedMigrationsAsync()).Contains("20261113180000_TenderInvitationIntegrity"),"tender migration applied before test writes");
            baseline=await Counts(db);originalUsers=JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());originalRoles=JsonSerializer.Serialize(await db.UserRoles.AsNoTracking().OrderBy(x=>x.UserId).ThenBy(x=>x.RoleId).ToListAsync());
            department=await db.Departments.Where(x=>x.Name=="Administration").Select(x=>x.Id).SingleAsync();
        }
        var marker="tender-check-"+Guid.NewGuid().ToString("N");var password="Aa1!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var ids=new Dictionary<string,string>();var profiles=new Dictionary<string,Guid>();
        await app.StartAsync();var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        HttpClient Browser()=>new(new HttpClientHandler{AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(_,cert,_,_)=>cert?.Thumbprint==certificate.Thumbprint}){BaseAddress=new Uri(address)};
        using var officer=Browser();using var manager=Browser();using var admin=Browser();using var owner=Browser();using var other=Browser();using var supplier1=Browser();using var supplier2=Browser();using var supplier3=Browser();using var supplier4=Browser();using var anonymous=Browser();
        try
        {
            await CreateUser("officer",new[]{ApplicationRoles.SourcingOfficer});await CreateUser("dual",new[]{ApplicationRoles.SourcingOfficer,ApplicationRoles.SourcingManager});await CreateUser("manager",new[]{ApplicationRoles.SourcingManager});await CreateUser("admin",new[]{ApplicationRoles.Admin,ApplicationRoles.SourcingOfficer,ApplicationRoles.SourcingManager});await CreateUser("owner",new[]{ApplicationRoles.RequestingDepartment});await CreateUser("other",new[]{ApplicationRoles.RequestingDepartment});
            foreach(var suffix in new[]{"supplier1","supplier2","supplier3","supplier4","supplier5","pending","suspended","disabled"})await CreateUser(suffix,new[]{ApplicationRoles.Supplier});
            foreach(var pair in new[]{(officer,"officer"),(manager,"manager"),(admin,"admin"),(owner,"owner"),(other,"other"),(supplier1,"supplier1"),(supplier2,"supplier2"),(supplier3,"supplier3"),(supplier4,"supplier4")})await Login(pair.Item1,pair.Item2);
            Check((await anonymous.GetAsync("/Tenders")).Headers.Location?.OriginalString.Contains("Login")==true,"anonymous tenders are protected");
            Check((await officer.PostAsync("/Tenders/Create",new FormUrlEncodedContent([]))).StatusCode==HttpStatusCode.BadRequest,"tender creation requires antiforgery");
            var draftSource=await Source("not-approved",PurchaseRequestStatus.Draft);
            await Reject(async s=>await s.SaveAsync(null,await Actor(s,"officer"),Form(draftSource)),409,"non-approved source cannot convert");
            var request=await Source("main");var original=Form(request);
            await Reject(async s=>await s.SaveAsync(null,await Actor(s,"admin"),original),403,"Admin cannot convert even with officer role");
            await Reject(async s=>await s.SaveAsync(null,await Actor(s,"manager"),original),403,"Manager cannot create tenders");
            var primary=await Execute(async s=>await s.SaveAsync(null,await Actor(s,"officer"),original));
            var detail=await Detail(primary);
            Check(detail.Source.Id==request&&detail.Source.Status==PurchaseRequestStatus.ConvertedToTender&&detail.Tender.Status==TenderStatus.Draft,"Draft conversion and source status persist atomically");
            Check(detail.Tender.SubmissionStart.Offset==TimeSpan.Zero&&detail.Tender.SubmissionDeadline.Offset==TimeSpan.Zero,"timestamps persist in UTC");
            await Reject(async s=>await s.SaveAsync(null,await Actor(s,"officer"),Form(request)),409,"duplicate source conversion rejected");
            Check((await owner.GetAsync("/Tenders/Details/"+primary)).StatusCode==HttpStatusCode.OK,"source requester can view linked Draft");
            Check((await other.GetAsync("/Tenders/Details/"+primary)).StatusCode==HttpStatusCode.NotFound,"requester cannot see colleague tender");
            Check((await supplier1.GetAsync("/Tenders/Details/"+primary)).StatusCode==HttpStatusCode.NotFound,"supplier cannot access Draft");
            foreach(var client in new[]{owner,admin,supplier1})Check(Denied(await client.GetAsync("/Tenders/Create")),"non-officer creation UI denied");
            var edit=Form(request);edit.Version=detail.Version;edit.Title=marker+" revised";
            await Execute(async s=>{await s.SaveAsync(primary,await Actor(s,"officer"),edit);return true;});
            await Reject(async s=>await s.SaveAsync(primary,await Actor(s,"officer"),edit),409,"stale Draft form rejected");
            // Configuration and approval are intentionally separate roles and evidence.
            await Reject(async s=>{await s.ChangeStatusAsync(primary,await Actor(s,"officer"),(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version,TenderStatus.Open);return true;},400,"cannot skip publication");
            var invalidCriteria=await Configuration(primary,60m,39.999999m);
            await Execute(async s=>{await s.SaveCriteriaAsync(primary,await Actor(s,"officer"),invalidCriteria);return true;});
            await Reject(async s=>{await s.ApproveCriteriaAsync(primary,await Actor(s,"manager"),(await s.DetailsAsync(primary,await Actor(s,"manager"))).Version);return true;},400,"criteria total must be exactly 100");
            var corrected=await Configuration(primary,60m,40m);await Execute(async s=>{await s.SaveCriteriaAsync(primary,await Actor(s,"officer"),corrected);return true;});
            await Reject(async s=>{await s.SaveCriteriaAsync(primary,await Actor(s,"officer"),corrected);return true;},409,"stale criteria configuration refused");
            await Reject(async s=>{await s.ApproveCriteriaAsync(primary,await Actor(s,"officer"),(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version);return true;},403,"officer cannot approve criteria");
            await Reject(async s=>{await s.ApproveCriteriaAsync(primary,await Actor(s,"admin"),(await s.DetailsAsync(primary,await Actor(s,"admin"))).Version);return true;},403,"Admin criteria approval is forbidden");
            // Publish before criteria approval is allowed, but opening remains gated.
            await Status(primary,TenderStatus.Published);
            await Reject(async s=>{await s.ChangeStatusAsync(primary,await Actor(s,"officer"),(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version,TenderStatus.Open);return true;},400,"opening requires approved criteria");
            var lockedEdit=Form(request);lockedEdit.Version=(await Detail(primary)).Version;
            await Reject(async s=>await s.SaveAsync(primary,await Actor(s,"officer"),lockedEdit),400,"Published core content is read-only");
            await Execute(async s=>{await s.ApproveCriteriaAsync(primary,await Actor(s,"manager"),(await s.DetailsAsync(primary,await Actor(s,"manager"))).Version);return true;});
            detail=await Detail(primary);Check(TenderManagement.Approved(detail.Criteria)&&detail.Criteria.All(x=>x.ApprovedByUserId==ids["manager"]),"criteria approval evidence persists across the complete set");
            await Reject(async s=>{await s.SaveCriteriaAsync(primary,await Actor(s,"officer"),await Configuration(primary,50,50));return true;},400,"approved criteria and weights cannot change");
            var selfSource=await Source("self-approval");var self=await Execute(async s=>await s.SaveAsync(null,await Actor(s,"officer"),Form(selfSource)));
            await Execute(async s=>{await s.SaveCriteriaAsync(self,await Actor(s,"dual"),await Configuration(self,100));return true;});
            await Reject(async s=>{await s.ApproveCriteriaAsync(self,await Actor(s,"dual"),(await s.DetailsAsync(self,await Actor(s,"dual"))).Version);return true;},403,"dual-role configurator cannot approve own criteria");
            await Execute(async s=>{await s.ApproveCriteriaAsync(self,await Actor(s,"manager"),(await s.DetailsAsync(self,await Actor(s,"manager"))).Version);return true;});
            // Invitation creation rechecks eligibility, uses one transaction and keeps supplier information private.
            foreach(var suffix in new[]{"pending","suspended","disabled"})await Reject(async s=>{await s.InviteAsync(primary,await Actor(s,"officer"),new(){Version=(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version,SupplierIds=[profiles[suffix]]});return true;},400,"ineligible supplier refused: "+suffix);
            await Reject(async s=>{await s.InviteAsync(self,await Actor(s,"officer"),new(){Version=(await s.DetailsAsync(self,await Actor(s,"officer"))).Version,SupplierIds=[profiles["supplier1"]]});return true;},400,"Draft invitations refused");
            await Execute(async s=>{await s.InviteAsync(primary,await Actor(s,"dual"),new(){Version=(await s.DetailsAsync(primary,await Actor(s,"dual"))).Version,SupplierIds=[profiles["supplier1"],profiles["supplier2"],profiles["supplier5"]]});return true;});
            await Reject(async s=>{await s.InviteAsync(primary,await Actor(s,"officer"),new(){Version=(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version,SupplierIds=[profiles["supplier1"]]});return true;},409,"duplicate invitation/resend refused");
            var raceVersion=(await Detail(primary)).Version;
            var inviteRace=await Task.WhenAll(InviteRace(),InviteRace());Check(inviteRace.Count(x=>x)==1,"concurrent duplicate invitations yield exactly one success");
            var one=await OwnInvitation("supplier1",primary);var two=await OwnInvitation("supplier2",primary);var three=await OwnInvitation("supplier3",primary);
            Check((await supplier1.GetAsync("/Tenders/Details/"+primary)).StatusCode==HttpStatusCode.OK,"invited supplier sees Published tender");
            Check((await supplier4.GetAsync("/Tenders/Details/"+primary)).StatusCode==HttpStatusCode.NotFound,"uninvited approved supplier cannot see protected tender");
            Check((await supplier1.GetAsync("/Tenders/Invitation/"+two.Invitation.Id)).StatusCode==HttpStatusCode.NotFound,"invitation ownership enforced");
            Check(Denied(await supplier1.GetAsync("/Tenders/Invitations/"+primary)),"suppliers cannot view competitor invitation tracking");
            var ownHtml=await supplier1.GetStringAsync("/Tenders/Inbox");Check(!ownHtml.Contains(profiles["supplier2"].ToString())&&!ownHtml.Contains(marker+" supplier2"),"inbox exposes no competitor company or invitation information");
            Check((await supplier1.PostAsync("/Tenders/Respond/"+one.Invitation.Id,new FormUrlEncodedContent([]))).StatusCode==HttpStatusCode.BadRequest,"invitation response requires antiforgery");
            await Execute(async s=>{await s.RespondAsync(one.Invitation.Id,await Actor(s,"supplier1"),TenderManagement.InvitationVersion(one),InvitationStatus.Accepted);return true;});
            await Execute(async s=>{await s.RespondAsync(two.Invitation.Id,await Actor(s,"supplier2"),TenderManagement.InvitationVersion(two),InvitationStatus.Declined);return true;});
            await Reject(async s=>{await s.RespondAsync(one.Invitation.Id,await Actor(s,"supplier1"),TenderManagement.InvitationVersion(one),InvitationStatus.Declined);return true;},409,"supplier cannot change a final response");
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Check(await db.Notifications.CountAsync(x=>x.TenderId==primary&&x.RecipientUserId==ids["officer"])==2,"responses notify responsible tender creator rather than the sending officer");
                Check(await db.Notifications.CountAsync(x=>x.TenderId==primary&&new[]{ids["supplier1"],ids["supplier2"],ids["supplier3"],ids["supplier5"]}.Contains(x.RecipientUserId))==4,"one in-app notification per invitation");
            }
            var notificationHtml=await supplier4.GetStringAsync("/Notifications");Check(!notificationHtml.Contains(marker+" revised"),"notification inbox is recipient scoped");
            await Status(primary,TenderStatus.Open);Check(await Execute(async s=>s.InSubmissionPeriod((await s.DetailsAsync(primary,await Actor(s,"officer"))).Tender)),"approved tender opens within its window");
            await Reject(async s=>{await s.ChangeStatusAsync(primary,await Actor(s,"officer"),(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version,TenderStatus.Closed);return true;},400,"cannot close before the configured submission period ends");
            // Preserve the real clock for browser flows before jumping to the exclusive deadline.
            var browserSource=await Source("browser-configuration");var browserTender=await Execute(async s=>await s.SaveAsync(null,await Actor(s,"officer"),Form(browserSource)));
            var browserInvitation=await OwnInvitation("supplier5",primary);
            var start=new System.Diagnostics.ProcessStartInfo("node","tests/tender-browser.cjs"){UseShellExecute=false};start.Environment["TENDER_TEST_URL"]=address;start.Environment["TENDER_TEST_PASSWORD"]=password;start.Environment["TENDER_TEST_MARKER"]=marker;start.Environment["TENDER_TEST_PRIMARY"]=primary.ToString();start.Environment["TENDER_TEST_DRAFT"]=self.ToString();start.Environment["TENDER_TEST_INVITATION"]=browserInvitation.Invitation.Id.ToString();start.Environment["TENDER_TEST_CONFIG"]=browserTender.ToString();
            using(var process=System.Diagnostics.Process.Start(start)!){await process.WaitForExitAsync();Check(process.ExitCode==0,"tender and invitation browser checks");}
            clock.Utc=detail.Tender.SubmissionDeadline;
            Check(!await Execute(async s=>s.InSubmissionPeriod((await s.DetailsAsync(primary,await Actor(s,"officer"))).Tender)),"exact deadline enforces closed availability without changing tender status");
            await Reject(async s=>{await s.RespondAsync(three.Invitation.Id,await Actor(s,"supplier3"),TenderManagement.InvitationVersion(three),InvitationStatus.Accepted);return true;},409,"response at exact deadline is refused");
            three=await OwnInvitation("supplier3",primary);Check(three.Invitation.Status==InvitationStatus.Expired,"unanswered invitation expiry persists");
            Check((await OwnInvitation("supplier1",primary)).Invitation.Status==InvitationStatus.Accepted&&(await OwnInvitation("supplier2",primary)).Invitation.Status==InvitationStatus.Declined,"answered invitations retain final statuses after deadline");
            Check((await Detail(primary)).Tender.Status==TenderStatus.Open,"deadline does not automatically advance tender status");
            await Status(primary,TenderStatus.Closed);Check((await Detail(primary)).Tender.Status==TenderStatus.Closed,"officer explicitly closes the tender");
            await Reject(async s=>{await s.ChangeStatusAsync(primary,await Actor(s,"officer"),(await s.DetailsAsync(primary,await Actor(s,"officer"))).Version,TenderStatus.Open);return true;},400,"closed tender cannot reopen");
            clock.Utc=DateTimeOffset.UtcNow;
            var futureSource=await Source("future");var futureForm=Form(futureSource);futureForm.Start=TenderTime.Local(clock.Utc.AddMinutes(5));var future=await Execute(async s=>await s.SaveAsync(null,await Actor(s,"officer"),futureForm));await Execute(async s=>{await s.SaveCriteriaAsync(future,await Actor(s,"officer"),await Configuration(future,100));return true;});await Execute(async s=>{await s.ApproveCriteriaAsync(future,await Actor(s,"manager"),(await s.DetailsAsync(future,await Actor(s,"manager"))).Version);return true;});await Status(future,TenderStatus.Published);
            await Reject(async s=>{await s.ChangeStatusAsync(future,await Actor(s,"officer"),(await s.DetailsAsync(future,await Actor(s,"officer"))).Version,TenderStatus.Open);return true;},400,"opening before start is refused");
            var raceSource=await Source("race-convert");var conversions=await Task.WhenAll(ConvertRace(),ConvertRace());Check(conversions.Count(x=>x)==1,"concurrent source conversion produces exactly one tender");
            var failureSource=await Source("rollback");failure.FailNext=true;
            try{await Execute(async s=>await s.SaveAsync(null,await Actor(s,"officer"),Form(failureSource)));throw new Exception("Expected audit failure.");}catch(SyntheticAuditException){}
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();Check((await db.PurchaseRequisitions.FindAsync(failureSource))!.Status==PurchaseRequestStatus.Approved&&!await db.TenderPurchaseRequisitions.AnyAsync(x=>x.PurchaseRequisitionId==failureSource),"audit failure rolls back tender, link and source conversion");}
            async Task<bool> InviteRace(){try{await Execute(async s=>{await s.InviteAsync(primary,await Actor(s,"officer"),new(){Version=raceVersion,SupplierIds=[profiles["supplier3"]]});return true;});return true;}catch(TenderException e)when(e.Status==409){return false;}}
            async Task<bool> ConvertRace(){try{await Execute(async s=>await s.SaveAsync(null,await Actor(s,"officer"),Form(raceSource)));return true;}catch(TenderException e)when(e.Status==409){return false;}}
        }
        finally
        {
            await app.StopAsync();using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var ownedUsers=await db.Users.Where(x=>x.UserName!=null&&x.UserName.StartsWith(marker+"-")).ToListAsync();var userIds=ownedUsers.Select(x=>x.Id).ToArray();
            var requests=await db.PurchaseRequisitions.Where(x=>userIds.Contains(x.CreatedByUserId)).ToListAsync();var reqIds=requests.Select(x=>x.Id).ToArray();
            var tenders=await db.Tenders.Where(x=>userIds.Contains(x.CreatedByUserId)).ToListAsync();var tenderIds=tenders.Select(x=>x.Id).ToArray();
            var criteria=await db.EvaluationCriteria.Where(x=>tenderIds.Contains(x.TenderId)).ToListAsync();var pairs=await db.TenderSuppliers.Where(x=>tenderIds.Contains(x.TenderId)).ToListAsync();var pairIds=pairs.Select(x=>x.Id).ToArray();var invitations=await db.Invitations.Where(x=>pairIds.Contains(x.TenderSupplierId)).ToListAsync();
            var recordIds=reqIds.Concat(tenderIds).Concat(criteria.Select(x=>x.Id)).Concat(invitations.Select(x=>x.Id)).Select(x=>x.ToString()).ToArray();
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                db.Notifications.RemoveRange(await db.Notifications.Where(x=>x.TenderId.HasValue&&tenderIds.Contains(x.TenderId.Value)&&userIds.Contains(x.RecipientUserId)).ToListAsync());db.AuditLogs.RemoveRange(await db.AuditLogs.Where(x=>(recordIds.Contains(x.RecordId)||(x.ActorUserId!=null&&userIds.Contains(x.ActorUserId)))&&new[]{"Tender","Invitation","EvaluationCriterion","PurchaseRequisition"}.Contains(x.RecordType)).ToListAsync());db.Invitations.RemoveRange(invitations);db.EvaluationCriteria.RemoveRange(criteria);await db.SaveChangesAsync();
                db.TenderSuppliers.RemoveRange(pairs);db.TenderPurchaseRequisitions.RemoveRange(await db.TenderPurchaseRequisitions.Where(x=>tenderIds.Contains(x.TenderId)).ToListAsync());await db.SaveChangesAsync();db.Tenders.RemoveRange(tenders);await db.SaveChangesAsync();
                db.PurchaseRequisitionItems.RemoveRange(await db.PurchaseRequisitionItems.Where(x=>reqIds.Contains(x.PurchaseRequisitionId)).ToListAsync());await db.SaveChangesAsync();db.PurchaseRequisitions.RemoveRange(requests);db.SupplierProfiles.RemoveRange(await db.SupplierProfiles.Where(x=>userIds.Contains(x.UserId)).ToListAsync());db.EmployeeDepartments.RemoveRange(await db.EmployeeDepartments.Where(x=>userIds.Contains(x.UserId)).ToListAsync());await db.SaveChangesAsync();foreach(var user in ownedUsers)Check((await users.DeleteAsync(user)).Succeeded,"temporary tender account removed");await tx.CommitAsync();
            }
            var final=await Counts(db);Check(baseline.Count==final.Count&&baseline.All(x=>final.GetValueOrDefault(x.Key)==x.Value),"all database table counts match post-migration baseline");Check(originalUsers==JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync())&&originalRoles==JsonSerializer.Serialize(await db.UserRoles.AsNoTracking().OrderBy(x=>x.UserId).ThenBy(x=>x.RoleId).ToListAsync()),"original Identity accounts and memberships unchanged");
        }
        Console.WriteLine("PASS: tender SQL/HTTPS/browser checks; all temporary records removed.");
        async Task CreateUser(string suffix,string[] roles)
        {
            using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var users=scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();var user=new IdentityUser(marker+"-"+suffix);Check((await users.CreateAsync(user,password)).Succeeded&&(await users.AddToRolesAsync(user,roles)).Succeeded,"fixture: "+suffix);ids[suffix]=user.Id;
            if(roles.Contains(ApplicationRoles.Supplier)){var profile=new SupplierProfile{Id=Guid.NewGuid(),UserId=user.Id,CompanyName=marker+" "+suffix,RegistrationNumber=marker+suffix,TaxNumber=marker+suffix,ContactName="Test",ContactEmail="test@example.invalid",ContactPhone="+96265550100",Status=suffix=="pending"?SupplierStatus.Pending:suffix=="suspended"?SupplierStatus.Suspended:SupplierStatus.Approved};profiles[suffix]=profile.Id;db.SupplierProfiles.Add(profile);if(suffix=="disabled"){user.LockoutEnabled=true;user.LockoutEnd=DateTimeOffset.MaxValue;await users.UpdateAsync(user);}}
            else db.EmployeeDepartments.Add(new EmployeeDepartment{UserId=user.Id,DepartmentId=department});await db.SaveChangesAsync();
        }
        async Task<Guid> Source(string suffix,PurchaseRequestStatus status=PurchaseRequestStatus.Approved){using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();var r=new PurchaseRequisition{Id=Guid.NewGuid(),CreatedByUserId=ids["owner"],DepartmentId=department,Title=marker+" "+suffix,Reason="Temporary test",Priority="High",TargetDate=DateOnly.FromDateTime(DateTime.Now),Status=status,CreatedAt=clock.Utc};db.PurchaseRequisitions.Add(r);db.PurchaseRequisitionItems.Add(new PurchaseRequisitionItem{Id=Guid.NewGuid(),PurchaseRequisitionId=r.Id,Description="Test material",Quantity=1.5m,Specifications="Test specifications"});await db.SaveChangesAsync();return r.Id;}
        TenderForm Form(Guid source)=>new(){RequisitionId=source,Title=marker+" tender",Description="Tender description",Terms="Tender terms",Start=TenderTime.Local(clock.Utc.AddMinutes(-5)),Deadline=TenderTime.Local(clock.Utc.AddHours(1))};
        async Task<CriteriaForm> Configuration(Guid id,params decimal[] weights){var d=await Detail(id);return new(){Version=d.Version,Items=weights.Select((w,i)=>new CriterionForm{Id=d.Criteria.Count>i?d.Criteria[i].Id:null,Name="Test criterion "+i,Description="Test requirement "+i,Kind=i==0?EvaluationKind.Technical:EvaluationKind.Financial,Weight=w}).ToList()};}
        async Task<T> Execute<T>(Func<TenderManagement,Task<T>> operation){using var scope=app.Services.CreateScope();return await operation(scope.ServiceProvider.GetRequiredService<TenderManagement>());}
        async Task<TenderActor> Actor(TenderManagement service,string suffix)=>await service.ActorAsync(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,ids[suffix])},"Test")));
        Task<TenderDetails> Detail(Guid id)=>Execute(async s=>await s.DetailsAsync(id,await Actor(s,"officer")));
        Task<InvitationRow> OwnInvitation(string suffix,Guid id)=>Execute(async s=>(await s.InboxAsync(await Actor(s,suffix))).Single(x=>x.Tender.Id==id));
        async Task Status(Guid id,TenderStatus decision){await Execute(async s=>{await s.ChangeStatusAsync(id,await Actor(s,"officer"),(await s.DetailsAsync(id,await Actor(s,"officer"))).Version,decision);return true;});}
        async Task Reject<T>(Func<TenderManagement,Task<T>> operation,int code,string message){try{await Execute(operation);throw new Exception("Expected rejection: "+message);}catch(TenderException e)when(e.Status==code){Check(true,message);}}
        async Task Login(HttpClient client,string suffix){var response=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["UserName"]=marker+"-"+suffix,["Password"]=password,["__RequestVerificationToken"]=await Token(client,"/Account/Login")}));Check(response.StatusCode==HttpStatusCode.Redirect,"sign-in: "+suffix);}
    }
    private static bool Denied(HttpResponseMessage r)=>r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound||r.Headers.Location?.OriginalString.Contains("AccessDenied")==true;
    private static async Task<string> Token(HttpClient client,string path){var html=await client.GetStringAsync(path);var match=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");if(!match.Success)throw new Exception("Missing antiforgery token.");return WebUtility.HtmlDecode(match.Groups[1].Value);}
    private static async Task<Dictionary<string,long>> Counts(ApplicationDbContext db){await db.Database.OpenConnectionAsync();var result=new Dictionary<string,long>();foreach(var table in db.Model.GetEntityTypes().Select(x=>x.GetTableName()!).Distinct().Append("__EFMigrationsHistory")){await using var c=db.Database.GetDbConnection().CreateCommand();c.CommandText="SELECT COUNT_BIG(*) FROM [dbo].["+table.Replace("]","]]")+"]";result[table]=(long)(await c.ExecuteScalarAsync())!;}return result;}
    private static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
    private sealed class SyntheticAuditException:Exception{}
    private sealed class AuditFailure:SaveChangesInterceptor{public bool FailNext{get;set;}public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken cancellationToken=default){if(FailNext&&e.Context!.ChangeTracker.Entries<AuditLog>().Any(x=>x.State==EntityState.Added)){FailNext=false;throw new SyntheticAuditException();}return ValueTask.FromResult(result);}}
}
