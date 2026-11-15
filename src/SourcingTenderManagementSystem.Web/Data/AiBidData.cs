using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Analysis;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.Web.Data;
public interface IAiBidData {
 Task<AiEvidence> SourceAsync(ClaimsPrincipal user,Guid tenderId,CancellationToken ct);
 Task<AiResult?> FindAsync(AiEvidence source,CancellationToken ct);
 Task<AiResult> SaveAsync(ClaimsPrincipal user,AiEvidence source,string model,AiAdvice advice,CancellationToken ct);
}
public sealed class AiBidData(ApplicationDbContext db,BidReviewPages pages,IBidData bids,TimeProvider clock):IAiBidData {
 public async Task<AiEvidence> SourceAsync(ClaimsPrincipal user,Guid id,CancellationToken ct)=>AiBidRules.Evidence(await pages.CompareAsync(user,id,ct));
 public async Task<AiResult?> FindAsync(AiEvidence source,CancellationToken ct){
  var row=await db.AiBidAnalyses.AsNoTracking().Where(a=>a.TenderId==source.TenderId&&a.InputReference==source.Fingerprint).OrderByDescending(a=>a.CreatedAt).ThenBy(a=>a.Id).FirstOrDefaultAsync(ct);
  return row is null?null:new(row.Id,row.ModelName,row.CreatedAt,AiBidRules.Parse(row.AnalysisJson,source));
 }
 public Task<AiResult> SaveAsync(ClaimsPrincipal user,AiEvidence source,string model,AiAdvice advice,CancellationToken ct)=>BidManagement.Mutate(async()=>{
  await using var tx=await bids.BeginAsync(source.TenderId,ct);
  var fresh=await SourceAsync(user,source.TenderId,ct);ComplianceRules.Version(source.Fingerprint,fresh.Fingerprint);
  var existing=await FindAsync(fresh,ct);if(existing is not null){await tx.CommitAsync(ct);return existing;}
  var actor=await bids.AccountAsync(user,clock.GetUtcNow(),ct);BidRules.Require(actor is {Internal:true},403,"Authorized internal analysis access is required.");
  var json=JsonSerializer.Serialize(advice,AiBidRules.Json);_=AiBidRules.Parse(json,fresh);
  var row=new AiBidAnalysis{Id=Guid.NewGuid(),TenderId=source.TenderId,RequestedByUserId=actor.UserId,ModelName=model,InputReference=source.Fingerprint,AnalysisJson=json,CreatedAt=clock.GetUtcNow()};
  db.AiBidAnalyses.Add(row);db.AuditLogs.Add(new(){Id=Guid.NewGuid(),ActorUserId=actor.UserId,RecordType="AiBidAnalysis",RecordId=row.Id.ToString(),Action="AdvisoryAnalysisCreated",Reason="Tender "+source.TenderId+"; source "+source.Fingerprint+"; model "+model,OccurredAt=row.CreatedAt});
  await db.SaveChangesAsync(ct);ComplianceRules.Version(source.Fingerprint,(await SourceAsync(user,source.TenderId,ct)).Fingerprint);await tx.CommitAsync(ct);
  return new AiResult(row.Id,model,row.CreatedAt,advice);
 });
}
