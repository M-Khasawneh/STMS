using System.Security.Cryptography;
using System.Text;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public static class ComplianceRules
{
    public static bool Officer(BidAccount account) => account.Internal && account.Roles.Contains(ApplicationRoles.SourcingOfficer);
    public static void Review(BidAccount account, Bid bid, Tender tender)
    {
        BidRules.Require(Officer(account), 403, "Only an authorized Sourcing Officer may review compliance.");
        BidRules.Require(bid.TenderId == tender.Id && tender.Status == TenderStatus.Closed && bid.SubmittedAt.HasValue &&
            bid.Status is BidStatus.Submitted or BidStatus.UnderReview or BidStatus.Compliant or BidStatus.NonCompliant,
            409, "Compliance actions are available for submitted offers while the tender is Closed.");
    }
    public static void Reply(BidAccount account, ClarificationRequest request, Tender tender)
    {
        BidRules.Require(account.Supplier && request.RecipientUserId == account.UserId, 403, "Only the addressed supplier may reply.");
        BidRules.Require(tender.Status == TenderStatus.Closed && request.Status == ClarificationStatus.Pending,
            409, "This clarification is no longer awaiting a response at the compliance stage.");
    }
    public static void Decision(ComplianceDecisionForm form)
    {
        BidRules.Require(form.Outcome.HasValue && Enum.IsDefined(form.Outcome.Value), 400, "Choose a valid compliance outcome.");
        BidRules.Require(form.Outcome != ComplianceStatus.NonCompliant || !string.IsNullOrWhiteSpace(form.Reasons),
            400, "A non-compliant decision requires a reason.");
        BidRules.Require((form.Reasons?.Length ?? 0) <= 8000, 400, "The reason is too long.");
    }
    public static void Text(string? value, int maximum, string field) =>
        BidRules.Require(!string.IsNullOrWhiteSpace(value) && value.Length <= maximum, 400, $"{field} is required and must not exceed {maximum} characters.");
    public static void AttachmentCount(int count) => BidRules.Require(count is >= 0 and <= 10, 400, "A response may contain at most ten attachments.");
    public static string Version(Bid bid, IEnumerable<ComplianceReview> reviews, IEnumerable<ClarificationRequest> requests)
    {
        var value = bid.Status + "|" + string.Join("|", reviews.OrderBy(x => x.Id).Select(x => x.Id.ToString("N"))) + "|" +
            string.Join("|", requests.OrderBy(x => x.Id).Select(x => $"{x.Id:N}:{x.Status}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
    public static void Version(string? posted, string current) =>
        BidRules.Require(!string.IsNullOrEmpty(posted) && posted == current, 409, "This page has changed. Reload it before trying again.");
}
