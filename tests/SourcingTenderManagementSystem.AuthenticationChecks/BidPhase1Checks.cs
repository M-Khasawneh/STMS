using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;
public static class BidPhase1Checks
{
    private static int checks;
    public static async Task RunAsync()
    {
        checks = 0;
        var f = new Fixture();
        var id = await f.Service.CreateDraftAsync(f.User, f.Context.Tender.Id, f.Form());
        var details = await f.Service.DetailsAsync(f.User, id);
        Check(details.Snapshot.Bid.Status == BidStatus.Draft && details.Snapshot.Bid.SubmittedAt is null, "creation saves only Draft");
        Check(details.Snapshot.Bid.Currency == "JOD" && details.Snapshot.Items[0].OfferedQuantity == 1.25m, "server sets JOD and authoritative fractional quantities");
        Check(details.Total == 6.64320875m, "decimal totals retain exact products and optional costs");
        await Reject(() => f.Service.CreateDraftAsync(f.User, f.Context.Tender.Id, f.Form()),409,"duplicate bid creation");
        var form=f.Form();form.Version=details.Version;form.Items[0].UnitPrice=3;
        await f.Service.SaveDraftAsync(f.User,id,form);
        await Reject(()=>f.Service.SaveDraftAsync(f.User,id,form),409,"stale draft overwrite");
        var fresh=await f.Service.DetailsAsync(f.User,id);
        await Reject(()=>f.Service.SubmitAsync(f.User,id,fresh.Version),400,"technical proposal required before submission");
        f.AddDocument(id);fresh=await f.Service.DetailsAsync(f.User,id);
        await f.Service.SubmitAsync(f.User,id,fresh.Version);
        var submitted=await f.Service.DetailsAsync(f.User,id);
        Check(submitted.Snapshot.Bid.Status==BidStatus.Submitted&&submitted.Snapshot.Bid.SubmittedAt==f.Clock.Utc&&submitted.Snapshot.Bid.SubmittedAt.Value.Offset==TimeSpan.Zero,"explicit submission persists UTC timestamp");
        Check(f.Verifier.Calls==1,"submission calls the document-verification boundary");
        form.Version=submitted.Version;
        await Reject(()=>f.Service.SaveDraftAsync(f.User,id,form),409,"submitted content immutable");
        await Reject(()=>f.Service.SubmitAsync(f.User,id,submitted.Version),409,"submission cannot repeat");
        var other=f.Account with {UserId="other",SupplierId=Guid.NewGuid()};f.Store.Accounts[other.UserId]=other;
        await Reject(()=>f.Service.DetailsAsync(Principal("other"),id),404,"competing supplier cannot read bid");
        await Reject(()=>f.Service.SaveDraftAsync(Principal("other"),id,form),404,"competing supplier cannot edit bid");
        Check((await f.Service.ListAsync(Principal("other"))).Count==0,"supplier list excludes competitors");
        foreach(var status in new[]{InvitationStatus.Pending,InvitationStatus.Declined,InvitationStatus.Expired}){
            var a=new Fixture();a.Store.Participation.Invitation.Status=status;
            await Reject(()=>a.Service.CreateDraftAsync(a.User,a.Context.Tender.Id,a.Form()),403,"reject participation with "+status+" invitation");
        }
        foreach(var account in new[]{ f.Account with{SupplierApproved=false},f.Account with{Enabled=false},f.Account with{Roles=[ApplicationRoles.Supplier,ApplicationRoles.SourcingOfficer]} }){
            var a=new Fixture();a.Store.Accounts["supplier"]=account with{UserId="supplier",SupplierId=a.Account.SupplierId};
            await Reject(()=>a.Service.CreateDraftAsync(a.User,a.Context.Tender.Id,a.Form()),403,"reject ineligible supplier account");
        }
        foreach(var state in new[]{TenderStatus.Draft,TenderStatus.Published,TenderStatus.Closed,TenderStatus.UnderEvaluation,TenderStatus.Awarded}){
            var a=new Fixture();a.Context.Tender.Status=state;
            await Reject(()=>a.Service.CreateDraftAsync(a.User,a.Context.Tender.Id,a.Form()),409,"reject bid creation while "+state);
        }
        foreach(var instant in new[]{f.Context.Tender.SubmissionStart.AddTicks(-1),f.Context.Tender.SubmissionDeadline,f.Context.Tender.SubmissionDeadline.AddTicks(1)}){
            var a=new Fixture();a.Clock.Utc=instant;
            await Reject(()=>a.Service.CreateDraftAsync(a.User,a.Context.Tender.Id,a.Form()),409,"submission window excludes "+instant.ToString("O"));
        }
        var start=new Fixture();start.Clock.Utc=start.Context.Tender.SubmissionStart;
        _=await start.Service.CreateDraftAsync(start.User,start.Context.Tender.Id,start.Form());Check(true,"submission window includes start");
        foreach(var mutation in new Action<BidDraftForm>[] {x=>x.Items.RemoveAt(0), x=>x.Items.Add(x.Items[0]),x=>x.Items[0].ItemId=Guid.NewGuid(),x=>x.Items[0].ItemId=Guid.Empty,
            x=>x.Items[0].UnitPrice=0,x=>x.Items[0].UnitPrice=-1,x=>x.Items[0].UnitPrice=.0000001m,x=>x.Items[0].UnitPrice=1000000000000m,
            x=>x.Charges[0].Amount=-1,x=>x.PaymentTerms=" ",x=>x.DeliveryTimeframe="",x=>x.TechnicalDetails="",x=>x.OfferValidUntil=null}){
            var a=new Fixture();var bad=a.Form();mutation(bad);await Reject(()=>a.Service.CreateDraftAsync(a.User,a.Context.Tender.Id,bad),400,"reject invalid commercial/item input");
            Check(a.Store.Rows.Count==0,"invalid input leaves no bid");
        }
        var optional=new Fixture();var noCharges=optional.Form();noCharges.Charges=[];_=await optional.Service.CreateDraftAsync(optional.User,optional.Context.Tender.Id,noCharges);Check(true,"additional costs are optional");
        var race=new Fixture();var results=await Task.WhenAll(Attempt(()=>race.Service.CreateDraftAsync(race.User,race.Context.Tender.Id,race.Form())),Attempt(()=>race.Service.CreateDraftAsync(race.User,race.Context.Tender.Id,race.Form())));
        Check(results.Count(x=>x==0)==1&&results.Count(x=>x==409)==1&&race.Store.Rows.Count==1,"concurrent creation serialized to one bid in service contract");
        var edit=new Fixture();var eid=await edit.Service.CreateDraftAsync(edit.User,edit.Context.Tender.Id,edit.Form());var ev=(await edit.Service.DetailsAsync(edit.User,eid)).Version;
        var e1=edit.Form();e1.Version=ev;var e2=edit.Form();e2.Version=ev;e2.Items[0].UnitPrice=9;
        results=await Task.WhenAll(Attempt(()=>edit.Service.SaveDraftAsync(edit.User,eid,e1)),Attempt(()=>edit.Service.SaveDraftAsync(edit.User,eid,e2)));
        Check(results.Count(x=>x==0)==1&&results.Count(x=>x==409)==1,"concurrent stale edits do not both save");
        var late=new Fixture();var lid=await late.Service.CreateDraftAsync(late.User,late.Context.Tender.Id,late.Form());late.AddDocument(lid);
        var lv=(await late.Service.DetailsAsync(late.User,lid)).Version;late.Store.OnPersist=()=>late.Clock.Utc=late.Context.Tender.SubmissionDeadline;
        await Reject(()=>late.Service.SubmitAsync(late.User,lid,lv),409,"deadline rechecked after persistence before commit");
        Check(late.Store.Rows[lid].Bid.Status==BidStatus.Draft&&late.Store.Rows[lid].Bid.SubmittedAt is null,"failed final deadline check rolls back transaction contract");
        var revoked=new Fixture();revoked.Store.OnBegin=()=>revoked.Store.Accounts["supplier"]=revoked.Account with{SupplierApproved=false};
        await Reject(()=>revoked.Service.CreateDraftAsync(revoked.User,revoked.Context.Tender.Id,revoked.Form()),403,"eligibility rechecked inside transaction");
        var blocked=new Fixture(new BidSubmissionDocumentsPending());var bid=await blocked.Service.CreateDraftAsync(blocked.User,blocked.Context.Tender.Id,blocked.Form());blocked.AddDocument(bid);
        await Reject(async()=>await blocked.Service.SubmitAsync(blocked.User,bid,(await blocked.Service.DetailsAsync(blocked.User,bid)).Version),409,"legacy placeholder verifier remains fail-closed when explicitly injected");
        Check(blocked.Store.Rows[bid].Bid.Status==BidStatus.Draft,"document verification failure preserves draft");
        var gate=new Fixture();var gid=await gate.Service.CreateDraftAsync(gate.User,gate.Context.Tender.Id,gate.Form());gate.AddDocument(gid);
        var snap=gate.Store.Rows[gid];snap.Items[0].OfferedQuantity=999;
        await Reject(async()=>await gate.Service.SubmitAsync(gate.User,gid,(await gate.Service.DetailsAsync(gate.User,gid)).Version),400,"stored quantity tampering rejected at submission");
        snap=gate.Store.Rows[gid];snap.Items[0].OfferedQuantity=gate.Context.Items[0].Quantity;snap.Bid.Currency="USD";
        await Reject(async()=>await gate.Service.SubmitAsync(gate.User,gid,(await gate.Service.DetailsAsync(gate.User,gid)).Version),400,"stored foreign currency rejected");
        foreach(var corrupt in new Action<BidSnapshot>[] {
            s=>s.Documents[0].Size=5*1024*1024+1,
            s=>s.Documents[0].ContentType="text/html",
            s=>s.Documents[0].DocumentType="Financial Proposal",
            s=>s.Documents[0].BidId=Guid.NewGuid(),
            s=>s.Documents[0].StorageReference=""}) {
            var a=new Fixture();var key=await a.Service.CreateDraftAsync(a.User,a.Context.Tender.Id,a.Form());a.AddDocument(key);corrupt(a.Store.Rows[key]);
            await Reject(async()=>await a.Service.SubmitAsync(a.User,key,(await a.Service.DetailsAsync(a.User,key)).Version),400,"reject invalid document prerequisite metadata");
        }
        var many=new Fixture();var mid=await many.Service.CreateDraftAsync(many.User,many.Context.Tender.Id,many.Form());many.AddDocument(mid);
        many.Store.Rows[mid]=many.Store.Rows[mid] with{Documents=Enumerable.Range(0,11).Select(i=>new BidDocument{Id=Guid.NewGuid(),BidId=mid,DocumentType="Technical Proposal",Size=100,ContentType="application/pdf",StorageReference="test-only"}).ToArray()};
        await Reject(async()=>await many.Service.SubmitAsync(many.User,mid,(await many.Service.DetailsAsync(many.User,mid)).Version),400,"reject more than ten document records");
        var submitRace=new Fixture();var sid=await submitRace.Service.CreateDraftAsync(submitRace.User,submitRace.Context.Tender.Id,submitRace.Form());submitRace.AddDocument(sid);
        var sv=(await submitRace.Service.DetailsAsync(submitRace.User,sid)).Version;
        results=await Task.WhenAll(Attempt(()=>submitRace.Service.SubmitAsync(submitRace.User,sid,sv)),Attempt(()=>submitRace.Service.SubmitAsync(submitRace.User,sid,sv)));
        Check(results.Count(x=>x==0)==1&&results.Count(x=>x==409)==1,"concurrent submission succeeds once in service contract");
        var failed=new Fixture();var fid=await failed.Service.CreateDraftAsync(failed.User,failed.Context.Tender.Id,failed.Form());var before=(await failed.Service.DetailsAsync(failed.User,fid)).Version;
        failed.Store.OnPersist=()=>throw new IOException("Injected persistence failure");var change=failed.Form();change.Version=before;change.PaymentTerms="Changed";
        try{await failed.Service.SaveDraftAsync(failed.User,fid,change);throw new Exception("Expected persistence failure");}catch(IOException){}
        Check((await failed.Service.DetailsAsync(failed.User,fid)).Version==before,"persistence failure rolls back service transaction contract");
        Check(TenderTime.Display(f.Clock.Utc).Contains("Asia/Amman"),"existing timezone display reused");
        Authorization();SqlShape();
        Console.WriteLine($"PASS: {checks} database-free bid Phase 1 checks; no SQL connection opened.");
    }
    private static void Authorization()
    {
        var f=new Fixture();var t=f.Context.Tender;var b=new Bid{TenderId=t.Id,Status=BidStatus.Submitted,SubmittedAt=f.Clock.Utc};
        BidAccount Actor(params string[] roles)=>new("employee",roles,null,false,true,true,[],[]);
        foreach(var role in new[]{ApplicationRoles.SourcingOfficer,ApplicationRoles.SourcingManager}){
            var a=Actor(role);Check(!BidRules.CanRead(a,b,t,f.Account.SupplierId!.Value),role+" cannot read before closure");t.Status=TenderStatus.Closed;Check(BidRules.CanRead(a,b,t,f.Account.SupplierId.Value),role+" can read after closure");t.Status=TenderStatus.Open;
        }
        t.Status=TenderStatus.Closed;
        var evaluator=Actor(ApplicationRoles.EvaluationCommittee);Check(!BidRules.CanRead(evaluator,b,t,f.Account.SupplierId!.Value),"unassigned evaluator denied");
        evaluator=evaluator with{EvaluationTenders=[t.Id]};Check(BidRules.CanRead(evaluator,b,t,f.Account.SupplierId.Value),"assigned evaluator allowed after closure");
        var top=Actor(ApplicationRoles.TopCommittee) with{TopTenders=[t.Id]};Check(!BidRules.CanRead(top,b,t,f.Account.SupplierId.Value),"Top Committee denied before final-decision stage");t.Status=TenderStatus.AwaitingCommitteeDecision;Check(BidRules.CanRead(top,b,t,f.Account.SupplierId.Value),"assigned Top Committee allowed at final-decision stage");t.Status=TenderStatus.Awarded;Check(BidRules.CanRead(top,b,t,f.Account.SupplierId.Value),"assigned Top Committee retains awarded bid history required by V5");
        foreach(var a in new[]{Actor(ApplicationRoles.RequestingDepartment),Actor(ApplicationRoles.Admin),Actor(ApplicationRoles.Admin,ApplicationRoles.SourcingOfficer),Actor(ApplicationRoles.TopCommittee)})Check(!BidRules.CanRead(a,b,t,f.Account.SupplierId.Value),"unauthorized or unassigned internal actor denied");
        b.Status=BidStatus.Draft;Check(!BidRules.CanRead(evaluator,b,t,f.Account.SupplierId.Value),"internal actors cannot read drafts");
        b.Status=BidStatus.Submitted;b.SubmittedAt=null;Check(!BidRules.CanRead(evaluator,b,t,f.Account.SupplierId.Value),"unsubmitted record cannot masquerade as submitted");
    }
    private static void SqlShape()
    {
        using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var adapter=new BidData(db,null!);var account=new BidAccount("supplier",[ApplicationRoles.Supplier],Guid.NewGuid(),true,true,false,[],[]);
        var sql=adapter.VisibleQuery(account, DateTimeOffset.UtcNow).ToQueryString();Check(sql.Contains("SupplierProfileId")&&sql.Contains("EXISTS"),"supplier filtering translates into SQL ownership predicate");
        account=account with{Roles=[ApplicationRoles.EvaluationCommittee],SupplierId=null,EmployeeDepartment=true,EvaluationTenders=[Guid.NewGuid()]};
        sql=adapter.VisibleQuery(account, DateTimeOffset.UtcNow).ToQueryString();Check(sql.Contains("SubmittedAt")&&sql.Contains("Status")&&sql.Contains("EXISTS"),"internal filtering translates into SQL stage and submission predicates");
        Check(sql.Contains("CommitteeMembers")&&sql.Contains("EffectiveFrom")&&sql.Contains("CommitteeAssignments"),"SQL rechecks current tender-scoped committee membership");
        Check(db.Model.FindEntityType(typeof(Bid))!.GetProperties().All(x=>x.Name!="RowVersion"),"existing schema retained without new concurrency column");
    }
    private static async Task<int> Attempt(Func<Task> work){try{await work();return 0;}catch(BidException e){return e.Status;}}
    private static async Task Reject(Func<Task> work,int status,string name){Check(await Attempt(work)==status,name);}
    private static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    private static ClaimsPrincipal Principal(string id)=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id)],"Phase1Test"));
    internal sealed class Clock : TimeProvider {public DateTimeOffset Utc=new(2026,11,13,10,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Utc;}
    internal sealed class VerifiedTestDocuments : IBidSubmissionDocuments {public int Calls;public Task VerifyAsync(BidSnapshot snapshot,CancellationToken ct){Calls++;return Task.CompletedTask;}}
    internal sealed class Fixture
    {
        public Clock Clock {get;}=new();public MemoryData Store {get;}=new();public VerifiedTestDocuments Verifier {get;}=new();public BidManagement Service {get;}
        public ClaimsPrincipal User=>Principal("supplier");public BidContext Context=>Store.Context;public BidAccount Account=>Store.Accounts["supplier"];
        public Fixture(IBidSubmissionDocuments? verifier=null){Store.Accounts["supplier"]=new("supplier",[ApplicationRoles.Supplier],Guid.NewGuid(),true,true,false,[],[]);
            Store.Context=new(new Tender{Id=Guid.NewGuid(),Status=TenderStatus.Open,SubmissionStart=Clock.Utc,SubmissionDeadline=Clock.Utc.AddHours(1)},[new(){Id=Guid.NewGuid(),Description="First",Quantity=1.25m,Specifications="Spec"},new(){Id=Guid.NewGuid(),Description="Second",Quantity=2.5m,Specifications="Spec"}],[new(){Weight=100,ApprovedAt=Clock.Utc,ApprovedByUserId="manager"}]);
            var link=new TenderSupplier{Id=Guid.NewGuid(),TenderId=Context.Tender.Id,SupplierProfileId=Account.SupplierId!.Value};Store.Participation=new(link,new Invitation{TenderSupplierId=link.Id,Status=InvitationStatus.Accepted});Service=new(Store,Clock,verifier??Verifier);}
        public BidDraftForm Form()=>new(){DeliveryTimeframe="Two weeks",TechnicalDetails="Meets specifications",PaymentTerms="Payment terms",OfferValidUntil=new DateOnly(2026,12,1),Items=[new(){ItemId=Context.Items[0].Id,UnitPrice=1.234567m},new(){ItemId=Context.Items[1].Id,UnitPrice=2}],Charges=[new(){CostType="Tax",Amount=.1m}]};
        public void AddDocument(Guid id){var s=Store.Rows[id];Store.Rows[id]=s with{Documents=[new(){Id=Guid.NewGuid(),BidId=id,DocumentType="Technical Proposal",ContentType="application/pdf",Size=100,StorageReference="test-metadata-only"}]};}
    }
    internal sealed class MemoryData : IBidDocumentData
    {
        public Dictionary<string,BidAccount> Accounts=[];public Dictionary<Guid,BidSnapshot> Rows=[];public BidContext Context=null!;public BidParticipation Participation=null!;public Action? OnPersist,OnBegin,OnSaveDocuments,OnBeforeCommit,OnAfterCommit,OnReference;private readonly SemaphoreSlim gate=new(1);
        public Task<string?> SupplierNameAsync(Guid bidId, BidAccount account, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(Rows.TryGetValue(bidId, out var row) && BidRules.CanRead(account, row.Bid, Context.Tender, Participation.Link.SupplierProfileId)
                ? "Temporary supplier company" : null);
        public List<AuditLog> Activity = [];
        public List<Notification> Receipts = [];
        public Action? OnActivity;
        public Task RecordActivityAsync(AuditLog activity, Notification? receipt, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Activity.Add(Copy(activity));
            if (receipt is not null) Receipts.Add(Copy(receipt));
            OnActivity?.Invoke();
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AuditLog>> HistoryAsync(Guid bidId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuditLog>>(Activity.Where(x => x.RecordType == "Bid" && x.RecordId == bidId.ToString()).ToArray());
        private static T Copy<T>(T x)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x))!;
        public Task<BidAccount?> AccountAsync(ClaimsPrincipal p,DateTimeOffset now,CancellationToken ct)=>Task.FromResult(Accounts.GetValueOrDefault(p.FindFirstValue(ClaimTypes.NameIdentifier)??""));
        public async Task<IBidTransaction> BeginAsync(Guid id,CancellationToken ct){await gate.WaitAsync(ct);OnBegin?.Invoke();return new Transaction(this,Copy(Rows));}
        public Task<BidContext?> ContextAsync(Guid id,CancellationToken ct)=>Task.FromResult<BidContext?>(id==Context.Tender.Id?Copy(Context):null);
        public Task<BidParticipation?> ParticipationAsync(Guid id,Guid supplier,CancellationToken ct)=>Task.FromResult<BidParticipation?>(id==Context.Tender.Id&&supplier==Participation.Link.SupplierProfileId?Copy(Participation):null);
        public Task<BidSnapshot?> ReadAsync(Guid id,BidAccount a,DateTimeOffset now,CancellationToken ct)=>Task.FromResult(Rows.TryGetValue(id,out var s)&&BidRules.CanRead(a,s.Bid,Context.Tender,Participation.Link.SupplierProfileId)?Copy(s):null);
        public Task<IReadOnlyList<Bid>> ListAsync(BidAccount a,DateTimeOffset now,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Bid>>(Rows.Values.Where(s=>BidRules.CanRead(a,s.Bid,Context.Tender,Participation.Link.SupplierProfileId)).Select(s=>Copy(s.Bid)).ToArray());
        public Task<bool> ExistsAsync(Guid link,Guid? except,CancellationToken ct)=>Task.FromResult(Rows.Values.Any(s=>s.Bid.TenderSupplierId==link&&s.Bid.Id!=except));
        public async Task PersistAsync(BidSnapshot s,bool create,CancellationToken ct){await Task.Yield();ct.ThrowIfCancellationRequested();Rows[s.Bid.Id]=Copy(s);OnPersist?.Invoke();}
        public Task SaveDocumentsAsync(Guid bidId,IReadOnlyList<BidDocument> documents,CancellationToken ct){ct.ThrowIfCancellationRequested();Rows[bidId]=Rows[bidId] with{Documents=Copy(documents.ToArray())};OnSaveDocuments?.Invoke();return Task.CompletedTask;}
        public Task<bool> ReferenceExistsAsync(string reference,CancellationToken ct){ct.ThrowIfCancellationRequested();OnReference?.Invoke();return Task.FromResult(Rows.Values.Any(x=>x.Documents.Any(d=>d.StorageReference==reference)));}
        private sealed class Transaction(MemoryData store,Dictionary<Guid,BidSnapshot> backup):IBidTransaction{private readonly List<AuditLog> auditBackup=Copy(store.Activity);private readonly List<Notification> receiptBackup=Copy(store.Receipts);private bool committed;public Task CommitAsync(CancellationToken ct){ct.ThrowIfCancellationRequested();store.OnBeforeCommit?.Invoke();committed=true;store.OnAfterCommit?.Invoke();return Task.CompletedTask;}public ValueTask DisposeAsync(){if(!committed){store.Rows=backup;store.Activity=auditBackup;store.Receipts=receiptBackup;}store.gate.Release();return ValueTask.CompletedTask;}}
    }
}
