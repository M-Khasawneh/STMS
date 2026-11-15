using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Analysis;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal sealed class MemoryAiData(BidReviewPages pages,IBidData bids):IAiBidData {
 internal List<(string Fingerprint,AiResult Result)> Rows=[];internal bool FailSave;
 public async Task<AiEvidence> SourceAsync(ClaimsPrincipal user,Guid id,CancellationToken ct)=>AiBidRules.Evidence(await pages.CompareAsync(user,id,ct));
 public Task<AiResult?> FindAsync(AiEvidence source,CancellationToken ct)=>Task.FromResult<AiResult?>(Rows.FirstOrDefault(r=>r.Fingerprint==source.Fingerprint).Result);
 public async Task<AiResult> SaveAsync(ClaimsPrincipal user,AiEvidence source,string model,AiAdvice advice,CancellationToken ct){
  await using var tx=await bids.BeginAsync(source.TenderId,ct);var fresh=await SourceAsync(user,source.TenderId,ct);ComplianceRules.Version(source.Fingerprint,fresh.Fingerprint);var existing=await FindAsync(fresh,ct);if(existing!=null)return existing;
  if(FailSave)throw new IOException("Injected analysis persistence failure");var result=new AiResult(Guid.NewGuid(),model,DateTimeOffset.UtcNow,advice);await tx.CommitAsync(ct);Rows.Add((source.Fingerprint,result));return result;
 }
}
internal sealed class TestOllama:IOllamaBidClient {
 public string Model=>"qwen3:4b";internal int Calls;internal bool Fail;internal Func<Task>? During;
 public async Task<AiAdvice> AnalyzeAsync(AiEvidence source,CancellationToken ct){Calls++;if(During!=null)await During();if(Fail)throw new BidException(502,"Fixture invalid model response");return AiAnalysisChecks.Advice(source);}
}
internal static class AiAnalysisChecks {
 internal static AiEvidence Synthetic(){var id=Guid.Parse("10000000-0000-0000-0000-000000000001");return new(Guid.Parse("20000000-0000-0000-0000-000000000001"),"Synthetic equipment procurement",TenderStatus.Closed,[new(id,"Synthetic supplier",BidStatus.Compliant,50m,"14 days","Requested specifications met",[new(id+":total","50 JOD"),new(id+":delivery","14 days"),new(id+":files","Technical Proposal present; document content not supplied")],[])],"synthetic-only");}
 internal static AiAdvice Advice(AiEvidence source)=>new(){Offers=source.Offers.Select(o=>new AiOfferAdvice{BidId=o.BidId,Strengths=[new(){Text="<script>advisory-test</script>",Evidence=[o.Facts[0].Key]}],Weaknesses=[]}).ToArray(),Comparison="Compare the saved commercial terms.",Recommendation="Human review of original documents is required.",RecommendedBidId=null};
 internal static async Task Rules(){
  int n=0;void Check(bool x){if(!x)throw new Exception("AI rule check "+n);n++;}void Deny(string json,AiEvidence s){try{AiBidRules.Parse(json,s);throw new Exception("Expected model rejection");}catch(BidException e){Check(e.Status==502);}}
  var source=Synthetic();var advice=Advice(source);string Encode()=>JsonSerializer.Serialize(advice,AiBidRules.Json);
  Check(AiBidRules.Parse(Encode(),source).Offers.Length==1);
  foreach(var bad in new[]{"not json","null","[]","{}",new string('x',64001),Encode().Replace("\"offers\":[", "\"offers\":[null,")})Deny(bad,source);
  advice.Offers[0].BidId=Guid.NewGuid();Deny(Encode(),source);advice=Advice(source);advice.Offers[0].Strengths[0].Evidence=["foreign:secret"];Deny(Encode(),source);
  advice=Advice(source);advice.Offers[0].Strengths[0].Evidence=[];Deny(Encode(),source);advice=Advice(source);advice.Offers=[advice.Offers[0],advice.Offers[0]];Deny(Encode(),source);
  advice=Advice(source);advice.RecommendedBidId=Guid.NewGuid();Deny(Encode(),source);advice.RecommendedBidId=source.Offers[0].BidId;Check(AiBidRules.Parse(Encode(),source).RecommendedBidId.HasValue);
  Deny(Encode(),source with{Offers=[source.Offers[0] with{Status=BidStatus.NonCompliant}]});advice=Advice(source);advice.Comparison=" ";Deny(Encode(),source);
  advice=Advice(source);Deny(Encode().Replace("{\"offers\"","{\"unexpected\":true,\"offers\""),source);
  var handler=new ReplyHandler(source);var model=new OllamaBidClient(new HttpClient(handler),Options.Create(new OllamaOptions()));
  _=await model.AnalyzeAsync(source,default);Check(handler.ValidRequest);
  foreach(var response in new[]{"[]","{}","not-json","{\"model\":\"foreign\",\"done\":true,\"message\":{\"content\":\"{}\"}}"}){handler.Response=response;try{await model.AnalyzeAsync(source,default);throw new Exception("Expected bad HTTP model output rejection");}catch(BidException e){Check(e.Status==502);}}
  handler.Status=HttpStatusCode.ServiceUnavailable;try{await model.AnalyzeAsync(source,default);throw new Exception("Expected unavailable model");}catch(BidException e){Check(e.Status==503);}
  Console.WriteLine($"PASS: {n} database-free advisory schema, evidence, foreign-offer, eligibility, bounded-response and Ollama request checks.");
 }
 private sealed class ReplyHandler(AiEvidence source):HttpMessageHandler{
  internal bool ValidRequest;internal string? Response;internal HttpStatusCode Status=HttpStatusCode.OK;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){var body=await request.Content!.ReadAsStringAsync(ct);using var json=JsonDocument.Parse(body);var r=json.RootElement;ValidRequest=request.RequestUri!.ToString()=="http://127.0.0.1:11434/api/chat"&&!r.GetProperty("stream").GetBoolean()&&!r.GetProperty("think").GetBoolean()&&r.GetProperty("format").ValueKind==JsonValueKind.Object&&body.Contains("UNTRUSTED DATA");return new(Status){Content=new StringContent(Response??JsonSerializer.Serialize(new{model="qwen3:4b",done=true,message=new{content=JsonSerializer.Serialize(Advice(source),AiBidRules.Json)}}))};}
 }
 internal static async Task LocalModel(){
  using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false}){Timeout=TimeSpan.FromMinutes(3)};
  var model=new OllamaBidClient(http,Options.Create(new OllamaOptions()));var first=Synthetic();var secondId=Guid.Parse("10000000-0000-0000-0000-000000000002");
  var second=new AiOffer(secondId,"Second synthetic supplier",BidStatus.Compliant,65m,"7 days","Requested specifications met",[new(secondId+":total","65 JOD"),new(secondId+":delivery","7 days"),new(secondId+":files","Technical Proposal present; document content not supplied")],[]);
  var result=await model.AnalyzeAsync(first with{Offers=[first.Offers[0],second]},default);Console.WriteLine($"PASS: actual local {model.Model} generated a validated structured advisory result for {result.Offers.Length} synthetic offer. No SQL, supplier records or private files used.");
 }
 internal static async Task Prepare(BidPhase1Checks.Fixture f){
  foreach(var pair in new[]{("officer",ApplicationRoles.SourcingOfficer),("manager",ApplicationRoles.SourcingManager),("evaluator",ApplicationRoles.EvaluationCommittee),("top",ApplicationRoles.TopCommittee),("admin",ApplicationRoles.Admin),("requester",ApplicationRoles.RequestingDepartment)})f.Store.Accounts[pair.Item1]=new(pair.Item1,[pair.Item2],null,false,true,true,[],[]);
  var id=await f.Service.CreateDraftAsync(f.User,f.Context.Tender.Id,f.Form());f.AddDocument(id);await f.Service.SubmitAsync(f.User,id,(await f.Service.DetailsAsync(f.User,id)).Version);f.Context.Tender.Status=TenderStatus.Closed;
 }
 internal static async Task Mvc(HttpClient client,IServiceProvider services,BidPhase1Checks.Fixture f){
  await Prepare(f);var n=0;void Check(bool x,string why){if(!x)throw new Exception(why);n++;}var id=f.Context.Tender.Id;
  using var scope=services.CreateScope();var service=scope.ServiceProvider.GetRequiredService<IAiBidManagement>();var data=scope.ServiceProvider.GetRequiredService<MemoryAiData>();var model=scope.ServiceProvider.GetRequiredService<TestOllama>();
  async Task<HttpResponseMessage> Get(string actor){client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor");client.DefaultRequestHeaders.Add("X-Bid-Test-Actor",actor);return await client.GetAsync("/AiAnalysis/Details/"+id);}
  foreach(var actor in new[]{"officer","manager","admin","requester","supplier","anonymous","evaluator","top"}){var response=await Get(actor);var allowed=actor is "officer" or "manager";Check(allowed?response.StatusCode==HttpStatusCode.OK:response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,"AI audience/stage "+actor);}
  Check(model.Calls==0,"GET must not generate");
  var page=await(await Get("officer")).Content.ReadAsStringAsync();Check(page.Contains("__RequestVerificationToken")&&page.Contains("file contents")&&page.Contains("JOD"),"compiled evidence/form/limitations");
  Check((await client.PostAsync("/AiAnalysis/Run/"+id,new FormUrlEncodedContent([]))).StatusCode==HttpStatusCode.BadRequest,"CSRF required");
  var token=WebUtility.HtmlDecode(Regex.Match(page,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"officer")],"MemoryOnly"));var snapshot=await service.PageAsync(user,id);
  var fields=new Dictionary<string,string>{{"__RequestVerificationToken",token},{"Version",snapshot.Evidence.Fingerprint},{"Confirm","false"}};
  Check((await client.PostAsync("/AiAnalysis/Run/"+id,new FormUrlEncodedContent(fields))).StatusCode==HttpStatusCode.BadRequest,"explicit confirmation");fields["Confirm"]="true";fields["Version"]="stale";
  Check((await client.PostAsync("/AiAnalysis/Run/"+id,new FormUrlEncodedContent(fields))).StatusCode==HttpStatusCode.Conflict,"stale source refused");fields["Version"]=snapshot.Evidence.Fingerprint;
  model.Fail=true;Check((await client.PostAsync("/AiAnalysis/Run/"+id,new FormUrlEncodedContent(fields))).StatusCode==HttpStatusCode.BadGateway&&data.Rows.Count==0,"bad model response never stored");model.Fail=false;
  var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  model.During=async()=>{entered.SetResult();await release.Task;};var pending=service.RunAsync(user,id,new(){Version=snapshot.Evidence.Fingerprint,Confirm=true});await entered.Task;
  try{await service.RunAsync(user,id,new(){Version=snapshot.Evidence.Fingerprint,Confirm=true});throw new Exception("Expected inference busy rejection");}catch(BidException e){Check(e.Status==409,"concurrent inference does not overload local model");}finally{release.SetResult();}
  await pending;model.During=null;
  Check((await client.PostAsync("/AiAnalysis/Run/"+id,new FormUrlEncodedContent(fields))).StatusCode==HttpStatusCode.Redirect&&data.Rows.Count==1,"explicit generation saved");var calls=model.Calls;
  await service.RunAsync(user,id,new(){Version=snapshot.Evidence.Fingerprint,Confirm=true});Check(model.Calls==calls&&data.Rows.Count==1,"duplicate request reuses current result");
  page=await(await Get("officer")).Content.ReadAsStringAsync();Check(page.Contains("&lt;script&gt;advisory-test&lt;/script&gt;")&&!page.Contains("<script>advisory-test</script>"),"advisory text encoded");Check(f.Context.Tender.Status==TenderStatus.Closed&&f.Store.Rows.Values.Single().Bid.Status==BidStatus.Submitted,"no workflow mutations");
  var originalTitle=f.Context.Tender.Title;f.Context.Tender.Title="Changed tender title";Check((await service.PageAsync(user,id)).Result is null,"stale stored analysis content not exposed");f.Context.Tender.Title=originalTitle;
  f.Store.Accounts["evaluator"]=f.Store.Accounts["evaluator"] with{EvaluationTenders=[id]};Check((await Get("evaluator")).StatusCode==HttpStatusCode.OK,"assigned evaluator allowed after closure");
  f.Context.Tender.Status=TenderStatus.AwaitingCommitteeDecision;f.Store.Accounts["top"]=f.Store.Accounts["top"] with{TopTenders=[id]};Check((await Get("top")).StatusCode==HttpStatusCode.OK,"assigned Top member allowed at final stage");
  f.Store.Accounts["top"]=f.Store.Accounts["top"] with{TopTenders=[]};Check((await Get("top")).StatusCode==HttpStatusCode.NotFound,"revoked Top appointment denied");f.Context.Tender.Status=TenderStatus.Closed;
  data.Rows.Clear();data.FailSave=true;try{await service.RunAsync(user,id,new(){Version=snapshot.Evidence.Fingerprint,Confirm=true});throw new Exception("Expected persistence failure");}catch(IOException){Check(data.Rows.Count==0,"persistence failure leaves no analysis");}data.FailSave=false;
  model.During=()=>{f.Context.Tender.Status=TenderStatus.UnderEvaluation;return Task.CompletedTask;};try{await service.RunAsync(user,id,new(){Version=snapshot.Evidence.Fingerprint,Confirm=true});throw new Exception("Expected source change refusal");}catch(BidException e){Check(e.Status==409&&data.Rows.Count==0,"changed source during inference cannot be saved");}model.During=null;
  snapshot=await service.PageAsync(user,id);f.Store.Accounts["officer"]=f.Store.Accounts["officer"] with{Enabled=false};try{await service.PageAsync(user,id);throw new Exception("Expected account revocation");}catch(BidException e){Check(e.Status==403,"fresh disabled actor denied");}
  Console.WriteLine($"PASS: {n} database-free AI service/compiled MVC authorization, stage, antiforgery, stale-input, encoding, no-decision and failure checks.");
 }
}
