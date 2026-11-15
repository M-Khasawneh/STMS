using System.Security.Claims;
using SourcingTenderManagementSystem.Web.Models.Analysis;
namespace SourcingTenderManagementSystem.Web.Data;
public interface IAiBidManagement {
 Task<AiPage> PageAsync(ClaimsPrincipal user,Guid id,CancellationToken ct=default);
 Task RunAsync(ClaimsPrincipal user,Guid id,AiRunForm form,CancellationToken ct=default);
}
public sealed class AiBidManagement(IAiBidData data,IOllamaBidClient model,AiAnalysisGate gate,IAiBidDocuments documents):IAiBidManagement {
 public async Task<AiPage> PageAsync(ClaimsPrincipal user,Guid id,CancellationToken ct=default){
  var source=await data.SourceAsync(user,id,ct);var result=await data.FindAsync(source,ct);
  if(result is not null){await documents.ValidateAsync(user,source,result.Documents,ct);source=AiDocumentRules.Compose(source,result.Documents);}
  ComplianceRules.Version(source.Fingerprint,(await data.SourceAsync(user,id,ct)).Fingerprint);return new(source,result);
 }
 public async Task RunAsync(ClaimsPrincipal user,Guid id,AiRunForm form,CancellationToken ct=default){
  var source=await data.SourceAsync(user,id,ct);ComplianceRules.Version(form.Version,source.Fingerprint);BidRules.Require(form.Confirm,400,"Confirm running local advisory analysis.");
  if(await data.FindAsync(source,ct)is {} existing){await documents.ValidateAsync(user,source,existing.Documents,ct);return;}
  BidRules.Require(await gate.Semaphore.WaitAsync(0,ct),409,"Local analysis is busy. Try again after the current request completes.");
  try{source=await data.SourceAsync(user,id,ct);ComplianceRules.Version(form.Version,source.Fingerprint);source=await documents.EnrichAsync(user,source,ct);var advice=await model.AnalyzeAsync(source,ct);_=AiBidRules.Parse(System.Text.Json.JsonSerializer.Serialize(advice,AiBidRules.Json),source);await data.SaveAsync(user,source,model.Model,advice,ct);}finally{gate.Semaphore.Release();}
 }
}
