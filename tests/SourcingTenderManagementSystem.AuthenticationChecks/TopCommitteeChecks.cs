using System.Security.Claims;
using System.Text.Json;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.TopCommittee;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

// The production service and compiled MVC views run against a serializable memory boundary only.
internal sealed class MemoryTopCommitteeData : ITopCommitteeData {
 internal static readonly Guid Id = Guid.Parse("10000000-0000-0000-0000-000000000099");
 internal TopUnit State = new(); private TopUnit? pending;
 internal readonly Dictionary<string,BidAccount> Accounts = [];
 internal readonly List<Notification> Notifications=[];
 private readonly SemaphoreSlim gate=new(1,1); private List<Notification> staged=[];
 internal bool FailSave, SupplierEligible=true;
 public MemoryTopCommitteeData(){Reset();}
 internal void Reset(){
  Accounts.Clear();Notifications.Clear();FailSave=false;SupplierEligible=true;
  foreach(var pair in new[]{("manager",ApplicationRoles.SourcingManager),("officer",ApplicationRoles.SourcingOfficer),("evaluator",ApplicationRoles.EvaluationCommittee),("requester",ApplicationRoles.RequestingDepartment),("admin",ApplicationRoles.Admin),("top",ApplicationRoles.TopCommittee),("top2",ApplicationRoles.TopCommittee),("top3",ApplicationRoles.TopCommittee),("top4",ApplicationRoles.TopCommittee),("top5",ApplicationRoles.TopCommittee),("unrelatedtop",ApplicationRoles.TopCommittee),("otherofficer",ApplicationRoles.SourcingOfficer),("othermanager",ApplicationRoles.SourcingManager)}) Accounts[pair.Item1]=new(pair.Item1,[pair.Item2],null,false,true,true,[],[Id]);
  Accounts["supplier"]=new("supplier",[ApplicationRoles.Supplier],Guid.NewGuid(),true,true,false,[],[]);
  Accounts["other"]=Accounts["supplier"] with{UserId="other",SupplierId=Guid.NewGuid()};
  var now=DateTimeOffset.UtcNow.AddDays(-1);var eval=Guid.NewGuid();var report=Guid.NewGuid();var bid=Guid.NewGuid();
  State=new(){Tender=new(){Id=Id,Title="Committee fixture tender",CreatedByUserId="officer",Status=TenderStatus.AwaitingCommitteeDecision},Source=new(){Id=Guid.NewGuid(),CreatedByUserId="requester"},
   Assignments=[new(){Id=eval,TenderId=Id,Kind=CommitteeKind.Evaluation}],
   Members=[new(){Id=Guid.NewGuid(),TenderId=Id,CommitteeAssignmentId=eval,UserId="manager",Position=CommitteeRules.Positions[0],EffectiveFrom=now},new(){Id=Guid.NewGuid(),TenderId=Id,CommitteeAssignmentId=eval,UserId="evaluator",Position=CommitteeRules.Positions[1],EffectiveFrom=now}],
   Reports=[new(){Id=report,TenderId=Id,PreparedByUserId="evaluator",RecommendedBidId=bid,SubmittedAt=now,Recommendation="Preserved recommendation",Comparison="preserved snapshot"}],
   Approvals=[new(){Id=Guid.NewGuid(),TenderId=Id,EvaluationReportId=report,Stage=ApprovalStage.Manager,Decision=DecisionStatus.ManagerApproved,ActorUserId="manager",DecidedAt=now}],
   Bids=[new(){Id=bid,TenderId=Id,SubmittedAt=now,Status=BidStatus.Compliant}],Companies=new(){[bid]="Fixture supplier"},Excluded=["officer","manager","evaluator"]};
 }
 internal static ClaimsPrincipal User(string id)=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id)],"MemoryOnly"));
 internal static TopUnit Clone(TopUnit s)=>JsonSerializer.Deserialize<TopUnit>(JsonSerializer.Serialize(s))!;
 public Task<BidAccount?> AccountAsync(ClaimsPrincipal u,CancellationToken ct)=>Task.FromResult(Accounts.GetValueOrDefault(u.FindFirstValue(ClaimTypes.NameIdentifier)??""));
 public async Task<IBidTransaction> BeginAsync(Guid id,CancellationToken ct){await gate.WaitAsync(ct);pending=Clone(State);staged=[];return new Transaction(this);}
 public Task<TopUnit> LoadAsync(Guid id,CancellationToken ct){if(id!=Id)throw new BidException(404,"Not found");var s=Clone(pending??State);s.People=Accounts.Values.Where(a=>a.Internal).Select(a=>new TopCandidate(a,a.UserId)).ToList();s.MemberNames=s.Members.Select(m=>m.UserId).Distinct().ToDictionary(u=>u,u=>u);s.Capture();return Task.FromResult(s);}
 public Task<IReadOnlyList<TopTenderRow>> ListAsync(BidAccount a,CancellationToken ct)=>Task.FromResult<IReadOnlyList<TopTenderRow>>([new(Id,State.Tender.Title,State.Tender.Status)]);
 public Task SaveAsync(TopUnit s,CancellationToken ct){if(FailSave)throw new InvalidOperationException("Simulated audit persistence failure");if(pending is null)throw new Exception("Transaction required");pending=Clone(s);pending.History.AddRange(s.NewAudits);staged.AddRange(s.NewNotifications);return Task.CompletedTask;}
 public Task<bool> SupplierEligibleAsync(Guid id,CancellationToken ct)=>Task.FromResult(SupplierEligible);
 public Task<IReadOnlyList<string>> InvitedUsersAsync(Guid id,CancellationToken ct)=>Task.FromResult<IReadOnlyList<string>>(["supplier","other"]);
 public Task<TenderOutcome> OutcomeAsync(BidAccount a,Guid id,CancellationToken ct){if(id!=Id||!(a.Internal||ReportRules.Admin(a)||a.Supplier&&a.UserId is "supplier" or "other"))throw new BidException(404,"No outcome");var s=pending??State;return Task.FromResult(new TenderOutcome(id,s.Tender.Title,s.Tender.Status,s.Awards.Count==0?"No final award has been recorded.":a.Supplier?a.UserId=="supplier"?"Your submitted offer was selected.":"Your submitted offer was not selected.":"The final award has been recorded.",s.Awards.SingleOrDefault()?.AwardedAt));}
 private sealed class Transaction(MemoryTopCommitteeData store):IBidTransaction{
  public Task CommitAsync(CancellationToken ct){store.State=store.pending!;store.Notifications.AddRange(store.staged);return Task.CompletedTask;}
  public ValueTask DisposeAsync(){store.pending=null;store.staged=[];store.gate.Release();return ValueTask.CompletedTask;}
 }
}
internal static class TopCommitteeChecks {
 private static int checks;
 private static void Check(bool ok,string message){if(!ok)throw new Exception("Top service check: "+message);checks++;}
 private static async Task Deny(Func<Task> action,int? status=null){try{await action();throw new Exception("Expected refusal");}catch(BidException e){Check(status is null||e.Status==status,"refusal status "+e.Status);}}
 internal static async Task Prepare(MemoryTopCommitteeData data,ITopCommitteeManagement service){var p=await service.PageAsync(MemoryTopCommitteeData.User("manager"),MemoryTopCommitteeData.Id);await service.AssignAsync(MemoryTopCommitteeData.User("manager"),MemoryTopCommitteeData.Id,new(){Version=p.Version,Users=["top","top2","top3","top4"]});p=await service.PageAsync(MemoryTopCommitteeData.User("manager"),MemoryTopCommitteeData.Id);await service.OpenAsync(MemoryTopCommitteeData.User("manager"),MemoryTopCommitteeData.Id,new(){Version=p.Version,Confirm=true});}
 internal static async Task Rules(){
  checks=0;var d=new MemoryTopCommitteeData();ITopCommitteeManagement s=new TopCommitteeManagement(d,TimeProvider.System);var id=MemoryTopCommitteeData.Id;
  Task<TopPage> Page(string u)=>s.PageAsync(MemoryTopCommitteeData.User(u),id);
  async Task Vote(string u,DecisionStatus c){var p=await Page(u);await s.VoteAsync(MemoryTopCommitteeData.User(u),id,new(){Version=p.Version,RoundId=p.OpenRoundId,Choice=c,Reason="<script>sealed-"+u+"</script>",Confirm=true});}
  foreach(var u in new[]{"admin","supplier","requester","evaluator","unrelatedtop","otherofficer","othermanager"})await Deny(()=>Page(u),403);
  await Deny(()=>s.PageAsync(MemoryTopCommitteeData.User("manager"),Guid.NewGuid()),404);
  var first=await Page("manager");
  await Deny(()=>s.AssignAsync(MemoryTopCommitteeData.User("manager"),id,new(){Version=first.Version,Users=["top","top","top3","top4"]}),400);
  await Deny(()=>s.AssignAsync(MemoryTopCommitteeData.User("manager"),id,new(){Version=first.Version,Users=["top","manager","top3","top4"]}));
  await Prepare(d,s);Check(d.State.Members.Count==6&&d.State.Approvals.Count==2,"appointment and round persisted");
  var stale=await Page("top");await Vote("top",DecisionStatus.CommitteeApproved);
  Check((await Page("manager")).Rounds.Single().Votes.Count==0,"manager cannot see partial choices");Check((await Page("top")).Rounds.Single().Votes.Count==1,"own vote visible");Check((await Page("top2")).Rounds.Single().Votes.Count==0,"peer vote sealed");
  await Deny(()=>s.VoteAsync(MemoryTopCommitteeData.User("top2"),id,new(){Version=stale.Version,RoundId=stale.OpenRoundId,Choice=DecisionStatus.CommitteeApproved,Reason="reason",Confirm=true}),409);
  await Deny(()=>Vote("top",DecisionStatus.CommitteeRejected));
  await Vote("top2",DecisionStatus.CommitteeApproved);await Vote("top3",DecisionStatus.CommitteeApproved);
  Check(d.State.Approvals.Last().Decision==DecisionStatus.RecommendationPending&&!(await Page("officer")).CanAward,"three votes never finalize");
  await Vote("top4",DecisionStatus.CommitteeRejected);Check(d.State.Approvals.Last().DecidedAt==d.State.Approvals.Last().RoundClosedAt,"decision timestamp meets SQL constraint");Check((await Page("manager")).Rounds.Single().Votes.Count==4,"complete votes disclosed");Check((await Page("officer")).CanAward,"majority allows explicit award");
  var p=await Page("officer");var form=new TopActionForm{Version=p.Version,RoundId=p.Rounds.Last().Round.Id,Confirm=true};
  await Deny(()=>s.AwardAsync(MemoryTopCommitteeData.User("manager"),id,form),403);
  d.SupplierEligible=false;await Deny(()=>s.AwardAsync(MemoryTopCommitteeData.User("officer"),id,form),409);Check(d.State.Awards.Count==0,"ineligible supplier rollback");d.SupplierEligible=true;
  d.FailSave=true;try{await s.AwardAsync(MemoryTopCommitteeData.User("officer"),id,form);throw new Exception("Expected persistence failure");}catch(InvalidOperationException){Check(d.State.Awards.Count==0&&d.State.Tender.Status==TenderStatus.AwaitingCommitteeDecision,"atomic rollback on audit failure");}d.FailSave=false;
  var outcomes=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try{await s.AwardAsync(MemoryTopCommitteeData.User("officer"),id,form);return true;}catch(BidException){return false;}}));
  Check(outcomes.Count(x=>x)==1&&d.State.Awards.Count==1&&d.State.Tender.Status==TenderStatus.Awarded,"concurrent award exactly once");Check(d.State.Bids.Single().Status==BidStatus.Compliant,"offer status unchanged");Check(d.Notifications.Any(n=>n.RecipientUserId=="other"&&n.Subject=="Tender award recorded"),"other invited supplier notified");
  var own=await s.OutcomeAsync(MemoryTopCommitteeData.User("supplier"),id);var other=await s.OutcomeAsync(MemoryTopCommitteeData.User("other"),id);Check(own.Result.Contains("was selected")&&other.Result.Contains("not selected")&&!JsonSerializer.Serialize(other).Contains("Fixture supplier"),"own outcome isolation");
  foreach(var decision in new[]{DecisionStatus.CommitteeRejected,DecisionStatus.ReturnedForReview}){
   d.Reset();await Prepare(d,s);foreach(var u in new[]{"top","top2","top3","top4"})await Vote(u,decision);
   Check(d.State.Tender.Status==TenderStatus.UnderEvaluation&&d.State.Reports.Count==2&&d.State.Reports[0].SubmittedAt!=null&&d.State.Reports[1].SubmittedAt==null,"return preserves original and creates draft");
   Check(ReportReviewRules.Current(d.State.Reports,d.State.Approvals,d.State.Approvals)==d.State.Reports[1],"returned approved report excluded from current");
   await Deny(()=>Page("top"),403);Check((await s.ListAsync(MemoryTopCommitteeData.User("top"))).Count==0,"rework hidden from Top Committee");
   d.State.Reports[1].SubmittedAt=DateTimeOffset.UtcNow;d.State.Approvals.Add(new(){Id=Guid.NewGuid(),TenderId=id,EvaluationReportId=d.State.Reports[1].Id,Stage=ApprovalStage.Manager,Decision=DecisionStatus.ManagerApproved,ActorUserId="manager",DecidedAt=DateTimeOffset.UtcNow});d.State.Tender.Status=TenderStatus.AwaitingCommitteeDecision;
   p=await Page("manager");Check(p.CanOpen,"new manager-approved report can have round");
  }
  d.Reset();await Prepare(d,s);await Vote("top",DecisionStatus.CommitteeApproved);await Vote("top2",DecisionStatus.CommitteeApproved);await Vote("top3",DecisionStatus.CommitteeRejected);await Vote("top4",DecisionStatus.CommitteeRejected);
  p=await Page("manager");Check(p.CanOpen&&!p.CanAward&&d.State.Approvals.Last().DecidedAt==null,"tie requires new reasoned round");await Deny(()=>s.OpenAsync(MemoryTopCommitteeData.User("manager"),id,new(){Version=p.Version,Confirm=true}),400);
  await s.OpenAsync(MemoryTopCommitteeData.User("manager"),id,new(){Version=p.Version,Confirm=true,Reason="Documented reconsideration"});Check(d.State.Approvals.Count==3&&d.State.Votes.Count==4,"new round preserves old votes");
  await Vote("top",DecisionStatus.ReturnedForReview);p=await Page("manager");await Deny(()=>s.AssignAsync(MemoryTopCommitteeData.User("manager"),id,new(){Version=p.Version,Users=["top5","top2","top3","top4"]}),400);
  await s.AssignAsync(MemoryTopCommitteeData.User("manager"),id,new(){Version=p.Version,Users=["top5","top2","top3","top4"],Reason="Documented replacement"});
  Check(d.State.Approvals.Count==4&&d.State.Votes.Count==5&&d.State.Members.Count==7,"replacement preserves history and opens fresh round");await Deny(()=>Page("top"),403);
  p=await Page("top5");Check(p.CanVote&&p.Rounds.Last().Votes.Count==0,"replacement has independent fresh vote");
  d.Accounts["top5"]=d.Accounts["top5"] with{Enabled=false};await Deny(()=>Page("top5"),403);Check(!(await Page("top2")).CanVote,"disabled round member pauses voting");
  d.Reset();await Prepare(d,s);p=await Page("top");
  var parallelVotes=await Task.WhenAll(new[]{"top","top2"}.Select(async user=>{try{await s.VoteAsync(MemoryTopCommitteeData.User(user),id,new(){Version=p.Version,RoundId=p.OpenRoundId,Choice=DecisionStatus.CommitteeApproved,Reason="Concurrent confirmed vote",Confirm=true});return true;}catch(BidException e)when(e.Status==409){return false;}}));
  Check(parallelVotes.Count(x=>x)==1&&d.State.Votes.Count==1,"parallel stale vote rejected without lost updates");
  var outstanding=new[]{"top","top2"}.Single(u=>!d.State.Members.Any(m=>m.UserId==u&&d.State.Votes.Any(v=>v.CommitteeMemberId==m.Id)));await Vote(outstanding,DecisionStatus.CommitteeApproved);Check(d.State.Votes.Count==2,"reload permits outstanding independent vote");
  Console.WriteLine($"PASS: {checks} production Top Committee service checks using serializable memory transactions; no SQL writes.");
 }
 internal static async Task Mvc(HttpClient client,IServiceProvider services){
  var n=0;var id=MemoryTopCommitteeData.Id;var d=services.GetRequiredService<MemoryTopCommitteeData>();var s=services.GetRequiredService<ITopCommitteeManagement>();await Prepare(d,s);
  async Task<HttpResponseMessage> Get(string actor,string path){client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");client.DefaultRequestHeaders.Add("X-Bid-Test-Actor",actor);return await client.GetAsync(path);}
  foreach(var actor in new[]{"manager","officer","top","admin","supplier","requester","evaluator","anonymous"}){
   var response=await Get(actor,"/TopCommittee/Details/"+id);var allowed=actor is "manager" or "officer" or "top";if(allowed?response.StatusCode!=HttpStatusCode.OK:response.StatusCode is not(HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))throw new Exception("Top MVC guard "+actor+" "+response.StatusCode);n++;
   if(allowed){var html=await response.Content.ReadAsStringAsync();if(!html.Contains("__RequestVerificationToken")&&actor!="officer")throw new Exception("CSRF field");if(!response.Headers.CacheControl!.NoStore)throw new Exception("No-store");n++;}
  }
  foreach(var pair in new[]{("manager","Assign"),("manager","Open"),("top","Vote"),("officer","Award")}){await Get(pair.Item1,"/TopCommittee/Details/"+id);var response=await client.PostAsync("/TopCommittee/"+pair.Item2+"/"+id,new FormUrlEncodedContent([]));if(response.StatusCode!=HttpStatusCode.BadRequest)throw new Exception("Missing antiforgery accepted");n++;}
  var pageResponse=await Get("top","/TopCommittee/Details/"+id);var page=await pageResponse.Content.ReadAsStringAsync();var token=System.Net.WebUtility.HtmlDecode(Regex.Match(page,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);var model=await s.PageAsync(MemoryTopCommitteeData.User("top"),id);
  var fields=new Dictionary<string,string>{{"__RequestVerificationToken",token},{"Version",model.Version},{"RoundId",model.OpenRoundId.ToString()!},{"Choice","CommitteeApproved"},{"Reason","<script>private-vote</script>"},{"Confirm","true"}};
  var result=await client.PostAsync("/TopCommittee/Vote/"+id,new FormUrlEncodedContent(fields));if(result.StatusCode!=HttpStatusCode.Redirect||d.State.Votes.Count!=1)throw new Exception("Vote binding");n++;
  page=await(await Get("top2","/TopCommittee/Details/"+id)).Content.ReadAsStringAsync();if(page.Contains("private-vote"))throw new Exception("Sealed vote leaked");n++;
  page=await(await Get("top","/TopCommittee/Details/"+id)).Content.ReadAsStringAsync();if(!page.Contains("&lt;script&gt;private-vote&lt;/script&gt;")||page.Contains("<script>private-vote</script>"))throw new Exception("Vote encoding");n++;
  result=await client.PostAsync("/TopCommittee/Vote/"+id,new FormUrlEncodedContent(fields));if(result.StatusCode!=HttpStatusCode.Conflict)throw new Exception("Duplicate post accepted");n++;
  foreach(var actor in new[]{"supplier","other","admin","requester"}){result=await Get(actor,"/TopCommittee/Outcome/"+id);if(result.StatusCode!=HttpStatusCode.OK||(await result.Content.ReadAsStringAsync()).Contains("Fixture supplier"))throw new Exception("Outcome metadata isolation");n++;}
  Console.WriteLine($"PASS: {n} compiled Top Committee MVC authorization, binding, antiforgery, secrecy and encoding checks; memory only.");
 }
}
