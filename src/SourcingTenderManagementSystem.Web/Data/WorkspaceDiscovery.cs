using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Work;
using SourcingTenderManagementSystem.Web.Security;
using SourcingTenderManagementSystem.Web.Models.Tenders;
namespace SourcingTenderManagementSystem.Web.Data;

public interface IWorkspaceDiscovery
{
    Task<WorkPage> ReadAsync(ClaimsPrincipal user, bool tasks, string? query, string? category, int page, CancellationToken ct);
    Task<AuditPage> AuditAsync(ClaimsPrincipal user, string? query, string? type, DateOnly? from, DateOnly? to, int page, CancellationToken ct);
}
public sealed record WorkQuery(string Key, string Title, string Controller, string Action, string Parameter, string StateKind, IQueryable<WorkRecord> Rows);

// Read-only discovery. Result links never grant permission; existing destination services remain authoritative.
public sealed class WorkspaceDiscovery(ApplicationDbContext db, IBidData accounts, BidData visibility, TimeProvider clock) : IWorkspaceDiscovery
{
    public static bool Eligible(BidAccount? a) => a is not null && (a.Supplier || ReportRules.Admin(a) || a.Internal && a.Roles.Intersect(ApplicationRoles.EmployeeRoles).Any());
    private async Task<BidAccount> Actor(ClaimsPrincipal user, CancellationToken ct)
    {
        var a = await accounts.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(Eligible(a), 403, "An eligible signed-in account is required."); return a!;
    }
    public IQueryable<Tender> VisibleTenders(BidAccount a, DateTimeOffset now)
    {
        var q = db.Tenders.AsNoTracking();
        if (a.Supplier) return q.Where(t => t.Status != TenderStatus.Draft && db.TenderSuppliers.Any(l => l.TenderId == t.Id && l.SupplierProfileId == a.SupplierId && db.Invitations.Any(i => i.TenderSupplierId == l.Id)));
        if (ReportRules.Admin(a) || a.Internal && a.Roles.Any(r => r == ApplicationRoles.SourcingOfficer || r == ApplicationRoles.SourcingManager)) return q;
        if (!a.Internal) return q.Where(t => false);
        var requester = a.Roles.Contains(ApplicationRoles.RequestingDepartment);
        var evaluator = a.Roles.Contains(ApplicationRoles.EvaluationCommittee);
        var top = a.Roles.Contains(ApplicationRoles.TopCommittee);
        return q.Where(t => requester && db.TenderPurchaseRequisitions.Any(l => l.TenderId == t.Id && db.PurchaseRequisitions.Any(r => r.Id == l.PurchaseRequisitionId && r.CreatedByUserId == a.UserId)) ||
            BidRules.ClosedStages.Contains(t.Status) && db.CommitteeMembers.Any(m => m.TenderId == t.Id && m.UserId == a.UserId && (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo) &&
                db.CommitteeAssignments.Any(c => c.Id == m.CommitteeAssignmentId && c.TenderId == t.Id && (evaluator && c.Kind == CommitteeKind.Evaluation || top && c.Kind == CommitteeKind.TopCommittee && (t.Status == TenderStatus.AwaitingCommitteeDecision || t.Status == TenderStatus.Awarded)))));
    }
    public IQueryable<PurchaseRequisition> VisibleRequests(BidAccount a)
    {
        var q = db.PurchaseRequisitions.AsNoTracking();
        if (ReportRules.Admin(a) || a.Internal && a.Roles.Contains(ApplicationRoles.SourcingOfficer)) return q;
        return a.Internal && a.Roles.Contains(ApplicationRoles.RequestingDepartment) ? q.Where(r => r.CreatedByUserId == a.UserId) : q.Where(r => false);
    }
    public IQueryable<AuditEntry> SafeAudit()
    {
        // Confidential record types and all free text, identities, status payloads and reasons are excluded in SQL.
        return db.AuditLogs.AsNoTracking().Where(a => a.RecordType == "PurchaseRequisition" || a.RecordType == "Tender" || a.RecordType == "Department" || a.RecordType == "Employee" || a.RecordType == "SupplierProfile")
            .Select(a => new AuditEntry {Id=a.Id,Action=a.Action,RecordType=a.RecordType,RecordId=a.RecordId,At=a.OccurredAt});
    }
    public IReadOnlyList<WorkQuery> Queries(BidAccount a, Guid? department, bool tasks, DateTimeOffset now)
    {
        var result = new List<WorkQuery>();
        void Add(string key, string title, string controller, string action, string state, IQueryable<WorkRecord> rows, string parameter = "id") => result.Add(new(key,title,controller,action,parameter,state,rows));
        bool Role(string r) => a.Roles.Contains(r);
        var admin = ReportRules.Admin(a);
        var officer = a.Internal && Role(ApplicationRoles.SourcingOfficer);
        var manager = a.Internal && Role(ApplicationRoles.SourcingManager);
        var requests = VisibleRequests(a);
        if (!tasks && (admin || officer || a.Internal && Role(ApplicationRoles.RequestingDepartment)))
            Add("requests","Purchase requisitions","Requisitions","Details","request",requests.Select(r=>new WorkRecord{Id=r.Id.ToString(),Title=r.Title,State=r.Status.ToString(),At=r.CreatedAt}));
        if (tasks && a.Internal && Role(ApplicationRoles.RequestingDepartment) && !officer)
            Add("request-drafts","Complete your requisitions","Requisitions","Edit","request",requests.Where(r=>r.CreatedByUserId==a.UserId && (r.Status==PurchaseRequestStatus.Draft || r.Status==PurchaseRequestStatus.Returned)).Select(r=>new WorkRecord{Id=r.Id.ToString(),Title=r.Title,State=r.Status.ToString(),At=r.CreatedAt}));
        if (tasks && officer && department.HasValue)
            Add("request-review","Review purchase requisitions","Requisitions","Details","request",requests.Where(r=>r.CreatedByUserId!=a.UserId && r.DepartmentId!=department && (r.Status==PurchaseRequestStatus.Submitted || r.Status==PurchaseRequestStatus.UnderReview)).Select(r=>new WorkRecord{Id=r.Id.ToString(),Title=r.Title,State=r.Status.ToString(),At=r.SubmittedAt}));
        var tenders=VisibleTenders(a,now);
        if (!tasks)
        {
            // Evaluation-only accounts use the authorized review pages, never the internal tender route.
            var internalTender = admin || officer || manager || a.Supplier || Role(ApplicationRoles.RequestingDepartment);
            if (internalTender) Add("tenders","Tenders","Tenders","Details","tender",tenders.Where(t=>admin||officer||manager||a.Supplier||db.TenderPurchaseRequisitions.Any(l=>l.TenderId==t.Id&&db.PurchaseRequisitions.Any(r=>r.Id==l.PurchaseRequisitionId&&r.CreatedByUserId==a.UserId))).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
        }
        if (tasks && officer)
        {
            Add("tender-drafts","Prepare draft tenders","Tenders","Edit","tender",tenders.Where(t=>t.Status==TenderStatus.Draft).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
            Add("tender-published","Manage published tenders","Tenders","Details","tender",tenders.Where(t=>t.Status==TenderStatus.Published).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
            Add("tender-close","Open tenders past their deadline","Tenders","Details","tender",tenders.Where(t=>t.Status==TenderStatus.Open&&t.SubmissionDeadline<=now).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
            Add("compliance","Review closed tender offers","ReviewBids","Index","tender",tenders.Where(t=>t.Status==TenderStatus.Closed).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}),"tenderId");
            Add("final-work","Manage final-decision rounds and awards","TopCommittee","Details","tender",tenders.Where(t=>t.CreatedByUserId==a.UserId&&t.Status==TenderStatus.AwaitingCommitteeDecision).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
            Add("convert","Approved requisitions ready for conversion","Tenders","Create","request",requests.Where(r=>r.Status==PurchaseRequestStatus.Approved&&!db.TenderPurchaseRequisitions.Any(l=>l.PurchaseRequisitionId==r.Id)).Select(r=>new WorkRecord{Id=r.Id.ToString(),Title=r.Title,State=r.Status.ToString(),At=r.CreatedAt}),"requisitionId");
        }
        if(tasks&&manager)
        {
            var criteria=db.EvaluationCriteria.AsNoTracking();
            Add("criteria","Review evaluation criteria","Tenders","Criteria","tender",tenders.Where(t=>(t.Status==TenderStatus.Draft||t.Status==TenderStatus.Published)&&criteria.Any(c=>c.TenderId==t.Id)&&!criteria.Any(c=>c.TenderId==t.Id&&c.ApprovedAt!=null)&&!db.AuditLogs.Any(log=>log.RecordType=="EvaluationCriterion"&&log.Action=="Criteria.Configured"&&log.ActorUserId==a.UserId&&criteria.Any(c=>c.TenderId==t.Id&&c.Id.ToString()==log.RecordId))).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
            Add("reports","Review submitted committee reports","UnifiedReports","Review","tender",tenders.Where(t=>t.Status==TenderStatus.AwaitingManagerReview&&db.CommitteeMembers.Any(m=>m.TenderId==t.Id&&m.UserId==a.UserId&&m.Position==CommitteeRules.Positions[0]&&(m.EffectiveFrom==null||m.EffectiveFrom<=now)&&(m.EffectiveTo==null||now<m.EffectiveTo)&&db.CommitteeAssignments.Any(c=>c.Id==m.CommitteeAssignmentId&&c.TenderId==t.Id&&c.Kind==CommitteeKind.Evaluation))).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
        }
        var offers=visibility.VisibleQuery(a,now);
        if(!tasks&&(a.Supplier||a.Internal&&a.Roles.Any(r=>r==ApplicationRoles.SourcingOfficer||r==ApplicationRoles.SourcingManager||r==ApplicationRoles.EvaluationCommittee||r==ApplicationRoles.TopCommittee)))
            Add("offers",a.Supplier?"Your offers":"Authorized submitted offers",a.Supplier?"Bids":"ReviewBids","Details","bid",offers.Join(db.Tenders,b=>b.TenderId,t=>t.Id,(b,t)=>new WorkRecord{Id=b.Id.ToString(),Title=t.Title,State=b.Status.ToString(),At=b.SubmittedAt}));
        if(a.Supplier)
        {
            var invitations=from i in db.Invitations.AsNoTracking() join l in db.TenderSuppliers on i.TenderSupplierId equals l.Id join t in tenders on l.TenderId equals t.Id where l.SupplierProfileId==a.SupplierId select new {i,l,t};
            if(tasks) invitations=invitations.Where(x=>x.i.Status==InvitationStatus.Pending&&(x.i.ExpiresAt==null||now<x.i.ExpiresAt)&&(x.t.Status==TenderStatus.Published||x.t.Status==TenderStatus.Open)&&now<x.t.SubmissionDeadline);
            Add("invitations",tasks?"Respond to invitations":"Your invitations","SupplierPortal","Details","invitation",invitations.Select(x=>new WorkRecord{Id=x.t.Id.ToString(),Title=x.t.Title,State=x.i.Status.ToString(),At=x.t.SubmissionDeadline}));
            if(tasks){
                Add("offer-drafts","Complete your draft offers","Bids","Edit","bid",offers.Where(b=>b.Status==BidStatus.Draft&&db.Tenders.Any(t=>t.Id==b.TenderId&&t.Status==TenderStatus.Open&&t.SubmissionStart<=now&&now<t.SubmissionDeadline)&&db.Invitations.Any(i=>i.TenderSupplierId==b.TenderSupplierId&&i.Status==InvitationStatus.Accepted&&(i.ExpiresAt==null||now<i.ExpiresAt))).Join(db.Tenders,b=>b.TenderId,t=>t.Id,(b,t)=>new WorkRecord{Id=b.Id.ToString(),Title=t.Title,State=b.Status.ToString(),At=t.SubmissionDeadline}));
                Add("offer-create","Accepted invitations ready for an offer","SupplierPortal","Details","invitation",(from i in db.Invitations.AsNoTracking() join l in db.TenderSuppliers on i.TenderSupplierId equals l.Id join t in tenders on l.TenderId equals t.Id where l.SupplierProfileId==a.SupplierId&&i.Status==InvitationStatus.Accepted&&(i.ExpiresAt==null||now<i.ExpiresAt)&&t.Status==TenderStatus.Open&&t.SubmissionStart<=now&&now<t.SubmissionDeadline&&!db.Bids.Any(b=>b.TenderSupplierId==l.Id) select new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=i.Status.ToString(),At=t.SubmissionDeadline}));
            }
        }
        if(a.Internal&&(manager||Role(ApplicationRoles.EvaluationCommittee)))
        {
            var assigned=tenders.Where(t=>t.Status==TenderStatus.UnderEvaluation&&db.CommitteeMembers.Any(m=>m.TenderId==t.Id&&m.UserId==a.UserId&&(m.EffectiveFrom==null||m.EffectiveFrom<=now)&&(m.EffectiveTo==null||now<m.EffectiveTo)&&db.CommitteeAssignments.Any(c=>c.Id==m.CommitteeAssignmentId&&c.TenderId==t.Id&&c.Kind==CommitteeKind.Evaluation)));
            if(tasks) Add("evaluate","Assigned evaluations and committee reports","ReviewBids","Index","tender",assigned.Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}),"tenderId");
            else if(!manager) Add("evaluations","Assigned tender evaluations","ReviewBids","Index","tender",tenders.Where(t=>db.CommitteeMembers.Any(m=>m.TenderId==t.Id&&m.UserId==a.UserId&&(m.EffectiveFrom==null||m.EffectiveFrom<=now)&&(m.EffectiveTo==null||now<m.EffectiveTo)&&db.CommitteeAssignments.Any(c=>c.Id==m.CommitteeAssignmentId&&c.TenderId==t.Id&&c.Kind==CommitteeKind.Evaluation))).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}),"tenderId");
        }
        if(a.Internal&&Role(ApplicationRoles.TopCommittee)) Add("decisions","Assigned final decisions","TopCommittee","Details","tender",tenders.Where(t=>(t.Status==TenderStatus.AwaitingCommitteeDecision||!tasks&&t.Status==TenderStatus.Awarded)&&db.CommitteeMembers.Any(m=>m.TenderId==t.Id&&m.UserId==a.UserId&&(m.EffectiveFrom==null||m.EffectiveFrom<=now)&&(m.EffectiveTo==null||now<m.EffectiveTo)&&db.CommitteeAssignments.Any(c=>c.Id==m.CommitteeAssignmentId&&c.TenderId==t.Id&&c.Kind==CommitteeKind.TopCommittee))).Select(t=>new WorkRecord{Id=t.Id.ToString(),Title=t.Title,State=t.Status.ToString(),At=t.SubmissionDeadline}));
        if(admin)
        {
            var suppliers=db.SupplierProfiles.AsNoTracking();if(tasks)suppliers=suppliers.Where(s=>s.Status==SupplierStatus.Pending);
            Add("suppliers",tasks?"Review supplier applications":"Supplier companies","Suppliers","Details","supplier",suppliers.Select(s=>new WorkRecord{Id=s.Id.ToString(),Title=s.CompanyName,State=s.Status.ToString(),At=s.ReviewedAt}));
            if(!tasks){
                Add("departments","Departments","Departments","Index","none",db.Departments.AsNoTracking().Select(d=>new WorkRecord{Id=d.Id.ToString(),Title=d.Name,At=null}));
                var employees=db.Users.AsNoTracking().Where(u=>u.NormalizedUserName!="ADMIN"&&!db.SupplierProfiles.Any(s=>s.UserId==u.Id)&&!db.UserRoles.Any(ur=>ur.UserId==u.Id&&db.Roles.Any(r=>r.Id==ur.RoleId&&r.Name==ApplicationRoles.Supplier)));
                Add("employees","Employee accounts","Employees","Edit","none",employees.Select(u=>new WorkRecord{Id=u.Id,Title=u.UserName??"Employee account",At=null}));
            }
        }
        return result;
    }
    public async Task<WorkPage> ReadAsync(ClaimsPrincipal user,bool tasks,string? query,string? category,int page,CancellationToken ct)
    {
        var a=await Actor(user,ct);query=(query??"").Trim();BidRules.Require(query.Length<=160,400,"Search must contain at most 160 characters.");
        page=Math.Clamp(page,1,100000);category=string.IsNullOrWhiteSpace(category)?null:category;
        if(!tasks&&query.Length<2)return new(false,query,category,page,[]);
        var department=await db.EmployeeDepartments.AsNoTracking().Where(x=>x.UserId==a.UserId).Select(x=>(Guid?)x.DepartmentId).SingleOrDefaultAsync(ct);
        var queries=Queries(a,department,tasks,clock.GetUtcNow());
        BidRules.Require(category is null||queries.Any(g=>g.Key==category),400,"Choose an available category.");
        var groups=new List<WorkGroup>();
        foreach(var g in queries.Where(g=>category is null||g.Key==category)){
            var rows=g.Rows;if(query.Length>0)rows=rows.Where(r=>r.Title.Contains(query)||r.Id==query);
            var count=await rows.LongCountAsync(ct);
            var ordered=tasks?rows.OrderBy(r=>r.At).ThenBy(r=>r.Id):rows.OrderByDescending(r=>r.At).ThenBy(r=>r.Id);
            var items=await ordered.Skip(category is null?0:(page-1)*WorkPage.Size).Take(category is null?6:WorkPage.Size).ToListAsync(ct);
            groups.Add(new(g.Key,g.Title,g.Controller,g.Action,g.Parameter,g.StateKind,items,count));
        }
        var fresh=await Actor(user,ct);BidRules.Require(fresh==a||fresh.UserId==a.UserId&&fresh.Roles.Order().SequenceEqual(a.Roles.Order())&&fresh.SupplierId==a.SupplierId&&fresh.EvaluationTenders.Order().SequenceEqual(a.EvaluationTenders.Order())&&fresh.TopTenders.Order().SequenceEqual(a.TopTenders.Order()),403,"Account access changed. Reload the page.");
        return new(tasks,query,category,page,groups);
    }
    public async Task<AuditPage> AuditAsync(ClaimsPrincipal user,string? query,string? type,DateOnly? from,DateOnly? to,int page,CancellationToken ct)
    {
        BidRules.Require(ReportRules.Admin(await Actor(user,ct)),403,"Administrator access is required.");
        query=(query??"").Trim();BidRules.Require(query.Length<=160,400,"Search must contain at most 160 characters.");
        BidRules.Require(!from.HasValue||!to.HasValue||from<=to,400,"The end date must follow the start date.");
        page=Math.Clamp(page,1,100000);var q=SafeAudit();
        if(query.Length>0)q=q.Where(a=>a.Action.Contains(query)||a.RecordId==query);
        if(!string.IsNullOrWhiteSpace(type))q=q.Where(a=>a.RecordType==type);
        if(from.HasValue){var at=TenderTime.Utc(from.Value.ToDateTime(TimeOnly.MinValue));q=q.Where(a=>a.At>=at);}
        if(to.HasValue&&to.Value<DateOnly.MaxValue){var at=TenderTime.Utc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));q=q.Where(a=>a.At<at);}
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(a=>a.At).ThenBy(a=>a.Id).Skip((page-1)*WorkPage.Size).Take(WorkPage.Size).ToListAsync(ct);
        BidRules.Require(ReportRules.Admin(await Actor(user,ct)),403,"Administrator access changed.");
        return new(query,type,from,to,page,total,rows);
    }
}
