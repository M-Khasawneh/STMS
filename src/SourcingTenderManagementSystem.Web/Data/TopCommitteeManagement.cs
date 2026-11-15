using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.TopCommittee;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Data;
public interface ITopCommitteeManagement {
 Task<IReadOnlyList<TopTenderRow>> ListAsync(ClaimsPrincipal user,CancellationToken ct=default);
 Task<TopPage> PageAsync(ClaimsPrincipal user,Guid id,CancellationToken ct=default);
 Task AssignAsync(ClaimsPrincipal user,Guid id,TopMembershipForm form,CancellationToken ct=default);
 Task OpenAsync(ClaimsPrincipal user,Guid id,TopActionForm form,CancellationToken ct=default);
 Task VoteAsync(ClaimsPrincipal user,Guid id,TopActionForm form,CancellationToken ct=default);
 Task AwardAsync(ClaimsPrincipal user,Guid id,TopActionForm form,CancellationToken ct=default);
 Task<TenderOutcome> OutcomeAsync(ClaimsPrincipal user,Guid id,CancellationToken ct=default);
}
public sealed class TopCommitteeManagement(ITopCommitteeData data,TimeProvider clock):ITopCommitteeManagement {
 private DateTimeOffset Now=>clock.GetUtcNow();
 private async Task<BidAccount> Actor(ClaimsPrincipal user,CancellationToken ct){var a=await data.AccountAsync(user,ct);BidRules.Require(a is not null&&(a.Internal||a.Supplier||ReportRules.Admin(a)),403,"An eligible account is required.");return a;}
 private static IEnumerable<CommitteeMember> Kind(TopUnit s,CommitteeKind kind)=>s.Members.Where(m=>s.Assignments.Any(a=>a.Id==m.CommitteeAssignmentId&&a.Kind==kind));
 private CommitteeMember[] Active(TopUnit s)=>Kind(s,CommitteeKind.TopCommittee).Where(m=>CommitteeRules.Effective(m,Now)).OrderBy(m=>m.Position).ThenBy(m=>m.Id).ToArray();
 private bool Manager(TopUnit s,BidAccount a)=>a.Internal&&a.Roles.Contains(ApplicationRoles.SourcingManager)&&Kind(s,CommitteeKind.Evaluation).Any(m=>m.Position==CommitteeRules.Positions[0]&&m.UserId==a.UserId&&CommitteeRules.Effective(m,Now));
 private static bool Officer(TopUnit s,BidAccount a)=>a.Internal&&a.Roles.Contains(ApplicationRoles.SourcingOfficer)&&a.UserId==s.Tender.CreatedByUserId;
 private bool Eligible(TopUnit s,CommitteeMember m)=>CommitteeRules.Effective(m,Now)&&s.People.Any(p=>p.Account.UserId==m.UserId&&TopCommitteeVotingRules.Eligible(p.Account,s.Excluded));
 private bool Member(TopUnit s,BidAccount a)=>TopCommitteeVotingRules.Eligible(a,s.Excluded)&&Active(s).Any(m=>m.UserId==a.UserId);
 private bool Access(TopUnit s,BidAccount a)=>a.Internal&&BidRules.ClosedStages.Contains(s.Tender.Status)&&(Manager(s,a)||Officer(s,a)||s.Tender.Status is TenderStatus.AwaitingCommitteeDecision or TenderStatus.Awarded&&Member(s,a));
 private static EvaluationReport? Current(TopUnit s)=>ReportReviewRules.Current(s.Reports,s.Approvals,s.Approvals.Where(a=>a.Stage==ApprovalStage.TopCommittee).ToArray());
 private static TenderApproval[] Rounds(TopUnit s)=>s.Approvals.Where(a=>a.Stage==ApprovalStage.TopCommittee&&a.RoundOpenedAt.HasValue).OrderBy(a=>a.RoundOpenedAt).ThenBy(a=>a.Id).ToArray();
 private static TenderApproval? OpenRound(TopUnit s)=>Rounds(s).SingleOrDefault(r=>r.RoundClosedAt is null);
 private static TenderApproval? Approved(TopUnit s,EvaluationReport? report)=>Rounds(s).SingleOrDefault(r=>r.EvaluationReportId==report?.Id&&r.Decision==DecisionStatus.CommitteeApproved);
 private void Validate(TopUnit s){
  BidRules.Require(s.Assignments.GroupBy(a=>a.Kind).All(g=>g.Count()==1)&&s.Members.All(m=>m.TenderId==s.Tender.Id&&s.Assignments.Any(a=>a.Id==m.CommitteeAssignmentId&&a.TenderId==m.TenderId)),409,"Committee assignment records conflict.");
  var members=Active(s);BidRules.Require(members.Length is 0 or 4&&members.Select(m=>m.UserId).Distinct().Count()==members.Length&&members.Select(m=>m.Position).Distinct().Count()==members.Length&&members.All(m=>Enumerable.Range(1,4).Select(i=>"Member "+i).Contains(m.Position)),409,"Top Committee requires four consistent current memberships.");
  BidRules.Require(!Kind(s,CommitteeKind.TopCommittee).Any(m=>m.EffectiveFrom>Now)&&s.Awards.Count<=1&&Rounds(s).Count(r=>r.RoundClosedAt is null)<=1,409,"Scheduled memberships, duplicate awards or open rounds require review.");
  foreach(var r in Rounds(s)){
   var ids=TopCommitteeVotingRules.Members(r);BidRules.Require(ids.All(id=>s.Members.Any(m=>m.Id==id&&m.CommitteeAssignmentId==r.CommitteeAssignmentId&&m.TenderId==s.Tender.Id)),409,"A frozen round membership is missing.");
   var tally=TopCommitteeVotingRules.Tally(r,s.Tender.Id,ids,s.Votes.Where(v=>v.TenderApprovalId==r.Id).ToArray());
   BidRules.Require(r.RoundClosedAt is null?r.Decision==DecisionStatus.RecommendationPending&&r.DecidedAt is null&&!tally.Complete:
    r.Decision==DecisionStatus.RecommendationPending?r.DecidedAt is null&&(!tally.Complete||tally.Outcome is null):r.DecidedAt.HasValue&&r.DecidedAt>=r.RoundOpenedAt&&r.DecidedAt<=r.RoundClosedAt&&tally.Complete&&tally.Outcome==r.Decision,409,"Round decision and vote records conflict.");
  }
  BidRules.Require(s.Votes.All(v=>Rounds(s).Any(r=>r.Id==v.TenderApprovalId)),409,"Unscoped legacy vote records require review.");_ = Current(s);
 }
 private static string Version(TopUnit s)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{
  s.Tender.Status,Assignments=s.Assignments.OrderBy(a=>a.Id),Members=s.Members.OrderBy(m=>m.Id),Reports=s.Reports.OrderBy(r=>r.Id),Approvals=s.Approvals.OrderBy(a=>a.Id),
  Votes=s.Votes.OrderBy(v=>v.Id).Select(v=>new{v.Id,v.CommitteeMemberId,v.VotedAt}),Awards=s.Awards.OrderBy(w=>w.Id),People=s.People.OrderBy(p=>p.Account.UserId),Excluded=s.Excluded.Order()
 })));
 public async Task<IReadOnlyList<TopTenderRow>> ListAsync(ClaimsPrincipal user,CancellationToken ct=default){var a=await Actor(user,ct);BidRules.Require(a.Internal&&a.Roles.Any(r=>r is ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or ApplicationRoles.TopCommittee),403,"Authorized final-stage access is required.");var result=new List<TopTenderRow>();foreach(var row in await data.ListAsync(a,ct)){var s=await data.LoadAsync(row.Id,ct);Validate(s);if(Access(s,a))result.Add(row);}var fresh=await Actor(user,ct);BidRules.Require(fresh.Internal&&fresh.Roles.Order().SequenceEqual(a.Roles.Order()),403,"Account eligibility changed.");return result;}
 public async Task<TopPage> PageAsync(ClaimsPrincipal user,Guid id,CancellationToken ct=default){var a=await Actor(user,ct);BidRules.Require(a.Internal,403,"Internal final-decision access is required.");var s=await data.LoadAsync(id,ct);Validate(s);BidRules.Require(Access(s,a),403,"You are not currently involved in this tender's final decision.");
  var report=Current(s);var open=OpenRound(s);var active=Active(s);var isManager=Manager(s,a);var approved=Approved(s,report);var canManage=isManager&&s.Tender.Status!=TenderStatus.Awarded&&approved is null;
  var canOpen=canManage&&s.Tender.Status==TenderStatus.AwaitingCommitteeDecision&&open is null&&report?.SubmittedAt!=null&&s.Approvals.Any(d=>d.Stage==ApprovalStage.Manager&&d.Decision==DecisionStatus.ManagerApproved&&d.EvaluationReportId==report.Id)&&active.Length==4&&active.All(m=>Eligible(s,m));
  var member=active.SingleOrDefault(m=>m.UserId==a.UserId);var rounds=new List<TopRoundRow>();
  foreach(var r in Rounds(s)){
   var votes=s.Votes.Where(v=>v.TenderApprovalId==r.Id).OrderBy(v=>v.VotedAt).ThenBy(v=>v.Id).ToArray();var tally=TopCommitteeVotingRules.Tally(r,id,TopCommitteeVotingRules.Members(r),votes);
   var own=Kind(s,CommitteeKind.TopCommittee).Where(m=>m.UserId==a.UserId&&TopCommitteeVotingRules.Members(r).Contains(m.Id)).Select(m=>(Guid?)m.Id).SingleOrDefault();
   rounds.Add(new(r,votes.Length,tally.Complete,TopCommitteeVotingRules.VisibleVotes(r,id,own,votes).Select(v=>new TopVoteRow(s.MemberNames.GetValueOrDefault(s.Members.Single(m=>m.Id==v.CommitteeMemberId).UserId)??"Former employee",v.Decision,v.Justification,v.VotedAt)).ToArray()));
  }
  var fresh=await Actor(user,ct);var final=await data.LoadAsync(id,ct);Validate(final);BidRules.Require(Access(final,fresh)&&Version(final)==Version(s),409,"This tender changed. Reload the page.");
  return new(s.Tender,Version(s),canManage,canOpen,open!=null&&member!=null&&Member(s,a)&&active.All(m=>Eligible(s,m))&&TopCommitteeVotingRules.Members(open).Order().SequenceEqual(active.Select(m=>m.Id).Order())&&!s.Votes.Any(v=>v.TenderApprovalId==open.Id&&v.CommitteeMemberId==member.Id),
   Officer(s,a)&&s.Tender.Status==TenderStatus.AwaitingCommitteeDecision&&approved!=null&&s.Awards.Count==0,open?.Id,report?.RecommendedBidId is Guid bid?s.Companies.GetValueOrDefault(bid):null,report?.Id,
   canManage?s.People.Where(p=>TopCommitteeVotingRules.Eligible(p.Account,s.Excluded)).OrderBy(p=>p.Name).ToArray():[],Kind(s,CommitteeKind.TopCommittee).OrderBy(m=>m.EffectiveFrom).Select(m=>new TopMemberRow(m,s.MemberNames.GetValueOrDefault(m.UserId)??s.People.FirstOrDefault(p=>p.Account.UserId==m.UserId)?.Name??"Former employee",Eligible(s,m))).ToArray(),rounds,s.Awards.SingleOrDefault(),s.History);
 }
 private EvaluationReport Ready(TopUnit s){var r=Current(s);BidRules.Require(s.Tender.Status==TenderStatus.AwaitingCommitteeDecision&&r?.SubmittedAt!=null&&r.RecommendedBidId.HasValue&&s.Approvals.Any(a=>a.Stage==ApprovalStage.Manager&&a.EvaluationReportId==r.Id&&a.Decision==DecisionStatus.ManagerApproved),409,"A current submitted manager-approved recommendation is required.");var b=s.Bids.SingleOrDefault(b=>b.Id==r.RecommendedBidId);BidRules.Require(b?.SubmittedAt!=null&&b.Status==BidStatus.Compliant,409,"The recommended submitted offer must remain compliant.");return r;}
 private void Notify(TopUnit s,IEnumerable<string> users,string subject,string message){foreach(var user in users.Distinct())s.NewNotifications.Add(new(){Id=Guid.NewGuid(),RecipientUserId=user,TenderId=s.Tender.Id,Subject=subject,Message=message,CreatedAt=Now});}
 private void Audit(TopUnit s,string actor,string action,string reason,string type="TopCommittee",string? before=null,string? after=null)=>s.NewAudits.Add(new(){Id=Guid.NewGuid(),ActorUserId=actor,RecordType=type,RecordId=s.Tender.Id.ToString(),Action=action,Reason=reason,PreviousStatus=before,NewStatus=after,OccurredAt=Now});
 private IEnumerable<string> Managers(TopUnit s)=>Kind(s,CommitteeKind.Evaluation).Where(m=>m.Position==CommitteeRules.Positions[0]&&CommitteeRules.Effective(m,Now)).Select(m=>m.UserId);
 private TenderApproval NewRound(TopUnit s,BidAccount a,string? reason){var report=Ready(s);var active=Active(s);BidRules.Require(active.Length==4&&active.All(m=>Eligible(s,m))&&OpenRound(s) is null&&Approved(s,report) is null,409,"Four eligible current members and no open/final approved round are required.");var prior=Rounds(s).Where(r=>r.EvaluationReportId==report.Id).ToArray();if(prior.Length>0||!string.IsNullOrEmpty(reason))TopCommitteeVotingRules.Reason(reason);
  var round=new TenderApproval{Id=Guid.NewGuid(),TenderId=s.Tender.Id,EvaluationReportId=report.Id,Stage=ApprovalStage.TopCommittee,Decision=DecisionStatus.RecommendationPending,ActorUserId=a.UserId,CommitteeAssignmentId=active[0].CommitteeAssignmentId,RoundOpenedAt=Now,RoundMemberSnapshot=JsonSerializer.Serialize(active.Select(m=>m.Id).ToArray()),Justification=reason?.Trim()??""};s.Approvals.Add(round);Audit(s,a.UserId,"RoundOpened","Round "+round.Id);Notify(s,active.Select(m=>m.UserId),"Top committee round","A voting round is available for a tender assigned to you.");return round;
 }
 private async Task Change(ClaimsPrincipal user,Guid id,string version,string authority,Func<TopUnit,BidAccount,Task> action,CancellationToken ct){await BidManagement.Mutate(async()=>{
  await using var tx=await data.BeginAsync(id,ct);var a=await Actor(user,ct);var s=await data.LoadAsync(id,ct);Validate(s);BidRules.Require(Access(s,a),403,"No current final-decision access.");ComplianceRules.Version(version,Version(s));
  BidRules.Require(authority=="manager"?Manager(s,a):authority=="officer"?Officer(s,a):Member(s,a),403,"This action is not authorized for your appointment.");
  await action(s,a);Validate(s);await data.SaveAsync(s,ct);
  var fresh=await Actor(user,ct);var final=await data.LoadAsync(id,ct);Validate(final);
  BidRules.Require(fresh.UserId==a.UserId&&(authority=="manager"?Manager(final,fresh):authority=="officer"?Officer(final,fresh):Member(final,fresh)),403,"Actor eligibility changed before commit.");if(authority=="officer"&&final.Awards.SingleOrDefault() is {} award)BidRules.Require(await data.SupplierEligibleAsync(award.BidId,ct),409,"Supplier eligibility changed before award commit.");await tx.CommitAsync(ct);return true;
 });}
 public Task AssignAsync(ClaimsPrincipal user,Guid id,TopMembershipForm form,CancellationToken ct=default)=>Change(user,id,form.Version,"manager",(s,a)=>{
  BidRules.Require(s.Tender.Status!=TenderStatus.Awarded&&Approved(s,Current(s)) is null,409,"An awarded tender or approved final decision is immutable.");
  TopCommitteeVotingRules.Selection(form.Users,s.People.Where(p=>TopCommitteeVotingRules.Eligible(p.Account,s.Excluded)).Select(p=>p.Account.UserId).ToHashSet());
  var active=Active(s);if(active.Length==4&&Enumerable.Range(0,4).All(i=>active.Single(m=>m.Position=="Member "+(i+1)).UserId==form.Users[i]))return Task.CompletedTask;
  if(Kind(s,CommitteeKind.TopCommittee).Any())TopCommitteeVotingRules.Reason(form.Reason);
  var open=OpenRound(s);if(open!=null){open.RoundClosedAt=Now;open.RoundCloseReason="Membership replaced; justification preserved in the appointment audit and new round.";Audit(s,a.UserId,"RoundAbandoned","Round "+open.Id+" closed for membership replacement.");}
  var assignment=s.Assignments.SingleOrDefault(c=>c.Kind==CommitteeKind.TopCommittee);if(assignment==null){assignment=new(){Id=Guid.NewGuid(),TenderId=id,Kind=CommitteeKind.TopCommittee};s.Assignments.Add(assignment);}
  for(int i=0;i<4;i++){var position="Member "+(i+1);var old=active.SingleOrDefault(m=>m.Position==position);if(old?.UserId==form.Users[i])continue;if(old!=null)old.EffectiveTo=Now;s.Members.Add(new(){Id=Guid.NewGuid(),TenderId=id,CommitteeAssignmentId=assignment.Id,UserId=form.Users[i],Position=position,EffectiveFrom=Now});}
  Audit(s,a.UserId,"MembersAssigned",form.Reason?.Trim()??"Initial four-member appointment.");Notify(s,form.Users,"Top committee update","Your committee appointment was updated. Tender review is available at the final-decision stage.");if(open!=null)NewRound(s,a,form.Reason);return Task.CompletedTask;
 },ct);
 public Task OpenAsync(ClaimsPrincipal user,Guid id,TopActionForm form,CancellationToken ct=default)=>Change(user,id,form.Version,"manager",(s,a)=>{BidRules.Require(form.Confirm,400,"Confirm opening the voting round.");NewRound(s,a,form.Reason);return Task.CompletedTask;},ct);
 public Task VoteAsync(ClaimsPrincipal user,Guid id,TopActionForm form,CancellationToken ct=default)=>Change(user,id,form.Version,"member",(s,a)=>{
  BidRules.Require(form.Confirm,400,"Explicitly confirm your final vote.");var r=OpenRound(s)??throw new BidException(409,"No open round.");BidRules.Require(r.Id==form.RoundId,409,"This round changed.");var decidedReport=Ready(s);var active=Active(s);BidRules.Require(active.All(m=>Eligible(s,m))&&active.Select(m=>m.Id).Order().SequenceEqual(TopCommitteeVotingRules.Members(r).Order()),409,"Current round membership is no longer eligible.");
  var member=active.Single(m=>m.UserId==a.UserId);var votes=s.Votes.Where(v=>v.TenderApprovalId==r.Id).ToArray();TopCommitteeVotingRules.Vote(a,s.Assignments.Single(c=>c.Id==member.CommitteeAssignmentId),member,s.Tender,r,TopCommitteeVotingRules.Members(r),votes,s.Excluded,form.Choice,form.Reason,Now);
  s.Votes.Add(new(){Id=Guid.NewGuid(),TenderId=id,TenderApprovalId=r.Id,CommitteeMemberId=member.Id,Decision=form.Choice.ToString(),Justification=form.Reason!.Trim(),VotedAt=Now});Audit(s,a.UserId,"VoteRecorded","A confirmed vote was recorded for round "+r.Id+".");
  var tally=TopCommitteeVotingRules.Tally(r,id,TopCommitteeVotingRules.Members(r),s.Votes.Where(v=>v.TenderApprovalId==r.Id).ToArray());
  if(tally.Complete){var completedAt=Now;r.RoundClosedAt=completedAt;r.RoundCloseReason=tally.Outcome.HasValue?"All four votes received; collective outcome recorded.":"All four votes received without a three-vote majority.";
   if(tally.Outcome.HasValue){r.Decision=tally.Outcome.Value;r.DecidedAt=completedAt;r.ActorUserId=null;r.Justification=string.Join(Environment.NewLine,s.Votes.Where(v=>v.TenderApprovalId==r.Id).OrderBy(v=>v.CommitteeMemberId).Select(v=>v.Decision+": "+v.Justification));}
   Audit(s,a.UserId,"RoundCompleted","Round "+r.Id+" completed.");
   if(tally.Outcome is DecisionStatus.CommitteeRejected or DecisionStatus.ReturnedForReview){var original=decidedReport;s.Tender.Status=TenderStatus.UnderEvaluation;s.Reports.Add(ReportReviewRules.Revision(original));Audit(s,a.UserId,"TopCommitteeReportReturned","Report "+original.Id+" returned after a collective decision.","EvaluationReport","AwaitingCommitteeDecision","UnderEvaluation");Audit(s,a.UserId,"Tender.TopCommitteeReturn","Round "+r.Id,"Tender","AwaitingCommitteeDecision","UnderEvaluation");Notify(s,Kind(s,CommitteeKind.Evaluation).Where(m=>CommitteeRules.Effective(m,Now)).Select(m=>m.UserId),"Top report returned","The collective decision requires a revised report and another manager review.");Notify(s,active.Select(m=>m.UserId),"Top committee update","The round completed and the tender returned for report revision.");}
   else Notify(s,active.Select(m=>m.UserId).Concat(Managers(s)).Append(s.Tender.CreatedByUserId),"Top committee outcome",tally.Outcome.HasValue?"The collective recommendation was approved. Award confirmation is a separate action.":"The round completed without a majority. A documented new round is required.");
  }else Notify(s,Managers(s),"Top committee update","A vote was recorded. Individual choices remain confidential until all four votes are received.");return Task.CompletedTask;
 },ct);
 public Task AwardAsync(ClaimsPrincipal user,Guid id,TopActionForm form,CancellationToken ct=default)=>Change(user,id,form.Version,"officer",async(s,a)=>{
  BidRules.Require(form.Confirm,400,"Explicitly confirm recording the final award.");var report=Ready(s);var decision=Approved(s,report)??throw new BidException(409,"No approved collective recommendation.");BidRules.Require(form.RoundId==decision.Id,409,"The approved decision changed.");var bid=s.Bids.Single(b=>b.Id==report.RecommendedBidId);BidRules.Require(await data.SupplierEligibleAsync(bid.Id,ct),409,"The recommended supplier is no longer eligible for participation.");TopCommitteeVotingRules.Award(a,s.Tender,decision,report,bid,s.Awards.Count>0,s.Votes.Where(v=>v.TenderApprovalId==decision.Id).ToArray());
  s.Awards.Add(new(){Id=Guid.NewGuid(),TenderId=id,BidId=bid.Id,TenderApprovalId=decision.Id,RecordedByUserId=a.UserId,AwardedAt=Now});s.Tender.Status=TenderStatus.Awarded;Audit(s,a.UserId,"AwardRecorded","Approved round "+decision.Id);Audit(s,a.UserId,"Tender.Awarded","Final award recorded.","Tender","AwaitingCommitteeDecision","Awarded");
  var invited=await data.InvitedUsersAsync(id,ct);var internalUsers=Kind(s,CommitteeKind.Evaluation).Concat(Kind(s,CommitteeKind.TopCommittee)).Where(m=>CommitteeRules.Effective(m,Now)).Select(m=>m.UserId).Append(s.Source.CreatedByUserId).Append(s.Tender.CreatedByUserId).Where(u=>s.People.Any(p=>p.Account.UserId==u));Notify(s,internalUsers.Concat(invited),"Tender award recorded","The tender's final outcome has been recorded. Open your authorized outcome page.");
 },ct);
 public async Task<TenderOutcome> OutcomeAsync(ClaimsPrincipal user,Guid id,CancellationToken ct=default){var a=await Actor(user,ct);var outcome=await data.OutcomeAsync(a,id,ct);var fresh=await Actor(user,ct);BidRules.Require(fresh.UserId==a.UserId,403,"Account access changed.");return await data.OutcomeAsync(fresh,id,ct);}
}
