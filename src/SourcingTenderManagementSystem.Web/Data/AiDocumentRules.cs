using System.Text.Json;
using System.Text.RegularExpressions;
using SourcingTenderManagementSystem.Web.Models.Analysis;
namespace SourcingTenderManagementSystem.Web.Data;
public static class AiDocumentRules {
 public const string Profile="private-document-text-v1";
 public static bool ValidText(AiTextResult? r)=>r is not null &&
  (r.Status is "Complete" or "Partial" or "NoText" or "Unreadable" or "Unavailable" or "Timeout") &&
  r.TotalPages>=0 && r.Pages is not null && r.Pages.Count<=10 && r.Pages.All(p=>p is not null) &&
  r.Pages.Select(p=>p.Number).Distinct().Count()==r.Pages.Count &&
  r.Pages.All(p=>p.Number>0 && p.Number<=r.TotalPages && (p.Method is "PDF text" or "OCR" or "PDF text + OCR") && p.Text is not null && p.Text.Length<=1200 &&
   (p.Confidence is null || double.IsFinite(p.Confidence.Value) && p.Confidence is >=0 and <=1)) &&
  (r.Status is not("Complete" or "Partial") || r.TotalPages>0 && r.Pages.Count==Math.Min(10,r.TotalPages));
 public static void Validate(AiEvidence source,IReadOnlyList<AiDocumentEvidence> documents){
  var expected=source.Offers.SelectMany(o=>o.Attachments.Select(d=>new{Bid=o.BidId,File=d})).ToArray();
  BidRules.Require(documents.Count==expected.Length&&documents.All(d=>d is not null)&&documents.Select(d=>(d.BidId,d.DocumentId)).Distinct().Count()==documents.Count&&documents.All(d=>d is not null&&expected.Any(e=>e.Bid==d.BidId&&e.File.Id==d.DocumentId&&e.File.Type==d.Type)&&Regex.IsMatch(d.ContentHash??"","\\A[0-9A-F]{64}\\z")&&ValidText(d.Extraction)),409,"Extracted document records do not match the authorized attachment inventory.");
 }
 public static AiEvidence Compose(AiEvidence source,IReadOnlyList<AiDocumentEvidence> documents,bool budget=false){
  Validate(source,documents);var offers=source.Offers.Select(o=>o with{Facts=o.Facts.ToList()}).ToArray();
  foreach(var d in documents){var offer=offers.Single(o=>o.BidId==d.BidId);((List<AiFact>)offer.Facts).Add(new(d.BidId+":document:"+d.DocumentId+":coverage",JsonSerializer.Serialize(new{d.Type,d.Extraction.Status,d.Extraction.TotalPages,ProcessedPages=d.Extraction.Pages.Count,Notice="Bounded extracted excerpts only. Omitted/unreadable text is uncertainty, not a missing file. OCR may contain errors."},AiBidRules.Json)));}
  AiEvidence Build()=>source with{Offers=offers};
  int Size()=>System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Build(),AiBidRules.Json));
  BidRules.Require(Size()<=16000,400,"Document inventory exceeds the local AI context limit. Use protected document review.");
  var kept=new List<AiDocumentEvidence>();
  foreach(var d in documents){var pages=new List<AiTextPage>();var facts=(List<AiFact>)offers.Single(o=>o.BidId==d.BidId).Facts;
   foreach(var p in d.Extraction.Pages){var text=p.Text;var key=d.BidId+":document:"+d.DocumentId+":page:"+p.Number;
    string Value(string excerpt)=>JsonSerializer.Serialize(new{p.Number,p.Method,p.Confidence,Truncated=p.Truncated||excerpt.Length<p.Text.Length,Text=excerpt},AiBidRules.Json);
    if(text.Length>0){facts.Add(new(key,Value(text)));if(budget&&Size()>16000){facts.RemoveAt(facts.Count-1);var low=0;var high=text.Length;while(low<high){var mid=(low+high+1)/2;facts.Add(new(key,Value(text[..mid])));var fits=Size()<=16000;facts.RemoveAt(facts.Count-1);if(fits)low=mid;else high=mid-1;}text=text[..low];if(text.Length>0&&char.IsHighSurrogate(text[^1]))text=text[..^1];if(text.Length>0)facts.Add(new(key,Value(text)));}}
    pages.Add(p with{Text=text,Truncated=p.Truncated||text.Length<p.Text.Length});
   }
   kept.Add(d with{Extraction=d.Extraction with{Pages=pages}});
  }
  BidRules.Require(Size()<=16000,400,"Extracted document evidence exceeds the local AI context limit.");
  return Build() with{Documents=kept};
 }
}
