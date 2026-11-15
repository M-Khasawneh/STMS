namespace SourcingTenderManagementSystem.Web.Models.Analysis;
public sealed record AiAttachment(Guid Id,string Type,string ContentType,long Size,string ReferenceFingerprint);
public sealed record AiTextPage(int Number,string Method,string Text,bool Truncated,double? Confidence);
public sealed record AiTextResult(string Status,int TotalPages,IReadOnlyList<AiTextPage> Pages);
public sealed record AiDocumentEvidence(Guid BidId,Guid DocumentId,string Type,string ContentHash,AiTextResult Extraction);
public sealed record StoredAiAnalysis(int FormatVersion,AiAdvice Advice,IReadOnlyList<AiDocumentEvidence> Documents);
