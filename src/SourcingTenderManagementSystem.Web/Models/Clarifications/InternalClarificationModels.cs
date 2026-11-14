using System.ComponentModel.DataAnnotations;
using SourcingTenderManagementSystem.Web.Models.Entities;
namespace SourcingTenderManagementSystem.Web.Models.Clarifications;

public sealed record ClarificationPerson(string Id, string Name, Guid DepartmentId, string[] Roles);
public sealed record InternalClarificationContext(Guid Id, bool Requisition, string Title, PurchaseRequisition? Request,
    Tender? Tender, Guid SourceDepartment, string? ReviewerId, IReadOnlyList<CommitteeMember> Evaluation, IReadOnlyList<CommitteeMember> Top);
public sealed class InternalQuestionForm
{
    [Required] public string RecipientId { get; set; } = "";
    [Required, StringLength(256)] public string Subject { get; set; } = "";
    [Required, StringLength(8000)] public string Description { get; set; } = "";
    [Required] public string Version { get; set; } = "";
}
public sealed class InternalReplyForm
{
    [Required, StringLength(8000)] public string Response { get; set; } = "";
    [Required] public string Version { get; set; } = "";
}
public sealed record InternalThread(ClarificationRequest Request, ClarificationResponse? Response, string Sender, string Recipient,
    string Version, bool CanReply, bool CanResolve);
public sealed record InternalClarificationPage(InternalClarificationContext Context, IReadOnlyList<ClarificationPerson> Recipients,
    IReadOnlyList<InternalThread> Threads, string Version);
public sealed record InternalInboxItem(ClarificationRequest Request, string ContextTitle);
public sealed record InternalInbox(IReadOnlyList<InternalInboxItem> Items, int Page, bool More);
