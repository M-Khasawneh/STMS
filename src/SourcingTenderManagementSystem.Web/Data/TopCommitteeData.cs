using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.TopCommittee;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed class TopUnit {
 public Tender Tender {get;set;}=new();
 public PurchaseRequisition Source {get;set;}=new();
 public List<CommitteeAssignment> Assignments {get;set;}=[];
 public List<CommitteeMember> Members {get;set;}=[];
 public List<EvaluationReport> Reports {get;set;}=[];
 public List<TenderApproval> Approvals {get;set;}=[];
 public List<CommitteeVote> Votes {get;set;}=[];
 public List<TenderAward> Awards {get;set;}=[];
 public List<Bid> Bids {get;set;}=[];
 public Dictionary<string,string> MemberNames {get;set;}=[];
 public Dictionary<Guid,string> Companies {get;set;}=[];
 public List<TopCandidate> People {get;set;}=[];
 public HashSet<string> Excluded {get;set;}=[];
 public List<AuditLog> History {get;set;}=[];
 public List<AuditLog> NewAudits {get;}=[];
 public List<Notification> NewNotifications {get;}=[];
 public Dictionary<string,string> Originals {get;set;}=[];
 public IEnumerable<object> Entities()=>new object[]{Tender}.Concat(Assignments).Concat(Members).Concat(Reports).Concat(Approvals).Concat(Votes).Concat(Awards);
 public static string Key(object value)=>value.GetType().Name+":"+value.GetType().GetProperty("Id")!.GetValue(value);
 public void Capture()=>Originals=Entities().ToDictionary(Key,e=>JsonSerializer.Serialize(e,e.GetType()));
}
public interface ITopCommitteeData {
 Task<BidAccount?> AccountAsync(ClaimsPrincipal user,CancellationToken ct);
 Task<IBidTransaction> BeginAsync(Guid tenderId,CancellationToken ct);
 Task<TopUnit> LoadAsync(Guid tenderId,CancellationToken ct);
 Task<IReadOnlyList<TopTenderRow>> ListAsync(BidAccount actor,CancellationToken ct);
 Task SaveAsync(TopUnit state,CancellationToken ct);
 Task<bool> SupplierEligibleAsync(Guid bidId,CancellationToken ct);
 Task<IReadOnlyList<string>> InvitedUsersAsync(Guid id,CancellationToken ct);
 Task<TenderOutcome> OutcomeAsync(BidAccount actor,Guid id,CancellationToken ct);
}
public sealed class TopCommitteeData(ApplicationDbContext db,IBidData bids,TimeProvider clock):ITopCommitteeData {
 public Task<BidAccount?> AccountAsync(ClaimsPrincipal user,CancellationToken ct)=>bids.AccountAsync(user,clock.GetUtcNow(),ct);
 public Task<IBidTransaction> BeginAsync(Guid id,CancellationToken ct)=>bids.BeginAsync(id,ct);
 public async Task<TopUnit> LoadAsync(Guid id,CancellationToken ct) {
  var s=new TopUnit {Tender=await db.Tenders.AsNoTracking().SingleOrDefaultAsync(t=>t.Id==id,ct)??throw new BidException(404,"Tender not found.")};
  s.Source=await(from l in db.TenderPurchaseRequisitions join q in db.PurchaseRequisitions.AsNoTracking() on l.PurchaseRequisitionId equals q.Id where l.TenderId==id select q).SingleOrDefaultAsync(ct)??throw new BidException(409,"Tender provenance is unavailable.");
  s.Assignments=await db.CommitteeAssignments.AsNoTracking().Where(a=>a.TenderId==id).ToListAsync(ct);s.Members=await db.CommitteeMembers.AsNoTracking().Where(m=>m.TenderId==id).ToListAsync(ct);
  s.Reports=await db.EvaluationReports.AsNoTracking().Where(r=>r.TenderId==id).ToListAsync(ct);s.Approvals=await db.TenderApprovals.AsNoTracking().Where(a=>a.TenderId==id).ToListAsync(ct);
  s.Votes=await db.CommitteeVotes.AsNoTracking().Where(v=>v.TenderId==id).ToListAsync(ct);s.Awards=await db.TenderAwards.AsNoTracking().Where(a=>a.TenderId==id).ToListAsync(ct);
  s.Bids=await db.Bids.AsNoTracking().Where(b=>b.TenderId==id).ToListAsync(ct);
  s.Companies=await(from b in db.Bids join l in db.TenderSuppliers on b.TenderSupplierId equals l.Id join p in db.SupplierProfiles on l.SupplierProfileId equals p.Id where b.TenderId==id select new{b.Id,p.CompanyName}).ToDictionaryAsync(x=>x.Id,x=>x.CompanyName,ct);
  var memberUsers=s.Members.Select(m=>m.UserId).Distinct().ToArray();s.MemberNames=await db.Users.AsNoTracking().Where(u=>memberUsers.Contains(u.Id)).ToDictionaryAsync(u=>u.Id,u=>u.UserName??u.Id,ct);
  var now=clock.GetUtcNow();var users=await(from u in db.Users.AsNoTracking() join e in db.EmployeeDepartments on u.Id equals e.UserId where(!u.LockoutEnabled||u.LockoutEnd==null||u.LockoutEnd<=now)&&!db.SupplierProfiles.Any(p=>p.UserId==u.Id) orderby u.Id select new{u.Id,Name=u.UserName??u.Id}).ToListAsync(ct);
  var roles=await(from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id orderby r.Name select new{ur.UserId,r.Name}).ToListAsync(ct);
  s.People=users.Select(u=>new TopCandidate(new(u.Id,roles.Where(r=>r.UserId==u.Id).Select(r=>r.Name!).ToArray(),null,false,true,true,[],[]),u.Name)).Where(p=>p.Account.Internal).ToList();
  s.Excluded.Add(s.Tender.CreatedByUserId);
  foreach(var m in s.Members.Where(m=>s.Assignments.Any(a=>a.Id==m.CommitteeAssignmentId&&a.Kind==CommitteeKind.Evaluation)))s.Excluded.Add(m.UserId);
  foreach(var a in s.Approvals.Where(a=>a.Stage==ApprovalStage.Manager&&a.ActorUserId!=null))s.Excluded.Add(a.ActorUserId!);
  foreach(var r in s.Reports)s.Excluded.Add(r.PreparedByUserId);
  var preparers=await db.AuditLogs.AsNoTracking().Where(a=>a.RecordType=="Tender"&&a.RecordId==id.ToString()&&a.ActorUserId!=null&&(a.Action=="Tender.Created"||a.Action=="Tender.Edited"||a.Action=="Tender.CriteriaSaved")).Select(a=>a.ActorUserId!).ToListAsync(ct);s.Excluded.UnionWith(preparers);
  s.Excluded.UnionWith(await db.TechnicalEvaluations.AsNoTracking().Where(e=>e.TenderId==id).Select(e=>e.RecordedByUserId).ToListAsync(ct));s.Excluded.UnionWith(await db.FinancialEvaluations.AsNoTracking().Where(e=>e.TenderId==id).Select(e=>e.RecordedByUserId).ToListAsync(ct));
  s.Excluded.UnionWith(await db.TechnicalEvaluations.AsNoTracking().Where(e=>e.TenderId==id&&e.EvaluatorUserId!=null).Select(e=>e.EvaluatorUserId!).ToListAsync(ct));s.Excluded.UnionWith(await db.FinancialEvaluations.AsNoTracking().Where(e=>e.TenderId==id&&e.EvaluatorUserId!=null).Select(e=>e.EvaluatorUserId!).ToListAsync(ct));
  s.History=await db.AuditLogs.AsNoTracking().Where(a=>a.RecordType=="TopCommittee"&&a.RecordId==id.ToString()).OrderBy(a=>a.OccurredAt).ThenBy(a=>a.Id).ToListAsync(ct);
  s.Capture();return s;
 }
 public async Task<IReadOnlyList<TopTenderRow>> ListAsync(BidAccount a,CancellationToken ct) {
  var now=clock.GetUtcNow();var manager=a.Internal&&a.Roles.Contains(ApplicationRoles.SourcingManager);var officer=a.Internal&&a.Roles.Contains(ApplicationRoles.SourcingOfficer);var top=a.Internal&&a.Roles.Contains(ApplicationRoles.TopCommittee);
  return await db.Tenders.AsNoTracking().Where(t=>BidRules.ClosedStages.Contains(t.Status)&&(
   officer&&t.CreatedByUserId==a.UserId||manager&&db.CommitteeMembers.Any(m=>m.TenderId==t.Id&&m.UserId==a.UserId&&m.Position=="Sourcing Manager"&&(m.EffectiveFrom==null||m.EffectiveFrom<=now)&&(m.EffectiveTo==null||now<m.EffectiveTo)&&db.CommitteeAssignments.Any(c=>c.Id==m.CommitteeAssignmentId&&c.TenderId==m.TenderId&&c.Kind==CommitteeKind.Evaluation))||
   top&&(t.Status==TenderStatus.AwaitingCommitteeDecision||t.Status==TenderStatus.Awarded)&&db.CommitteeMembers.Any(m=>m.TenderId==t.Id&&m.UserId==a.UserId&&(m.EffectiveFrom==null||m.EffectiveFrom<=now)&&(m.EffectiveTo==null||now<m.EffectiveTo)&&db.CommitteeAssignments.Any(c=>c.Id==m.CommitteeAssignmentId&&c.TenderId==m.TenderId&&c.Kind==CommitteeKind.TopCommittee))))
   .OrderBy(t=>t.Title).Select(t=>new TopTenderRow(t.Id,t.Title,t.Status)).ToListAsync(ct);
 }
 public async Task<bool> SupplierEligibleAsync(Guid bidId,CancellationToken ct){
  var supplier=await(from b in db.Bids join l in db.TenderSuppliers on new{Id=b.TenderSupplierId,b.TenderId} equals new{l.Id,l.TenderId} join p in db.SupplierProfiles on l.SupplierProfileId equals p.Id where b.Id==bidId&&db.Invitations.Any(i=>i.TenderSupplierId==l.Id&&i.Status==InvitationStatus.Accepted) select p.UserId).SingleOrDefaultAsync(ct);
  if(supplier is null)return false;var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,supplier)],"AwardEligibility"));return (await bids.AccountAsync(principal,clock.GetUtcNow(),ct))?.Supplier==true;
 }
 public async Task<IReadOnlyList<string>> InvitedUsersAsync(Guid id,CancellationToken ct)=>await(from i in db.Invitations join l in db.TenderSuppliers on i.TenderSupplierId equals l.Id join p in db.SupplierProfiles on l.SupplierProfileId equals p.Id where l.TenderId==id select p.UserId).Distinct().ToListAsync(ct);
 public async Task SaveAsync(TopUnit s,CancellationToken ct) {
  BidRules.Require(db.Database.CurrentTransaction!=null,409,"A locked transaction is required.");
  var additions=new List<object>();
  foreach(var entity in s.Entities()){
   var type=entity.GetType();var json=JsonSerializer.Serialize(entity,type);if(!s.Originals.TryGetValue(TopUnit.Key(entity),out var original)){additions.Add(entity);continue;}if(json==original)continue;
   string[] allowed=entity switch{Tender=>["Status"],CommitteeMember=>["EffectiveTo"],TenderApproval approval when approval.Stage==ApprovalStage.TopCommittee=>["Decision","DecidedAt","RoundClosedAt","RoundCloseReason","Comments","Justification","ActorUserId"],_=>[]};
   using var before=JsonDocument.Parse(original);using var after=JsonDocument.Parse(json);var changed=after.RootElement.EnumerateObject().Where(p=>p.Value.GetRawText()!=before.RootElement.GetProperty(p.Name).GetRawText()).Select(p=>p.Name).ToArray();
   BidRules.Require(changed.All(allowed.Contains),409,"An immutable historical record was changed.");db.Attach(entity);foreach(var name in changed)db.Entry(entity).Property(name).IsModified=true;
  }
  // Close the old filtered-unique open round before inserting its replacement.
  // Both flushes remain inside the same serializable transaction and roll back together.
  await db.SaveChangesAsync(ct);
  foreach(var entity in additions)db.Add(entity);
  db.AuditLogs.AddRange(s.NewAudits);db.Notifications.AddRange(s.NewNotifications);await db.SaveChangesAsync(ct);
 }
 public async Task<TenderOutcome> OutcomeAsync(BidAccount a,Guid id,CancellationToken ct) {
  var admin=ReportRules.Admin(a);var requester=a.Internal&&a.Roles.Contains(ApplicationRoles.RequestingDepartment);var broad=a.Internal&&(a.Roles.Contains(ApplicationRoles.SourcingOfficer)||a.Roles.Contains(ApplicationRoles.SourcingManager));
  var q=db.Tenders.AsNoTracking().Where(t=>t.Id==id&&(admin||broad||requester&&db.TenderPurchaseRequisitions.Any(l=>l.TenderId==t.Id&&db.PurchaseRequisitions.Any(r=>r.Id==l.PurchaseRequisitionId&&r.CreatedByUserId==a.UserId))||
   a.Supplier&&t.Status!=TenderStatus.Draft&&db.TenderSuppliers.Any(l=>l.TenderId==t.Id&&l.SupplierProfileId==a.SupplierId&&db.Invitations.Any(i=>i.TenderSupplierId==l.Id))||a.Internal&&(a.Roles.Contains(ApplicationRoles.EvaluationCommittee)&&a.EvaluationTenders.Contains(t.Id)&&BidRules.ClosedStages.Contains(t.Status)||a.Roles.Contains(ApplicationRoles.TopCommittee)&&a.TopTenders.Contains(t.Id)&&(t.Status==TenderStatus.AwaitingCommitteeDecision||t.Status==TenderStatus.Awarded))));
  var row=await q.Select(t=>new{t.Id,t.Title,t.Status,At=db.TenderAwards.Where(w=>w.TenderId==t.Id).Select(w=>(DateTimeOffset?)w.AwardedAt).SingleOrDefault(),OwnWon=a.Supplier&&db.TenderAwards.Any(w=>w.TenderId==t.Id&&db.Bids.Any(b=>b.Id==w.BidId&&db.TenderSuppliers.Any(l=>l.Id==b.TenderSupplierId&&l.TenderId==t.Id&&l.SupplierProfileId==a.SupplierId))),OwnSubmitted=a.Supplier&&db.Bids.Any(b=>b.TenderId==t.Id&&b.SubmittedAt!=null&&db.TenderSuppliers.Any(l=>l.Id==b.TenderSupplierId&&l.SupplierProfileId==a.SupplierId))}).SingleOrDefaultAsync(ct)??throw new BidException(404,"Tender outcome not found.");
  return new(row.Id,row.Title,row.Status,row.At is null?"No final award has been recorded.":!a.Supplier?"The final award has been recorded.":row.OwnWon?"Your submitted offer was selected.":row.OwnSubmitted?"Your submitted offer was not selected.":"The tender was awarded. You did not submit an offer.",row.At);
 }
}
