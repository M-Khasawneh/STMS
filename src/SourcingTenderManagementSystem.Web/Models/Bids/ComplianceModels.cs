using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Bids;

public sealed class ComplianceDecisionForm
{
    [Required] public string Version { get; set; } = "";
    [Required] public ComplianceStatus? Outcome { get; set; }
    [StringLength(8000)] public string? Reasons { get; set; }
}
public sealed class ClarificationForm
{
    [Required] public string Version { get; set; } = "";
    [Required, StringLength(256)] public string Subject { get; set; } = "";
    [Required, StringLength(8000)] public string Description { get; set; } = "";
}
public sealed class ClarificationReplyForm
{
    [Required] public string Version { get; set; } = "";
    [Required, StringLength(8000)] public string Response { get; set; } = "";
    public List<IFormFile> Files { get; set; } = [];
}
public sealed record ClarificationReply(ClarificationResponse Response, IReadOnlyList<ClarificationAttachment> Attachments);
public sealed record ClarificationThread(ClarificationRequest Request, IReadOnlyList<ClarificationReply> Replies);
public sealed record CompliancePage(Guid BidId, string TenderTitle, string Version, bool CanReview,
    bool Supplier, string UserId, IReadOnlyList<ComplianceReview> Reviews, IReadOnlyList<ClarificationThread> Threads);
