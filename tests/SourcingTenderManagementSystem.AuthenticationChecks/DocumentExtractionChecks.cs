using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SkiaSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;
using SourcingTenderManagementSystem.Web.Models.Analysis;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
internal static class DocumentExtractionChecks {
 internal sealed class EnvironmentFixture : IWebHostEnvironment {
  public string ApplicationName{get;set;}="ExtractionChecks";public string EnvironmentName{get;set;}="Test";
  public string ContentRootPath{get;set;}=Path.GetFullPath("src/SourcingTenderManagementSystem.Web");public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
  public string WebRootPath{get;set;}=Path.GetFullPath("src/SourcingTenderManagementSystem.Web/wwwroot");public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
 }
 internal static byte[] Image(string text="SYNTHETIC QUOTATION 125 JOD",SKEncodedImageFormat format=SKEncodedImageFormat.Png,int width=1400){
  using var bitmap=new SKBitmap(width,280);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);using var paint=new SKPaint{Color=SKColors.Black,IsAntialias=true};using var font=new SKFont(SKTypeface.FromFamilyName("Arial"),52);canvas.DrawText(text,30,130,SKTextAlign.Left,font,paint);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(format,95);return data.ToArray();
 }
 internal static byte[] TextPdf(int pages=1){var builder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();var font=builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);for(int i=0;i<pages;i++){var page=builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);page.AddText("Synthetic equipment quotation. Delivery within fourteen days. Total 125 JOD.",14,new UglyToad.PdfPig.Core.PdfPoint(40,700),font);}return builder.Build();}
 internal static byte[] ScannedPdf(bool password=false){var bytes=Image();using var doc=new PdfDocument();var page=doc.AddPage();using(var graphics=XGraphics.FromPdfPage(page)){using var image=XImage.FromStream(new MemoryStream(bytes));graphics.DrawImage(image,20,40,550,110);}if(password){doc.SecuritySettings.UserPassword="synthetic-protection";doc.SecuritySettings.OwnerPassword="synthetic-owner";}using var output=new MemoryStream();doc.Save(output,false);return output.ToArray();}
 internal static async Task LocalModel(){
  var extractor=new BidTextExtractor(new EnvironmentFixture(),Options.Create(new BidTextOptions()));var text=await extractor.ExtractAsync(TextPdf(),"application/pdf",default);
  if(!text.Pages.Any(p=>p.Text.Contains("125")))throw new Exception("No actual document text to analyze");
  var original=AiAnalysisChecks.Synthetic();var attachment=new AiAttachment(Guid.NewGuid(),"Technical Proposal","application/pdf",12,new string('B',64));var source=original with{Offers=[original.Offers[0] with{Attachments=[attachment]}]};
  var enriched=AiDocumentRules.Compose(source,[new(source.Offers[0].BidId,attachment.Id,attachment.Type,new string('A',64),text)],true);
  using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false}){Timeout=TimeSpan.FromMinutes(3)};
  var model=new OllamaBidClient(http,Options.Create(new OllamaOptions()));var advice=await model.AnalyzeAsync(enriched,default);
  var cited=advice.Offers.SelectMany(o=>o.Strengths.Concat(o.Weaknesses)).SelectMany(o=>o.Evidence).Count(k=>k.Contains(":document:")&&k.Contains(":page:"));
  if(cited==0)throw new Exception("Model did not cite the actual extracted page facts");
  Console.WriteLine($"PASS: real local Ollama analyzed actual synthetic PDF text with {cited} page evidence citation(s). No SQL, real bids or private supplier files used.");
 }
 internal static async Task Run(){
  int checks=0;void Check(bool value,string label){if(!value)throw new Exception("Extraction: "+label);checks++;Console.WriteLine("PASS: "+label);}
  var env=new EnvironmentFixture();var extractor=new BidTextExtractor(env,Options.Create(new BidTextOptions()));
  var text=await extractor.ExtractAsync(TextPdf(),"application/pdf",default);Check(text.Status=="Complete"&&text.Pages.Single().Method=="PDF text"&&text.Pages.Single().Text.Contains("fourteen"),"real PdfPig text extraction in isolated application worker");
  foreach(var format in new[]{SKEncodedImageFormat.Png,SKEncodedImageFormat.Jpeg}){var result=await extractor.ExtractAsync(Image(format:format),format==SKEncodedImageFormat.Png?"image/png":"image/jpeg",default);Check(result.Pages.Any(p=>p.Method=="OCR"&&p.Text.Contains("125")),"real "+format+" OCR via native Tesseract");}
  var scan=await extractor.ExtractAsync(ScannedPdf(),"application/pdf",default);Check(scan.Pages.Any(p=>p.Method=="OCR"&&p.Text.Contains("125")),"real PDFium rendering and scanned PDF OCR");
  var encrypted=await extractor.ExtractAsync(ScannedPdf(true),"application/pdf",default);Check(encrypted.Status=="Unreadable"&&encrypted.Pages.Count==0,"encrypted PDF never produces fabricated text");
  var bad=await extractor.ExtractAsync("%PDF-1.4 broken"u8.ToArray(),"application/pdf",default);Check(bad.Status=="Unreadable","malformed PDF handled");
  var many=await extractor.ExtractAsync(TextPdf(11),"application/pdf",default);Check(many.Status=="Partial"&&many.TotalPages==11&&many.Pages.Count==10,"page cap reported explicitly");
  var large=await extractor.ExtractAsync(Image(width:6001),"image/png",default);Check(large.Status=="Unreadable","oversized image dimensions rejected before OCR decode");
  var missing=new BidTextExtractor(env,Options.Create(new BidTextOptions{LanguageDataPath=Path.Combine(Path.GetTempPath(),"nonexistent-language-"+Guid.NewGuid().ToString("N"))}));
  Check((await missing.ExtractAsync(Image(),"image/png",default)).Status=="Unavailable","missing language resources report unavailable");Check((await missing.ExtractAsync(TextPdf(),"application/pdf",default)).Pages.Count==1,"native PDF text does not require OCR language files");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{await extractor.ExtractAsync(Image(),"image/png",cancel.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){Check(true,"cancelled extraction terminates without a result");}}
  var activeImage=Image();using(var activeCancel=new CancellationTokenSource()){activeCancel.CancelAfter(100);var elapsed=System.Diagnostics.Stopwatch.StartNew();try{await extractor.ExtractAsync(activeImage,"image/png",activeCancel.Token);throw new Exception("Expected active worker cancellation");}catch(OperationCanceledException){Check(elapsed.Elapsed<TimeSpan.FromSeconds(5),"active worker cancellation returns promptly after cleanup");}}
  var source=AiAnalysisChecks.Synthetic();var attachment=new AiAttachment(Guid.NewGuid(),"Technical Proposal","application/pdf",12,new string('B',64));source=source with{Offers=[source.Offers[0] with{Attachments=[attachment]}]};
  var docs=new[]{new AiDocumentEvidence(source.Offers[0].BidId,attachment.Id,attachment.Type,new string('A',64),text)};
  try{AiDocumentRules.Compose(source,[null!]);throw new Exception("Expected null provenance refusal");}catch(BidException){Check(true,"null document provenance refused safely");}
  var enriched=AiDocumentRules.Compose(source,docs,true);Check(enriched.Fingerprint==source.Fingerprint&&enriched.Offers[0].Facts.Any(f=>f.Key.EndsWith(":page:1")&&f.Value.Contains("fourteen")),"page facts bound to bid/document/page without replacing authoritative totals");
  var roundTrip=AiDocumentRules.Compose(source,enriched.Documents);Check(roundTrip.Offers[0].Facts.SequenceEqual(enriched.Offers[0].Facts),"preserved excerpts reproduce exact citation facts");
  foreach(var malformed in new[]{docs[0] with{BidId=Guid.NewGuid()},docs[0] with{DocumentId=Guid.NewGuid()},docs[0] with{ContentHash="invalid"}}){try{AiDocumentRules.Compose(source,[malformed]);throw new Exception("Expected provenance refusal");}catch(BidException){Check(true,"foreign or invalid document provenance rejected");}}
  var pages=Enumerable.Range(1,10).Select(i=>new AiTextPage(i,"OCR",new string('ع',1200),false,.7)).ToArray();var bounded=AiDocumentRules.Compose(source,[docs[0] with{Extraction=new("Complete",10,pages)}],true);
  Check(System.Text.Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(bounded,AiBidRules.Json))<=16000&&bounded.Documents[0].Extraction.Pages.Any(p=>p.Truncated),"context budget shortens text with explicit coverage flags");
  Check(AiDocumentRules.Compose(source,bounded.Documents).Offers[0].Facts.SequenceEqual(bounded.Offers[0].Facts),"budgeted excerpts retain stable provenance on reload");
  await Isolation(env,extractor,Check);
  var external=System.Environment.GetEnvironmentVariable("STMS_ARABIC_OCR_FIXTURE");if(!string.IsNullOrEmpty(external)){var arabic=await extractor.ExtractAsync(await File.ReadAllBytesAsync(external),"image/png",default);Check(arabic.Pages.Any(p=>p.Text.Contains("عرض")||p.Text.Contains("دينار")),"real Arabic OCR from synthetic shaped text image");}else Console.WriteLine("LIMITATION: Arabic language loaded but shaped-image recognition not requested by this invocation.");
  Console.WriteLine($"PASS: {checks} database-free actual PDF/OCR, worker, provenance and document-isolation checks. No SQL or supplier documents used.");
 }
 private sealed class StatusExtractor(string status):IBidTextExtractor {public Task<AiTextResult> ExtractAsync(byte[] bytes,string type,CancellationToken ct)=>Task.FromResult(new AiTextResult(status,0,[]));}
 private static async Task Isolation(EnvironmentFixture env,IBidTextExtractor extractor,Action<bool,string> check){
  var root=Path.Combine(Path.GetTempPath(),"stms-extraction-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var f=new BidPhase1Checks.Fixture();var id=await f.Service.CreateDraftAsync(f.User,f.Context.Tender.Id,f.Form());
   var bytes=TextPdf();var files=new PrivateBidFiles(new EnvironmentFixture{ContentRootPath=root,WebRootPath=Path.Combine(root,"wwwroot")});
   var reference=PrivateBidFiles.NewReference(id,bytes);var validated=await SupplierUploadValidation.ReadAsync(new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes),0,bytes.Length,"file","synthetic.pdf"),default);await files.SaveAsync(id,reference,validated,default);
   var row=f.Store.Rows[id];var document=new BidDocument{Id=Guid.NewGuid(),BidId=id,DocumentType="Technical Proposal",OriginalName="synthetic.pdf",StorageReference=reference,ContentType="application/pdf",Size=bytes.Length};f.Store.Rows[id]=row with{Documents=[document]};
   await f.Service.SubmitAsync(f.User,id,(await f.Service.DetailsAsync(f.User,id)).Version);f.Context.Tender.Status=TenderStatus.Closed;f.Store.Accounts["officer"]=new("officer",[ApplicationRoles.SourcingOfficer],null,false,true,true,[],[]);
   var reviewer=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"officer")],"Memory"));var pages=new BidReviewPages(f.Store,f.Service,f.Clock);var service=new AiBidDocuments(f.Store,pages,files,extractor,f.Clock);
   var source=AiBidRules.Evidence(await pages.CompareAsync(reviewer,f.Context.Tender.Id,default));var result=await service.EnrichAsync(reviewer,source,default);check(result.Documents.Single().ContentHash==Convert.ToHexString(SHA256.HashData(bytes)),"verified private bytes feed actual extraction with SHA-256 provenance");
   foreach(var status in new[]{"Unavailable","Timeout"}){var failed=new AiBidDocuments(f.Store,pages,files,new StatusExtractor(status),f.Clock);try{await failed.EnrichAsync(reviewer,source,default);throw new Exception("Expected OCR failure refusal");}catch(BidException e){check(e.Status==503,"transient OCR "+status+" never becomes a cacheable successful analysis");}}
   try{await service.EnrichAsync(reviewer,source with{TenderId=Guid.NewGuid()},default);throw new Exception("Expected tender substitution refusal");}catch(BidException e){check(e.Status==404,"tender substitution cannot read the private document");}
   f.Store.Accounts["admin"]=new("admin",[ApplicationRoles.Admin],null,false,true,true,[],[]);foreach(var user in new[]{"admin","supplier"}){try{await service.ValidateAsync(new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user)],"Memory")),source,result.Documents,default);throw new Exception("Expected isolation refusal");}catch(BidException){check(true,"document analysis denies "+user);}}
   var path=Path.Combine(root,"App_Data","BidDocuments",reference.Replace('/',Path.DirectorySeparatorChar));await File.WriteAllBytesAsync(path,new byte[bytes.Length]);
   try{await service.ValidateAsync(reviewer,source,result.Documents,default);throw new Exception("Expected tamper refusal");}catch(BidException e){check(e.Status==409,"tampered private original cannot reuse cached document evidence");}
  }finally{var full=Path.GetFullPath(root);var parent=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("stms-extraction-check-"))throw new Exception("Unsafe cleanup path");Directory.Delete(full,true);}
 }
}
