using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.Web.Models.Analysis;
public sealed record AiFact(string Key,string Value);
public sealed record AiOffer(Guid BidId,string Supplier,BidStatus Status,decimal Total,string Delivery,string Technical,IReadOnlyList<AiFact> Facts,IReadOnlyList<string> MissingDocuments) { [System.Text.Json.Serialization.JsonIgnore] public IReadOnlyList<AiAttachment> Attachments {get;init;}=[]; }
public sealed record AiEvidence(Guid TenderId,string Title,TenderStatus Status,IReadOnlyList<AiOffer> Offers,string Fingerprint) { [System.Text.Json.Serialization.JsonIgnore] public IReadOnlyList<AiDocumentEvidence> Documents {get;init;}=[]; }
public sealed class AiObservation { public string Text {get;set;}=""; public string[] Evidence {get;set;}=[]; }
public sealed class AiOfferAdvice { public Guid BidId {get;set;} public AiObservation[] Strengths {get;set;}=[]; public AiObservation[] Weaknesses {get;set;}=[]; }
public sealed class AiAdvice { public AiOfferAdvice[] Offers {get;set;}=[]; public string Comparison {get;set;}=""; public string Recommendation {get;set;}=""; public Guid? RecommendedBidId {get;set;} }
public sealed record AiResult(Guid Id,string Model,DateTimeOffset CreatedAt,AiAdvice Advice) { public IReadOnlyList<AiDocumentEvidence> Documents {get;init;}=[]; }
public sealed record AiPage(AiEvidence Evidence,AiResult? Result);
public sealed class AiRunForm { [Required] public string Version {get;set;}=""; public bool Confirm {get;set;} }
