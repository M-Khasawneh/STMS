using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class BidPhase2Checks
{
    private static int checks;
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nprivate bid evidence\n%%EOF");
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==");
    public static async Task RunAsync()
    {
        checks = 0;
        await HappyPath(); await Validation(); await StoredContentValidation(); await Failures(); await Recovery(); await Access(); await Concurrency();
        Console.WriteLine($"PASS: {checks} database-free bid Phase 2 checks (real isolated filesystem, transactional memory data; no SQL connections).");
    }
    private static async Task HappyPath()
    {
        using var h = new Harness(); await h.Start();
        var first = await h.Upload("Technical Proposal", "../proposal.PDF");
        var doc = h.Document(first);
        Check(doc.OriginalName == "proposal.PDF" && doc.ContentType == "application/pdf", "sanitize display filename and derive MIME from validated contents");
        Check(doc.StorageReference.StartsWith(h.BidId.ToString("N") + "/") && !doc.StorageReference.Contains("proposal"), "generated bid-scoped storage reference never uses original filename");
        Check(!Directory.Exists(Path.Combine(h.Root, "wwwroot")) && h.Pending == 0 && h.Binaries == 1, "private files and journal completion outside wwwroot");
        Check(doc.UploadedAt.Offset == TimeSpan.Zero, "document timestamp stored in UTC");
        var oldRef = doc.StorageReference;
        await h.Documents.ReplaceAsync(h.User, h.BidId, first, await h.Version(), "Technical Proposal", File(Png, "revised.png"));
        Check(h.Document(first).StorageReference != oldRef && h.Binaries == 1 && !System.IO.File.Exists(h.PathFor(oldRef)), "replacement retains document id and removes old unreferenced bytes after commit");
        await using (var download = await h.Documents.DownloadAsync(h.User, h.BidId, first))
        {
            using var bytes = new MemoryStream(); await download.Content.CopyToAsync(bytes);
            Check(bytes.ToArray().SequenceEqual(Png), "download returns verified private bytes");
            Check(download.ContentType == "application/octet-stream" && download.Headers["Cache-Control"] == "no-store" && download.Headers["X-Content-Type-Options"] == "nosniff", "attachment-only download contract carries safe headers");
        }
        var financial = await h.Upload("Financial Proposal");
        await h.Documents.DeleteAsync(h.User, h.BidId, financial, await h.Version());
        Check(h.F.Store.Rows[h.BidId].Documents.Count == 1 && h.Binaries == 1, "delete removes metadata before retiring unreferenced bytes");
        var version = await h.Version();
        await h.Bids.SubmitAsync(h.User, h.BidId, version);
        Check(h.F.Store.Rows[h.BidId].Bid.Status == BidStatus.Submitted, "real file verifier integrates with bid final submission");
        await Reject(() => h.Upload("Supporting Document"), 409, "upload after submission denied");
        await Reject(() => h.Documents.DeleteAsync(h.User, h.BidId, first, version), 409, "delete after submission denied");
        await Reject(() => h.Documents.ReplaceAsync(h.User, h.BidId, first, version, "Technical Proposal", File(Pdf,"new.pdf")), 409, "replacement after submission denied");
        Check(h.Binaries == 1 && h.Pending == 0, "rejected submitted mutations do not change files");
        using var last = new Harness(); await last.Start(); var technical = await last.Upload("Technical Proposal");
        await last.Documents.DeleteAsync(last.User, last.BidId, technical, await last.Version());
        await Reject(async () => await last.Bids.SubmitAsync(last.User, last.BidId, await last.Version()), 400, "removing last Technical Proposal is allowed in Draft but blocks submission");
    }
    private static async Task Validation()
    {
        using var h = new Harness(); await h.Start();
        foreach (var file in new[] { File(Pdf,"script.exe"),File(Png,"mismatch.pdf"),File(Pdf,"file.jpeg"),File([],"empty.pdf"),File(Encoding.ASCII.GetBytes("%PDF-1.4 without ending"),"broken.pdf"),File(new byte[5*1024*1024+1],"large.pdf"),File(Pdf,"line\r\nbreak.pdf") })
            await Reject(async () => await h.Documents.UploadAsync(h.User,h.BidId,await h.Version(),"Technical Proposal",file),400,"reject invalid extension, content, filename, or size");
        Check(h.Binaries==0 && h.Pending==0 && h.F.Store.Rows[h.BidId].Documents.Count==0,"invalid uploads leave neither metadata nor files");
        await Reject(async()=>await h.Documents.UploadAsync(h.User,h.BidId,await h.Version(),"Company Registration Certificate",File(Pdf,"registration.pdf")),400,"registration document types are not bid document types");
        await Reject(async()=>await h.Documents.UploadAsync(h.User,h.BidId,await h.Version(),"Technical Proposal",new LyingFile()),400,"bounded reader rejects stream exceeding its declared length and 5 MB");
        var exact = new byte[5*1024*1024]; Pdf.CopyTo(exact,0); Encoding.ASCII.GetBytes("%%EOF").CopyTo(exact,exact.Length-5);
        var max=await h.Documents.UploadAsync(h.User,h.BidId,await h.Version(),"Technical Proposal",File(exact,"maximum.pdf"));
        Check(h.Document(max).Size==5*1024*1024,"exactly 5 MB accepted");
        await h.Upload("Financial Proposal","photo.jpg",[255,216,255,0,255,217]);
        while(h.F.Store.Rows[h.BidId].Documents.Count<10) await h.Upload("Supporting Document");
        await Reject(()=>h.Upload("Supporting Document"),400,"eleventh attachment rejected");
        await h.Documents.ReplaceAsync(h.User,h.BidId,max,await h.Version(),"Technical Proposal",File(Pdf,"replacement.pdf"));
        Check(h.F.Store.Rows[h.BidId].Documents.Count==10 && h.Binaries==10,"replacement at ten-attachment limit is supported");
        var stale=await h.Version();var one=h.Document(max);
        await h.Documents.ReplaceAsync(h.User,h.BidId,max,stale,"Technical Proposal",File(Pdf,"again.pdf"));
        await Reject(()=>h.Documents.DeleteAsync(h.User,h.BidId,max,stale),409,"stale document mutation rejected");
        await Reject(async()=>await h.Documents.ReplaceAsync(h.User,h.BidId,Guid.NewGuid(),await h.Version(),"Technical Proposal",File(Pdf,"new.pdf")),404,"foreign or missing document id rejected");
        using var altered = new Harness(); await altered.Start();var key=await altered.Upload("Technical Proposal");
        var target=altered.PathFor(altered.Document(key).StorageReference);var changed=Pdf.ToArray();changed[12]^=1;System.IO.File.WriteAllBytes(target,changed);
        await Reject(async()=>await altered.Bids.SubmitAsync(altered.User,altered.BidId,await altered.Version()),409,"same-size content tampering blocked by hash verification");
        await Reject(async()=>{await using var d=await altered.Documents.DownloadAsync(altered.User,altered.BidId,key);},409,"tampered bytes cannot be downloaded");
        System.IO.File.Delete(target);
        await Reject(async()=>await altered.Bids.SubmitAsync(altered.User,altered.BidId,await altered.Version()),503,"missing physical file blocks submission");
        Check(altered.F.Store.Rows[altered.BidId].Bid.Status==BidStatus.Draft,"file verification failures leave bid Draft");
        foreach(var reference in new[]{"../outside.pdf", "C:/Windows/win.ini",new string('a',64)+".bin", Guid.NewGuid().ToString("N")+"/"+new string('a',64)+"-"+new string('b',64)+".bin"})
        {
            var invalid=new BidDocument{BidId=h.BidId,Size=Pdf.Length,OriginalName="x.pdf",ContentType="application/pdf",StorageReference=reference};
            try { await using var stream=await h.Store.OpenVerifiedAsync(invalid,CancellationToken.None);throw new Exception("Expected path rejection"); }catch(InvalidDataException){Check(true,"path traversal, registration reference and cross-bid reference rejected");}
        }
        try{_=new PrivateBidFiles(new Environment(Path.Combine(h.Root,"unsafe")){WebRootPath=Path.Combine(h.Root,"unsafe","App_Data")});throw new Exception("Expected web root rejection");}catch(InvalidOperationException){Check(true,"misconfigured publicly exposed storage root rejected");}
    }
    private static async Task StoredContentValidation()
    {
        // Matching storage hashes are insufficient: actual format and persisted metadata must agree too.
        using var h = new Harness(); await h.Start();
        var id = await h.Upload("Technical Proposal");
        foreach (var alter in new Action<BidDocument>[] {
            d => d.Size++, d => d.ContentType = "image/png", d => d.OriginalName = "proposal.png" })
        {
            var original = h.Document(id);
            var savedSize = original.Size; var savedType = original.ContentType; var savedName = original.OriginalName;
            alter(original);
            await Reject(async () => await h.Bids.SubmitAsync(h.User, h.BidId, await h.Version()), 409, "submission checks stored size, MIME and extension against actual bytes");
            original = h.Document(id);
            original.Size = savedSize; original.ContentType = savedType; original.OriginalName = savedName;
        }
        var stored = h.Document(id);
        var invalidBytes = Encoding.ASCII.GetBytes("this is not a PDF document");
        var reference = PrivateBidFiles.NewReference(h.BidId, invalidBytes);
        var intent = new BidFileIntent(Guid.NewGuid(), h.BidId, h.F.Context.Tender.Id, reference, stored.StorageReference);
        await h.Store.WriteIntentAsync(intent, CancellationToken.None);
        await h.Store.SaveAsync(h.BidId, reference, new(invalidBytes, "proposal.pdf", "application/pdf"), CancellationToken.None);
        stored.StorageReference = reference; stored.Size = invalidBytes.Length;
        await Reject(async () => await h.Bids.SubmitAsync(h.User, h.BidId, await h.Version()), 409, "matching hash cannot bypass real file-signature verification on submission");
        await Reject(async () => { await using var d = await h.Documents.DownloadAsync(h.User, h.BidId, id); }, 409, "matching hash cannot bypass file-signature verification on download");
    }
    private static async Task Failures()
    {
        using var h=new Harness();await h.Start();
        h.Faults.AfterSave=()=>throw new IOException("Injected after file write");
        await Reject(()=>h.Upload("Technical Proposal"),503,"file write failure handled");
        Check(h.Binaries==0&&h.Pending==0&&h.F.Store.Rows[h.BidId].Documents.Count==0,"failed file operation reconciles new bytes");
        h.Faults.AfterSave=null;h.F.Store.OnSaveDocuments=()=>throw new IOException("Injected persistence failure");
        await Reject(()=>h.Upload("Technical Proposal"),503,"metadata write failure handled");
        Check(h.Binaries==0&&h.Pending==0&&h.F.Store.Rows[h.BidId].Documents.Count==0,"metadata failure rolls back and removes new file");
        h.F.Store.OnSaveDocuments=null;var id=await h.Upload("Technical Proposal");var old=h.Document(id).StorageReference;
        h.F.Store.OnBeforeCommit=()=>throw new InvalidOperationException("Injected pre-commit failure");
        await ExpectFailure(async()=>await h.Documents.ReplaceAsync(h.User,h.BidId,id,await h.Version(),"Technical Proposal",File(Png,"changed.png")),"failed replacement transaction");
        h.F.Store.OnBeforeCommit=null;
        Check(h.Document(id).StorageReference==old&&System.IO.File.Exists(h.PathFor(old))&&h.Binaries==1,"failed replacement retains original metadata and bytes");
        h.Faults.FailDelete=true;
        await h.Documents.DeleteAsync(h.User,h.BidId,id,await h.Version());
        Check(h.F.Store.Rows[h.BidId].Documents.Count==0&&h.Binaries==1&&h.Pending==1,"post-commit delete failure leaves a durable cleanup intent");
        h.Faults.FailDelete=false;await h.Documents.RecoverAsync(h.User,h.BidId);
        Check(h.Binaries==0&&h.Pending==0,"cleanup retry removes retired bytes and intent");
        h.F.Store.OnAfterCommit=()=>throw new InvalidOperationException("Lost commit acknowledgement");
        await ExpectFailure(()=>h.Upload("Technical Proposal"),"uncertain committed upload outcome");
        h.F.Store.OnAfterCommit=null;
        Check(h.F.Store.Rows[h.BidId].Documents.Count==1&&h.Binaries==1&&h.Pending==0,"uncertain commit retains database-referenced bytes");
        var technical=h.F.Store.Rows[h.BidId].Documents.Single().Id;
        h.Faults.AfterSave=()=>h.F.Clock.Utc=h.F.Context.Tender.SubmissionDeadline;
        await Reject(async()=>await h.Documents.ReplaceAsync(h.User,h.BidId,technical,await h.Version(),"Technical Proposal",File(Png,"late.png")),409,"deadline crossing during file IO rejects commit");
        Check(h.Document(technical).OriginalName=="proposal.pdf"&&h.Binaries==1&&h.Pending==0,"deadline rollback preserves original file");
        h.Faults.AfterSave=null;h.F.Clock.Utc=h.F.Context.Tender.SubmissionStart;
        using var cancellation=new CancellationTokenSource();h.Faults.AfterSave=()=>cancellation.Cancel();
        try{await h.Documents.UploadAsync(h.User,h.BidId,await h.Version(),"Supporting Document",File(Pdf,"cancel.pdf"),cancellation.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){Check(true,"cancellation propagated");}
        Check(h.Binaries==1&&h.Pending==0&&h.F.Store.Rows[h.BidId].Documents.Count==1,"cancelled upload rolls back metadata and cleans bytes using independent cleanup token");
    }
    private static async Task Recovery()
    {
        using var h=new Harness();await h.Start();
        var file=await SupplierUploadValidation.ReadAsync(File(Pdf,"crash.pdf"),CancellationToken.None);
        var reference=PrivateBidFiles.NewReference(h.BidId,file.Bytes);var intent=new BidFileIntent(Guid.NewGuid(),h.BidId,h.F.Context.Tender.Id,reference,null);
        await h.Store.WriteIntentAsync(intent,CancellationToken.None);await h.Store.SaveAsync(h.BidId,reference,file,CancellationToken.None);
        var restarted=new BidDocumentManagement(h.F.Store,h.Bids,new PrivateBidFiles(new Environment(h.Root)),h.F.Clock,NullLogger<BidDocumentManagement>.Instance);
        await restarted.RecoverAsync(h.User,h.BidId);
        Check(h.Binaries==0&&h.Pending==0,"new service instance recovers crash-before-database-save files");
        await h.Store.WriteIntentAsync(intent,CancellationToken.None);
        System.IO.File.WriteAllBytes(h.PathFor(reference)+".upload",Pdf);
        await restarted.RecoverAsync(h.User,h.BidId);
        Check(!Directory.EnumerateFiles(h.Root,"*.upload",SearchOption.AllDirectories).Any()&&h.Pending==0,"crash-mid-write partial file recovered");
        h.F.Store.OnSaveDocuments=()=>throw new IOException("Injected rollback");h.F.Store.OnReference=()=>throw new IOException("Database unavailable during recovery");
        await Reject(()=>h.Upload("Technical Proposal"),503,"failed database write with temporarily unavailable reference check");
        Check(h.Pending==1&&h.Binaries==1,"uncertain reference state retains durable intent and file for safe retry");
        h.F.Store.OnSaveDocuments=null;h.F.Store.OnReference=null;await restarted.RecoverAsync(h.User,h.BidId);
        Check(h.Pending==0&&h.Binaries==0,"retry after database recovery removes only unreferenced file");
        var id=await h.Upload("Technical Proposal");var current=h.Document(id);
        var bogusRetirement=new BidFileIntent(Guid.NewGuid(),h.BidId,h.F.Context.Tender.Id,null,current.StorageReference);
        await h.Store.WriteIntentAsync(bogusRetirement,CancellationToken.None);await restarted.RecoverAsync(h.User,h.BidId);
        Check(h.Binaries==1&&h.Pending==0,"recovery never deletes a still-referenced document");
        // A file of the same size with a valid PDF signature still cannot impersonate stored content.
        var wrong=Pdf.ToArray();wrong[15]^=2;System.IO.File.WriteAllBytes(h.PathFor(current.StorageReference),wrong);
        await Reject(async()=>{await using var d=await h.Documents.DownloadAsync(h.User,h.BidId,id);},409,"restarted service preserves content-integrity checks");
    }
    private static async Task Access()
    {
        using var h=new Harness();await h.Start();var id=await h.Upload("Technical Proposal");
        ClaimsPrincipal Actor(string userId, params string[] roles){h.F.Store.Accounts[userId]=new(userId,roles,null,false,true,true,[],[]);return Principal(userId);}
        var officer=Actor("officer",ApplicationRoles.SourcingOfficer);var manager=Actor("manager",ApplicationRoles.SourcingManager);
        var evaluator=Actor("evaluator",ApplicationRoles.EvaluationCommittee);var top=Actor("top",ApplicationRoles.TopCommittee);
        var admin=Actor("admin",ApplicationRoles.Admin);var requester=Actor("requester",ApplicationRoles.RequestingDepartment);
        var other=h.F.Account with{UserId="other",SupplierId=Guid.NewGuid()};h.F.Store.Accounts["other"]=other;
        foreach(var user in new[]{officer,manager,evaluator,top,admin,requester,Principal("other"),new ClaimsPrincipal()})
            await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(user,h.BidId,id);},"unauthorized draft document download denied");
        await Denied(async()=>await h.Documents.UploadAsync(Principal("other"),h.BidId,await h.Version(),"Supporting Document",File(Pdf,"x.pdf")),"competing supplier upload denied");
        await Denied(async()=>await h.Documents.DeleteAsync(Principal("other"),h.BidId,id,await h.Version()),"competing supplier deletion denied");
        await Denied(async()=>await h.Documents.ReplaceAsync(Principal("other"),h.BidId,id,await h.Version(),"Technical Proposal",File(Png,"x.png")),"competing supplier replacement denied");
        foreach(var status in new[]{InvitationStatus.Pending,InvitationStatus.Declined,InvitationStatus.Expired}){
            h.F.Store.Participation.Invitation.Status=status;
            await Reject(()=>h.Upload("Supporting Document"),403,"document mutation requires Accepted invitation: "+status);
        }
        h.F.Store.Participation.Invitation.Status=InvitationStatus.Accepted;
        foreach(var status in new[]{TenderStatus.Published,TenderStatus.Closed}){h.F.Context.Tender.Status=status;await Reject(()=>h.Upload("Supporting Document"),409,"document mutation requires Open tender");}
        h.F.Context.Tender.Status=TenderStatus.Open;
        h.F.Store.Accounts["supplier"]=h.F.Account with{SupplierApproved=false};await Reject(()=>h.Upload("Supporting Document"),403,"suspended supplier mutation denied");
        await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(h.User,h.BidId,id);},"suspended supplier download denied");
        h.F.Store.Accounts["supplier"]=h.F.Account with{SupplierApproved=true};
        await h.Bids.SubmitAsync(h.User,h.BidId,await h.Version());
        foreach(var user in new[]{officer,manager})await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(user,h.BidId,id);},"submitted bid remains sealed before tender closure");
        h.F.Context.Tender.Status=TenderStatus.Closed;
        foreach(var user in new[]{officer,manager}){await using var d=await h.Documents.DownloadAsync(user,h.BidId,id);Check(d.Size==Pdf.Length,"authorized officer/manager reads after closure");}
        await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(evaluator,h.BidId,id);},"unassigned evaluator denied");
        h.F.Store.Accounts["evaluator"]=h.F.Store.Accounts["evaluator"] with{EvaluationTenders=[h.F.Context.Tender.Id]};
        await using(var d=await h.Documents.DownloadAsync(evaluator,h.BidId,id))Check(true,"assigned evaluator reads after closure");
        h.F.Store.Accounts["evaluator"]=h.F.Store.Accounts["evaluator"] with{EvaluationTenders=[]};
        await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(evaluator,h.BidId,id);},"revoked evaluator assignment denied");
        h.F.Store.Accounts["top"]=h.F.Store.Accounts["top"] with{TopTenders=[h.F.Context.Tender.Id]};
        await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(top,h.BidId,id);},"Top Committee denied before final-decision stage");
        h.F.Context.Tender.Status=TenderStatus.AwaitingCommitteeDecision;
        await using(var d=await h.Documents.DownloadAsync(top,h.BidId,id))Check(true,"assigned Top Committee reads at final-decision stage");
        foreach(var user in new[]{admin,requester,Principal("other")})await Denied(async()=>{await using var d=await h.Documents.DownloadAsync(user,h.BidId,id);},"Admin, requester and competitor remain denied after closure");
        await Reject(async()=>{await using var d=await h.Documents.DownloadAsync(h.User,h.BidId,Guid.NewGuid());},404,"document id must belong to selected bid");
    }
    private static async Task Concurrency()
    {
        using var h=new Harness();await h.Start();var version=await h.Version();
        var results=await Task.WhenAll(Attempt(()=>h.Documents.UploadAsync(h.User,h.BidId,version,"Technical Proposal",File(Pdf,"one.pdf"))),Attempt(()=>h.Documents.UploadAsync(h.User,h.BidId,version,"Technical Proposal",File(Pdf,"two.pdf"))));
        Check(results.Count(x=>x==0)==1&&results.Count(x=>x==409)==1&&h.Binaries==1&&h.Pending==0,"concurrent uploads respect version and cleanly serialize");
        var id=h.F.Store.Rows[h.BidId].Documents.Single().Id;version=await h.Version();
        results=await Task.WhenAll(Attempt(()=>h.Bids.SubmitAsync(h.User,h.BidId,version)),Attempt(()=>h.Documents.ReplaceAsync(h.User,h.BidId,id,version,"Technical Proposal",File(Png,"replacement.png"))));
        Check(results.Count(x=>x==0)==1&&results.Count(x=>x==409)==1&&h.Binaries==1&&h.Pending==0,"replacement and submission cannot both commit from the same version");
    }
    private static IFormFile File(byte[] bytes,string name)=>new FormFile(new MemoryStream(bytes),0,bytes.Length,"file",name){Headers=new HeaderDictionary(),ContentType="text/html"};
    private static ClaimsPrincipal Principal(string id)=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id)],"Phase2Test"));
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    private static async Task<int> Attempt(Func<Task> work){try{await work();return 0;}catch(BidException e){return e.Status;}}
    private static async Task Reject(Func<Task> work,int code,string label)=>Check(await Attempt(work)==code,label);
    private static async Task Denied(Func<Task> work,string label){var code=await Attempt(work);Check(code is 403 or 404,label);}
    private static async Task ExpectFailure(Func<Task> work,string label){try{await work();throw new Exception("Expected injected failure");}catch(InvalidOperationException){Check(true,label);}}
    private sealed class Harness : IDisposable
    {
        public string Root {get;}=Path.Combine(Path.GetTempPath(),"bid-doc-checks-"+Guid.NewGuid().ToString("N"));
        public BidPhase1Checks.Fixture F {get;}=new();public PrivateBidFiles Store {get;}public FaultingFiles Faults {get;}public BidManagement Bids {get;}public BidDocumentManagement Documents {get;}public Guid BidId;public ClaimsPrincipal User=>F.User;
        public Harness(){Store=new(new Environment(Root));Faults=new(Store);Bids=new(F.Store,F.Clock,new BidSubmissionDocuments(Faults));Documents=new(F.Store,Bids,Faults,F.Clock,NullLogger<BidDocumentManagement>.Instance);}
        public async Task Start()=>BidId=await Bids.CreateDraftAsync(User,F.Context.Tender.Id,F.Form());
        public async Task<string> Version()=>(await Bids.DetailsAsync(User,BidId)).Version;
        public async Task<Guid> Upload(string type,string name="proposal.pdf",byte[]? bytes=null)=>await Documents.UploadAsync(User,BidId,await Version(),type,File(bytes??Pdf,name));
        public BidDocument Document(Guid id)=>F.Store.Rows[BidId].Documents.Single(x=>x.Id==id);
        public string PathFor(string reference)=>Path.Combine(Root,"App_Data","BidDocuments",reference.Replace('/',Path.DirectorySeparatorChar));
        public int Binaries=>Directory.Exists(Root)?Directory.EnumerateFiles(Root,"*.bin",SearchOption.AllDirectories).Count():0;
        public int Pending=>Directory.Exists(Root)?Directory.EnumerateFiles(Root,"*.json",SearchOption.AllDirectories).Count():0;
        public void Dispose(){var resolved=Path.GetFullPath(Root);var temp=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;if(!resolved.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(resolved).StartsWith("bid-doc-checks-",StringComparison.Ordinal))throw new Exception("Unsafe temporary cleanup path");if(Directory.Exists(resolved))Directory.Delete(resolved,true);}
    }
    private sealed class FaultingFiles(IPrivateBidFiles inner):IPrivateBidFiles
    {
        public Action? AfterSave;public bool FailDelete;
        public Task WriteIntentAsync(BidFileIntent i,CancellationToken ct)=>inner.WriteIntentAsync(i,ct);
        public Task<IReadOnlyList<BidFileIntent>> IntentsAsync(Guid b,CancellationToken ct)=>inner.IntentsAsync(b,ct);
        public async Task SaveAsync(Guid b,string r,ValidatedSupplierFile f,CancellationToken ct){await inner.SaveAsync(b,r,f,ct);AfterSave?.Invoke();}
        public Task<Stream> OpenVerifiedAsync(BidDocument d,CancellationToken ct)=>inner.OpenVerifiedAsync(d,ct);
        public Task DeleteAsync(Guid b,string r,CancellationToken ct){if(FailDelete)throw new IOException("Injected delete failure");return inner.DeleteAsync(b,r,ct);}
        public Task CompleteIntentAsync(BidFileIntent i,CancellationToken ct)=>inner.CompleteIntentAsync(i,ct);
    }
    private sealed class Environment(string root):IWebHostEnvironment
    {
        public string ApplicationName {get;set;}="BidTests";public string EnvironmentName {get;set;}="Tests";public string ContentRootPath {get;set;}=root;
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();public string WebRootPath {get;set;}=Path.Combine(root,"wwwroot");public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
    }
    private sealed class LyingFile:IFormFile
    {
        public string ContentType=>"application/pdf";public string ContentDisposition=>"";public IHeaderDictionary Headers {get;}=new HeaderDictionary();public long Length=>1;public string Name=>"file";public string FileName=>"dishonest.pdf";
        public Stream OpenReadStream()=>new MemoryStream(new byte[5*1024*1024+1]);public void CopyTo(Stream target)=>throw new NotSupportedException();public Task CopyToAsync(Stream target,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
