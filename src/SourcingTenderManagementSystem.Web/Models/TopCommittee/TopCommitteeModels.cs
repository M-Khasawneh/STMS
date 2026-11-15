using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.Web.Models.TopCommittee;
public sealed class TopActionForm {
 [Required] public string Version {get;set;}="";
 public Guid? RoundId {get;set;}
 [StringLength(8000)] public string? Reason {get;set;}
 public bool Confirm {get;set;}
 public DecisionStatus Choice {get;set;}
}
public sealed class TopMembershipForm {
 [Required] public string Version {get;set;}="";
 public string[] Users {get;set;}=["","","",""];
 [StringLength(8000)] public string? Reason {get;set;}
}
public sealed record TopCandidate(BidAccount Account,string Name);
public sealed record TopMemberRow(CommitteeMember Member,string Name,bool Eligible);
public sealed record TopVoteRow(string Member, string Choice,string Reason,DateTimeOffset At);
public sealed record TopRoundRow(TenderApproval Round,int VoteCount,bool Complete,IReadOnlyList<TopVoteRow> Votes);
public sealed record TopTenderRow(Guid Id,string Title,TenderStatus Status);
public sealed record TopPage(Tender Tender,string Version,bool CanManage,bool CanOpen,bool CanVote,bool CanAward,Guid? OpenRoundId,
 string? RecommendedSupplier,Guid? ReportId,IReadOnlyList<TopCandidate> Candidates,IReadOnlyList<TopMemberRow> Members,
 IReadOnlyList<TopRoundRow> Rounds,TenderAward? Award,IReadOnlyList<AuditLog> Activity);
public sealed record TenderOutcome(Guid TenderId,string Title,TenderStatus Status,string Result,DateTimeOffset? AwardedAt);
