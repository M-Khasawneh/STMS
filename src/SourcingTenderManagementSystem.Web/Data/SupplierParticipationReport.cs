using SourcingTenderManagementSystem.Web.Models.Reports;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed record SupplierParticipationRow(Guid SupplierId, string Company, int Invitations, int SubmittedOffers, int Awards);
public static class SupplierParticipationReport
{
    public const string Definition = "Supplier participation = explicitly submitted offers / invitations x 100. Drafts are excluded. Each tender-supplier invitation counts once. Counts use only the matching tenders whose confidential participation data you may currently view after closure; suppliers with no invitations in that scope are omitted. No delivery-quality score, prices or proposal contents are included. This is participation, not fulfillment performance.";
    public static ReportTable Table(IReadOnlyList<SupplierParticipationRow> rows)
    {
        ReportRules.Limit(rows.Count);
        return new("Supplier participation in authorized tenders", ["Supplier", "Invitations", "Submitted offers", "Participation %", "Recorded awards"], rows.Select(r =>
        {
            var rate = ReportRules.Participation(r.SubmittedOffers, r.Invitations);
            return new[] { new ReportCell(r.Company), ReportCell.Value(r.Invitations), ReportCell.Value(r.SubmittedOffers), rate.HasValue ? ReportCell.Value(rate.Value) : new ReportCell("Not available"), ReportCell.Value(r.Awards) };
        }).ToArray());
    }
}
