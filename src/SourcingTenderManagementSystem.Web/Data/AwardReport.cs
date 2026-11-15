using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Models.Tenders;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed record AwardReportRow(Guid Id, string Tender, string Supplier, Guid TenderId, Guid BidId, Guid ApprovalId, DateTimeOffset AwardedAt, string? ContractReference);
public static class AwardReport
{
    public const string Definition = "This report lists existing final award records and any recorded contract reference. Missing references remain Not recorded. An award/reference does not establish that a contract has been drafted, signed or fulfilled; this system has no separate signed-contract document or execution data. No contract amounts, dates or documents are invented. Search matches tender title, supplier name or the recorded contract reference. Date filters use award time with an inclusive end date. Existing winner-bid confidentiality and current tender-specific membership restrictions apply.";
    public static ReportTable Table(IReadOnlyList<AwardReportRow> rows)
    {
        ReportRules.Limit(rows.Count);
        return new("Final awards and recorded contract references", ["Tender", "Winning supplier", "Awarded", "Contract reference", "Award ID", "Tender ID", "Winning bid ID", "Approval ID"],
            rows.Select(r => new[] { new ReportCell(r.Tender), new ReportCell(r.Supplier), new ReportCell(TenderTime.Display(r.AwardedAt)),
                new ReportCell(string.IsNullOrWhiteSpace(r.ContractReference) ? "Not recorded" : r.ContractReference), new ReportCell(r.Id.ToString()), new ReportCell(r.TenderId.ToString()), new ReportCell(r.BidId.ToString()), new ReportCell(r.ApprovalId.ToString()) }).ToArray());
    }
}
