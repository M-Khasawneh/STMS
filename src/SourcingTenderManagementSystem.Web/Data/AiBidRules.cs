using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Analysis;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.Web.Data;
public static class AiBidRules {
 public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()}};
 public static AiAttachment Attachment(BidDocument d)=>new(d.Id,d.DocumentType,d.ContentType,d.Size,Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(d.StorageReference))));
 public static AiEvidence Evidence(BidReviewComparison comparison){
  var offers=comparison.Offers.OrderBy(o=>o.Details.Snapshot.Bid.Id).Select(o=>{
   var s=o.Details.Snapshot;var b=s.Bid;BidRules.Require(s.Items.Count==comparison.RequestedItems.Count&&b.Currency=="JOD"&&s.Items.All(i=>i.UnitPrice>0),409,"Offer commercial or item records conflict.");var facts=new List<AiFact>();
   void Fact(string key,string value)=>facts.Add(new(b.Id+":"+key,value));
   Fact("total",o.Details.Total.ToString(CultureInfo.InvariantCulture)+" JOD");Fact("delivery",b.DeliveryTimeframe);Fact("technical",b.TechnicalDetails);Fact("payment",b.PaymentTerms);Fact("validity",b.OfferValidUntil?.ToString("yyyy-MM-dd")??"Unknown");Fact("status",b.Status.ToString());
   foreach(var item in comparison.RequestedItems.OrderBy(i=>i.Id)){
    var lines=s.Items.Where(i=>i.PurchaseRequisitionItemId==item.Id).ToArray();
    BidRules.Require(lines.Length==1&&lines[0].OfferedQuantity==item.Quantity,409,"Offer item records conflict with the source requisition.");var line=lines[0];
    Fact("item:"+item.Id,JsonSerializer.Serialize(new{item.Description,item.Specifications,RequestedQuantity=item.Quantity,line.UnitPrice,Total=item.Quantity*line.UnitPrice,OfferedSpecifications=line.Specifications},Json));
   }
   foreach(var cost in s.Charges.OrderBy(c=>c.Id))Fact("charge:"+cost.Id,JsonSerializer.Serialize(new{cost.CostType,cost.Amount,cost.Currency,cost.Notes},Json));
   // Stable extraction profile and private reference fingerprints invalidate older inventory-only results.
   Fact("files",JsonSerializer.Serialize(new{ExtractionProfile=AiDocumentRules.Profile,Attachments=s.Documents.OrderBy(d=>d.Id).Select(Attachment)},Json));
   var missing=s.Documents.Any(d=>d.DocumentType=="Technical Proposal")?Array.Empty<string>():new[]{"Technical Proposal"};
   return new AiOffer(b.Id,o.SupplierName??"Supplier",b.Status,o.Details.Total,b.DeliveryTimeframe,b.TechnicalDetails,facts,missing){Attachments=s.Documents.OrderBy(d=>d.Id).Select(Attachment).ToArray()};
  }).ToArray();
  BidRules.Require(offers.Length>0&&offers.Length<=20,400,"Analysis supports between one and twenty visible submitted offers at a time.");
  var evidence=new AiEvidence(comparison.Tender.Id,comparison.Tender.Title,comparison.Tender.Status,offers,"");
  var json=JsonSerializer.Serialize(evidence,Json);BidRules.Require(System.Text.Encoding.UTF8.GetByteCount(json)<=16000,400,"This tender exceeds the local analysis input limit. Use the existing comparison and human review.");
  return evidence with{Fingerprint=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))};
 }
 public static AiAdvice Parse(string json,AiEvidence source){
  BidRules.Require(json.Length<=64000,502,"The analysis response exceeded its allowed size.");AiAdvice? advice;
  try{advice=JsonSerializer.Deserialize<AiAdvice>(json,Json);}catch(JsonException){throw new BidException(502,"The model returned an invalid analysis format. No result was saved.");}
  BidRules.Require(advice is not null&&advice.Offers is not null&&advice.Offers.Length==source.Offers.Count&&advice.Offers.All(o=>o is not null)&&advice.Offers.Select(o=>o.BidId).Distinct().Count()==source.Offers.Count&&advice.Offers.All(o=>source.Offers.Any(b=>b.BidId==o.BidId)),502,"The model omitted or introduced an offer. No result was saved.");
  bool Text(string? value,int limit)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=limit;
  foreach(var offer in advice.Offers){var facts=source.Offers.Single(b=>b.BidId==offer.BidId).Facts.Select(f=>f.Key).ToHashSet();
   BidRules.Require(offer.Strengths is not null&&offer.Weaknesses is not null&&offer.Strengths.Length<=8&&offer.Weaknesses.Length<=8,502,"Invalid analysis observations.");
   foreach(var observation in offer.Strengths.Concat(offer.Weaknesses))BidRules.Require(observation is not null&&Text(observation.Text,1000)&&observation.Evidence is {Length:>0 and <=8}&&observation.Evidence.All(facts.Contains),502,"An observation has no valid evidence reference.");
  }
  BidRules.Require(Text(advice.Comparison,4000)&&Text(advice.Recommendation,4000)&&(!advice.RecommendedBidId.HasValue||source.Offers.Any(o=>o.BidId==advice.RecommendedBidId&&o.Status==BidStatus.Compliant)),502,"Invalid preliminary recommendation or non-compliant suggested offer. No result was saved.");
  return advice;
 }
}
