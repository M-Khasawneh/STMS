using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class SupplierParticipationChecks
{
    internal static ReportDocument Document() => new("Tender participation", DateTimeOffset.UtcNow, [],
        [SupplierParticipationReport.Table([new(Guid.NewGuid(), "<script>synthetic supplier</script>", 4, 1, 1), new(Guid.NewGuid(), "Synthetic non-participant", 3, 0, 0)])], [SupplierParticipationReport.Definition]);
    internal static void Rules()
    {
        int n = 0; void Check(bool ok, string label) { if (!ok) throw new Exception(label); n++; }
        var table = SupplierParticipationReport.Table([new(Guid.NewGuid(), "Synthetic", 4, 1, 1), new(Guid.NewGuid(), "Zero offers", 4, 0, 0), new(Guid.NewGuid(), "No denominator", 0, 0, 0)]);
        Check(table.Rows[0][3].Number == 25 && table.Rows[1][3].Number == 0 && table.Rows[2][3].Number is null && table.Rows[2][3].Text == "Not available", "Approved participation formula and empty denominator");
        Check(table.Rows[0][1].Number == 4 && table.Rows[0][2].Number == 1 && table.Rows[0][4].Number == 1, "Independent actual-data measures, no combined score");
        Check(table.Columns.Length == 5 && !table.Columns.Any(c => c.Contains("price", StringComparison.OrdinalIgnoreCase) || c.Contains("score", StringComparison.OrdinalIgnoreCase)), "Only approved aggregate metadata columns");
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=ParticipationTranslationOnly;Integrated Security=True").Options);
        var bids = new BidData(db, null!); var reporting = new Reporting(db, bids, null!, null!, TimeProvider.System);
        var id = Guid.NewGuid();
        foreach (var role in ApplicationRoles.All)
        {
            var account = new BidAccount("synthetic", [role], role == ApplicationRoles.Supplier ? Guid.NewGuid() : null, true, true, true, [id], [id]);
            var sql = reporting.SupplierParticipationQuery(account, [id], DateTimeOffset.UtcNow, "synthetic").ToQueryString();
            if (role is ApplicationRoles.Admin or ApplicationRoles.RequestingDepartment or ApplicationRoles.Supplier) Check(sql.Contains("0 = 1") || sql.Contains("CAST(0 AS bit)"), "Restricted role produces empty confidential scope " + role);
            else { Check(sql.Contains("Invitations") && sql.Contains("TenderAwards") && sql.Contains("SubmittedAt"), "Real aggregate SQL translation for " + role); Check(sql.Contains("Status") && sql.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase), "Matching tender/stage predicates retained " + role); }
            if (role is ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee) Check(sql.Contains("CommitteeMembers") && sql.Contains("EffectiveTo"), "Current tender membership rechecked in submitted-offer query " + role);
        }
        var disabled = new BidAccount("synthetic", [ApplicationRoles.SourcingOfficer], null, false, false, true, [], []);
        Check(reporting.SupplierParticipationQuery(disabled, [id], DateTimeOffset.UtcNow).ToQueryString().Contains("0 = 1"), "Disabled actor has no aggregate scope");
        var doc = Document(); Check(ReportExports.Excel(doc).Length > 100 && ReportExports.Pdf(doc).Length > 100, "Existing real XLSX/PDF exporters support new report table");
        Console.WriteLine($"PASS: {n} database-free supplier participation formula, role/stage SQL translation and export checks.");
    }
}
