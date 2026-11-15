using System.Security.Claims;
using System.Security.Cryptography;
using SourcingTenderManagementSystem.Web.Models.Analysis;
namespace SourcingTenderManagementSystem.Web.Data;
public interface IAiBidDocuments {
 Task<AiEvidence> EnrichAsync(ClaimsPrincipal user,AiEvidence source,CancellationToken ct);
 Task ValidateAsync(ClaimsPrincipal user,AiEvidence source,IReadOnlyList<AiDocumentEvidence> documents,CancellationToken ct);
}
public sealed class AiBidDocuments(IBidData data,BidReviewPages pages,IPrivateBidFiles files,IBidTextExtractor extractor,TimeProvider clock):IAiBidDocuments {
 private async Task<byte[]> Bytes(ClaimsPrincipal user,AiEvidence source,Guid bidId,AiAttachment attachment,CancellationToken ct){
  await pages.RequireReviewerAsync(user,ct);var actor=await data.AccountAsync(user,clock.GetUtcNow(),ct);BidRules.Require(actor is {Internal:true},403,"Internal document analysis access is required.");
  var snapshot=await data.ReadAsync(bidId,actor,clock.GetUtcNow(),ct);BidRules.Require(snapshot is not null&&snapshot.Bid.TenderId==source.TenderId,404,"Offer document unavailable.");
  var document=snapshot.Documents.SingleOrDefault(d=>d.Id==attachment.Id&&d.BidId==bidId);BidRules.Require(document is not null&&AiBidRules.Attachment(document)==attachment,409,"The private attachment changed.");
  try{await using var content=await files.OpenVerifiedAsync(document,ct);using var buffer=new MemoryStream();var block=new byte[8192];int n;while((n=await content.ReadAsync(block,ct))>0){BidRules.Require(buffer.Length+n<=5242880,409,"Private file exceeded its approved size.");buffer.Write(block,0,n);}return buffer.ToArray();}
  catch(InvalidDataException){throw new BidException(409,"A private document failed verification. No analysis was saved.");}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){throw new BidException(503,"A private document is unavailable. No analysis was saved.");}
 }
 public async Task<AiEvidence> EnrichAsync(ClaimsPrincipal user,AiEvidence source,CancellationToken ct){
  BidRules.Require(source.Offers.Sum(o=>o.Attachments.Count)<=20,400,"Local extraction supports at most twenty authorized attachments per analysis.");
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromMinutes(2));var token=deadline.Token;var documents=new List<AiDocumentEvidence>();
  try{foreach(var offer in source.Offers)foreach(var file in offer.Attachments){var bytes=await Bytes(user,source,offer.BidId,file,token);var text=await extractor.ExtractAsync(bytes,file.ContentType,token);BidRules.Require(AiDocumentRules.ValidText(text),503,"The text extractor returned an invalid result.");BidRules.Require(text.Status is not("Unavailable" or "Timeout"),503,"Document processing is temporarily unavailable or timed out. No analysis was saved; retry when the local OCR tools are ready.");documents.Add(new(offer.BidId,file.Id,file.Type,Convert.ToHexString(SHA256.HashData(bytes)),text));}
   var result=AiDocumentRules.Compose(source,documents,true);await ValidateAsync(user,source,result.Documents,token);return result;
  }catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new BidException(503,"Document extraction timed out. Human document review remains available.");}
 }
 public async Task ValidateAsync(ClaimsPrincipal user,AiEvidence source,IReadOnlyList<AiDocumentEvidence> documents,CancellationToken ct){
  AiDocumentRules.Validate(source,documents);
  foreach(var offer in source.Offers)foreach(var file in offer.Attachments){var bytes=await Bytes(user,source,offer.BidId,file,ct);var expected=documents.Single(d=>d.BidId==offer.BidId&&d.DocumentId==file.Id);BidRules.Require(Convert.ToHexString(SHA256.HashData(bytes))==expected.ContentHash,409,"Document content changed after extraction.");}
 }
}
