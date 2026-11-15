using System.Net;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Models.Work;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
internal static class WorkspaceEnhancementChecks
{
    public static async Task Rules()
    {
        int n=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);n++;}
        var f=new BidPhase1Checks.Fixture();
        await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var service=new WorkspaceDiscovery(db,f.Store,new BidData(db,null!),f.Clock);
        var department=Guid.NewGuid();
        BidAccount A(string role)=>new("actor",[role],role==ApplicationRoles.Supplier?Guid.NewGuid():null,true,true,true,[],[]);
        foreach(var role in ApplicationRoles.All){
            var a=A(role);Check(WorkspaceDiscovery.Eligible(a),"Eligible known role "+role);
            foreach(var tasks in new[]{true,false})foreach(var group in service.Queries(a,department,tasks,f.Clock.Utc)){
                var sql=group.Rows.Where(x=>x.Title.Contains("network")||x.Id=="reference").OrderBy(x=>x.At).ThenBy(x=>x.Id).Skip(20).Take(20).ToQueryString();
                Check(!System.Text.RegularExpressions.Regex.IsMatch(sql,@"CAST\([^)]*\[Status\][^)]*AS int",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"String-backed status must not be cast to SQL int "+group.Key);
                Check(sql.Contains("SELECT")&&sql.Contains("OFFSET"),"Real SQL Server discovery translation "+role+"/"+tasks+"/"+group.Key);
                if(role==ApplicationRoles.Supplier)Check(!sql.Contains("[UnitPrice]")&&!sql.Contains("[StorageReference]"),"Supplier discovery never projects confidential contents");
            }
        }
        foreach(var a in new[]{A(ApplicationRoles.Supplier) with{SupplierApproved=false},A(ApplicationRoles.Supplier) with{Roles=[ApplicationRoles.Admin,ApplicationRoles.Supplier]},A(ApplicationRoles.Admin) with{Enabled=false},A(ApplicationRoles.RequestingDepartment) with{EmployeeDepartment=false},A("Unknown")})Check(!WorkspaceDiscovery.Eligible(a),"Ineligible account denied");
        var requester=service.VisibleRequests(A(ApplicationRoles.RequestingDepartment)).ToQueryString();Check(requester.Contains("CreatedByUserId")&&requester.Contains("actor"),"Requester discovery uses personal ownership, never department-wide visibility");
        var officer=service.Queries(A(ApplicationRoles.SourcingOfficer),department,true,f.Clock.Utc).Single(g=>g.Key=="request-review").Rows.ToQueryString();Check(officer.Contains("CreatedByUserId")&&officer.Contains("DepartmentId")&&officer.Contains("<>"),"Officer review excludes self and own department");
        var evaluator=service.VisibleTenders(A(ApplicationRoles.EvaluationCommittee),f.Clock.Utc).ToQueryString();Check(evaluator.Contains("CommitteeAssignments")&&evaluator.Contains("EffectiveTo")&&evaluator.Contains("EffectiveFrom"),"Evaluation search uses current tender-specific membership");
        var top=service.VisibleTenders(A(ApplicationRoles.TopCommittee),f.Clock.Utc).ToQueryString();Check(top.Contains("CommitteeAssignments")&&top.Contains("Status"),"Top search restricts current membership and final stage");
        var adminTasks=service.Queries(A(ApplicationRoles.Admin),department,true,f.Clock.Utc);Check(adminTasks.All(g=>g.Key=="suppliers"),"Admin receives no bid/evaluation/review task");
        Check(!service.Queries(A(ApplicationRoles.RequestingDepartment),department,false,f.Clock.Utc).Any(g=>g.Key=="offers"),"Requester never discovers supplier offers");
        Check(!service.Queries(A(ApplicationRoles.Admin),department,false,f.Clock.Utc).Any(g=>g.Key=="offers"),"Admin never discovers confidential offers");
        var mixed=service.Queries(A(ApplicationRoles.Admin) with{Roles=[ApplicationRoles.Admin,ApplicationRoles.SourcingOfficer]},department,true,f.Clock.Utc);Check(mixed.All(g=>g.Key=="suppliers"),"Admin precedence retained with combined roles");
        var supplierDraft=service.Queries(A(ApplicationRoles.Supplier),null,true,f.Clock.Utc).Single(g=>g.Key=="offer-drafts").Rows.ToQueryString();Check(supplierDraft.Contains("SubmissionStart")&&supplierDraft.Contains("SubmissionDeadline")&&supplierDraft.Contains("Invitations")&&supplierDraft.Contains("SupplierProfileId"),"Supplier tasks retain invitation, ownership and exact deadline conditions");
        var audit=service.SafeAudit().Where(a=>a.Action.Contains("Created")).OrderByDescending(a=>a.At).Skip(20).Take(20).ToQueryString();var projection=audit[..audit.IndexOf("FROM",StringComparison.Ordinal)];Check(!projection.Contains("Reason")&&!projection.Contains("ActorUserId")&&!projection.Contains("PreviousStatus")&&!projection.Contains("NewStatus"),"Audit redacts identities and all free-text/status payloads in SQL");Check(audit.Contains("RecordType")&&!projection.Contains("BidId"),"Administrative audit uses safe metadata projection");
        var portal=new SupplierTenderPortal(db,null!,null!,f.Clock).OwnedInvitation(Guid.NewGuid(),Guid.NewGuid()).ToQueryString();Check(portal.Contains("TenderSuppliers")&&portal.Contains("SupplierProfileId")&&portal.Contains("TenderId")&&portal.Contains("Status"),"Portal query binds invitation, supplier, tender and published visibility");
        using(var first=new NotificationWatchGate().Enter("account")){Check(first is not null,"Notification stream accepts first tab");}
        var gate=new NotificationWatchGate();var l1=gate.Enter("account");var l2=gate.Enter("account");Check(gate.Enter("account") is null,"Notification connection limit enforced per user");using(var other=gate.Enter("other")){Check(other is not null,"Independent account has its own connection allowance");}l1!.Dispose();l1.Dispose();using(var replacement=gate.Enter("account")){Check(replacement is not null,"Disconnect releases connection and double dispose is safe");}l2!.Dispose();
        var item=new Notification{Id=Guid.NewGuid(),RecipientUserId="one",Subject="Private"};var menu=new NotificationMenu([item],1,true);var version=NotificationWatchGate.Version(menu);item.ReadAt=f.Clock.Utc;Check(version!=NotificationWatchGate.Version(menu with{Unread=0}),"Notification fingerprint changes on reading");Check(!version.Contains("Private"),"Live signal is opaque metadata without notification text");
        foreach(var input in new[]{"anonymous","suspended","mixed"}){
            try{await service.ReadAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,input)],"fixture")),true,null,null,1,default);throw new Exception("Expected authorization denial");}catch(BidException e){Check(e.Status==403,"Discovery rejects "+input+" before any SQL query");}
        }
        Console.WriteLine($"PASS: {n} database-free workspace SQL translation, role, ownership, redaction and live-channel checks.");
    }
    public static async Task Mvc(HttpClient client,IServiceProvider services,BidPhase1Checks.Fixture f)
    {
        int n=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);n++;}
        async Task<HttpResponseMessage> Get(string actor,string path){client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");client.DefaultRequestHeaders.Add("X-Bid-Test-Actor",actor);return await client.GetAsync(path);}
        foreach(var action in new[]{"Tasks","Search?q=network","Audit"})Check((await Get("anonymous","/Work/"+action)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous work route denied");
        foreach(var actor in new[]{"admin","requester","officer","manager","evaluator","top","supplier"})foreach(var action in new[]{"Tasks","Search?q=network"}){
            var response=await Get(actor,"/Work/"+action);var html=await response.Content.ReadAsStringAsync();Check(response.StatusCode==HttpStatusCode.OK,"Compiled workspace Razor "+actor+"/"+action);Check(response.Headers.CacheControl?.NoStore==true,"Workspace is not cached");Check(!html.Contains("<script>synthetic work</script>")&&html.Contains("&lt;script&gt;synthetic work&lt;/script&gt;"),"Work title escaped");Check(html.Contains("/Work/Tasks")&&html.Contains("/Work/Search"),"Navigation routes wired");
        }
        foreach(var actor in new[]{"requester","officer","manager","evaluator","top","supplier"})Check((await Get(actor,"/Work/Audit")).StatusCode==HttpStatusCode.Forbidden,"Administrative audit role gate "+actor);
        var audit=await Get("admin","/Work/Audit?q=Created&type=Tender&from=2026-01-01&to=2026-12-31&page=2");var auditHtml=await audit.Content.ReadAsStringAsync();Check(audit.StatusCode==HttpStatusCode.OK&&auditHtml.Contains("Metadata only"),"Audit Razor renders metadata");Check(!auditHtml.Contains("confidential reason")&&!auditHtml.Contains("UnitPrice"),"Audit has no confidential payload");
        var discovery=(WorkDiscoveryFixture)services.GetRequiredService<IWorkspaceDiscovery>();Check(discovery.LastAudit?.Page==2&&discovery.LastAudit.Type=="Tender","Audit filter and pagination binding");
        Check((await Get("admin","/Work/Audit?from=bad-date")).StatusCode==HttpStatusCode.BadRequest,"Malformed audit dates rejected");
        Check((await Get("supplier","/Work/Search?q="+new string('x',161))).StatusCode==HttpStatusCode.BadRequest,"Bounded search input");
        Check((await Get("suspended","/Work/Tasks")).StatusCode==HttpStatusCode.Forbidden,"Suspended account fails service gate");
        var search=await Get("requester","/Work/Search?q=network&category=requests&page=2");Check(search.StatusCode==HttpStatusCode.OK&&discovery.Last?.Category=="requests"&&discovery.Last.Page==2,"Search category/query/page wiring");
        Check((await Get("other","/SupplierPortal/Details/"+f.Context.Tender.Id)).StatusCode==HttpStatusCode.NotFound,"Competing supplier portal hidden");
        foreach(var actor in new[]{"anonymous","admin","officer","requester","manager","evaluator","top"})Check((await Get(actor,"/SupplierPortal/Details/"+f.Context.Tender.Id)).StatusCode==(actor=="anonymous"?HttpStatusCode.Unauthorized:HttpStatusCode.Forbidden),"Supplier portal role gate "+actor);
        var portal=await Get("supplier","/SupplierPortal/Details/"+f.Context.Tender.Id);var portalHtml=await portal.Content.ReadAsStringAsync();Check(portal.StatusCode==HttpStatusCode.OK&&portalHtml.Contains("Prepare offer")&&portalHtml.Contains("Requested items"),"Portal renders accepted invitation and authoritative items");Check(portalHtml.Contains("/Bids/Create?tenderId="),"Portal preserves bid service route");Check(portal.Headers.CacheControl?.NoStore==true,"Private supplier portal no-store");
        var portalFixture=(SupplierPortalFixture)services.GetRequiredService<ISupplierTenderPortal>();portalFixture.Pending=true;
        portal=await Get("supplier","/SupplierPortal/Details/"+f.Context.Tender.Id);portalHtml=await portal.Content.ReadAsStringAsync();Check(portalHtml.Contains("__RequestVerificationToken")&&portalHtml.Contains("/Tenders/Respond/"),"Invitation response uses existing POST/antiforgery route");portalFixture.Pending=false;
        var id=await f.Service.CreateDraftAsync(f.User,f.Context.Tender.Id,f.Form());portal=await Get("supplier","/SupplierPortal/Details/"+f.Context.Tender.Id);portalHtml=await portal.Content.ReadAsStringAsync();Check(portalHtml.Contains("Continue draft")&&portalHtml.Contains("/Bids/Summary/"+id),"Existing own draft and summary shown");
        f.Store.Rows[id].Bid.Status=BidStatus.Submitted;f.Store.Rows[id].Bid.SubmittedAt=f.Clock.Utc;portal=await Get("supplier","/SupplierPortal/Details/"+f.Context.Tender.Id);portalHtml=await portal.Content.ReadAsStringAsync();Check(!portalHtml.Contains("Continue draft")&&portalHtml.Contains("read-only"),"Submitted portal offer stays immutable");
        Check((await Get("anonymous","/Notifications/Menu")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous live inbox denied");Check((await Get("suspended","/Notifications/Menu")).StatusCode==HttpStatusCode.Forbidden,"Revoked account live inbox denied");
        var menu=await Get("supplier","/Notifications/Menu?returnUrl=https://external.test");Check(menu.StatusCode==HttpStatusCode.OK&&menu.Headers.GetValues("X-STMS-Notification-Menu").Single()=="1","Live inbox serves existing encoded partial");Check(!(await menu.Content.ReadAsStringAsync()).Contains("external.test"),"Unsafe live inbox return URL discarded");
        client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");client.DefaultRequestHeaders.Add("X-Bid-Test-Actor","supplier");
        var notifications=(MemoryNotifications)services.GetRequiredService<INotificationStore>();
        var extra=new Notification{Id=Guid.NewGuid(),RecipientUserId="supplier",Subject="New committed synthetic notice",Message="Only this supplier",CreatedAt=f.Clock.Utc};
        var account=f.Store.Accounts["supplier"];
        using(var cts=new CancellationTokenSource(TimeSpan.FromSeconds(42)))
        try{
            using var response=await client.GetAsync("/Notifications/Watch",HttpCompletionOption.ResponseHeadersRead,cts.Token);
            Check(response.StatusCode==HttpStatusCode.OK&&response.Content.Headers.ContentType?.MediaType=="text/event-stream","Authenticated native event stream opens");
            Check(response.Headers.CacheControl?.NoStore==true,"Live stream is private/no-store");
            using var reader=new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
            async Task Event(string expected){string? line;while((line=await reader.ReadLineAsync(cts.Token))!=expected){if(line is null)throw new Exception("Stream ended before "+expected);}}
            await Event("event: changed");Check(true,"Stream signals committed inbox refresh without contents");
            notifications.Rows.Add(extra);
            await Event("event: changed");Check(true,"Live stream detects new persisted memory notification on next committed read");
            var own=await Get("supplier","/Notifications/Menu");Check((await own.Content.ReadAsStringAsync()).Contains(extra.Subject),"Refreshed menu contains owner's new notification");
            var foreign=await Get("other","/Notifications/Menu");Check(!(await foreign.Content.ReadAsStringAsync()).Contains(extra.Subject),"New notification isolated from competing account");
            f.Store.Accounts["supplier"]=account with{Enabled=false};
            await Event("event: revoked");Check(true,"Existing live stream stops when account eligibility is revoked");
            Check((await Get("supplier","/Notifications/Menu")).StatusCode==HttpStatusCode.Forbidden,"Revoked account cannot retrieve refreshed content");
        }finally{cts.Cancel();f.Store.Accounts["supplier"]=account;notifications.Rows.Remove(extra);}
        Console.WriteLine($"PASS: {n} memory-only workspace, supplier portal, audit and live-notification MVC checks.");
    }
}
internal sealed class WorkDiscoveryFixture(BidPhase1Checks.Fixture f):IWorkspaceDiscovery
{
    public WorkPage? Last;public AuditPage? LastAudit;
    async Task<BidAccount> Actor(ClaimsPrincipal user){var a=await f.Store.AccountAsync(user,f.Clock.Utc,default);BidRules.Require(WorkspaceDiscovery.Eligible(a),403,"Eligible account required.");return a!;}
    public async Task<WorkPage> ReadAsync(ClaimsPrincipal user,bool tasks,string? query,string? category,int page,CancellationToken ct){var a=await Actor(user);BidRules.Require((query?.Length??0)<=160,400,"Search too long.");return Last=new(tasks,query??"",category,page,[new("requests","Your authorized records",a.Supplier?"Bids":"Requisitions","Details","id",a.Supplier?"bid":"request",[new(){Id=Guid.NewGuid().ToString(),Title="<script>synthetic work</script>",State="Draft",At=f.Clock.Utc}],24)]);}
    public async Task<AuditPage> AuditAsync(ClaimsPrincipal user,string? query,string? type,DateOnly? from,DateOnly? to,int page,CancellationToken ct){BidRules.Require(ReportRules.Admin(await Actor(user)),403,"Admin required.");return LastAudit=new(query??"",type,from,to,page,42,[new(Guid.NewGuid(),"Tender.Created","Tender",Guid.NewGuid().ToString(),f.Clock.Utc)]);}
}
internal sealed class SupplierPortalFixture(BidPhase1Checks.Fixture f):ISupplierTenderPortal
{
    public bool Pending;
    public async Task<SupplierPortalPage> ReadAsync(ClaimsPrincipal user,Guid id,CancellationToken ct){var pages=new SupplierBidPages(f.Store,f.Service,f.Clock);await pages.RequireSupplierAsync(user,ct);BidRules.Require(user.FindFirstValue(ClaimTypes.NameIdentifier)=="supplier"&&id==f.Context.Tender.Id,404,"Tender not found.");var existing=(await pages.ListAsync(user,ct)).SingleOrDefault();var offer=existing is null?null:await pages.DetailsAsync(user,existing.Bid.Id,ct);var invitation=new Invitation{Id=Guid.NewGuid(),TenderSupplierId=f.Store.Participation.Link.Id,Status=Pending?InvitationStatus.Pending:InvitationStatus.Accepted};return new(new(f.Context.Tender,new(),f.Context.Items,[],"version",new("supplier",false,false,false,false,f.Account.SupplierId)),new(invitation,f.Context.Tender,new(){Id=f.Account.SupplierId!.Value,CompanyName="Synthetic supplier"}),offer,Pending,offer is null&&!Pending,null);}
}
