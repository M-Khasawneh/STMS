using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class AwardReportChecks
{
    internal static ReportDocument Document() => new("Awards & contract references", DateTimeOffset.UtcNow, [new("Matching recorded awards", "2")],
        [AwardReport.Table([new(Guid.NewGuid(), "Synthetic awarded tender", "<script>award supplier</script>", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2026,11,15,10,0,0,TimeSpan.Zero), null),
            new(Guid.NewGuid(), "Synthetic second award", "Synthetic winner", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "=HYPERLINK(\"https://example.invalid\")")])], [AwardReport.Definition]);
    internal static void Rules()
    {
        int n = 0; void Check(bool ok, string label) { if (!ok) throw new Exception(label); n++; }
        var document = Document(); var table = document.Tables.Single();
        Check(table.Rows[0][3].Text == "Not recorded" && table.Rows[0][2].Text.Contains("13:00"), "Missing reference stays unknown; UTC award time displays in Amman");
        Check(table.Columns.Length == 8 && table.Rows[1][3].Text.StartsWith("=HYPERLINK"), "References preserved literally without invented contract data");
        Check(!table.Columns.Any(c => c.Contains("amount", StringComparison.OrdinalIgnoreCase) || c.Contains("signed", StringComparison.OrdinalIgnoreCase)), "No inferred contract values/signature evidence");
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=AwardTranslationOnly;Integrated Security=True").Options);
        var bids = new BidData(db, null!); var reporting = new Reporting(db, bids, null!, null!, TimeProvider.System);
        var tender = Guid.NewGuid();
        foreach (var role in ApplicationRoles.All)
        {
            var actor = new BidAccount("synthetic", [role], role == ApplicationRoles.Supplier ? Guid.NewGuid() : null, true, true, true, [tender], [tender]);
            var sql = reporting.AwardQuery(actor, new() { TenderId = tender, Search = "synthetic-ref", DepartmentId = Guid.NewGuid(), From = new(2026,11,15), To = new(2026,11,15) }).ToQueryString();
            if (role is ApplicationRoles.Admin or ApplicationRoles.RequestingDepartment or ApplicationRoles.Supplier) Check(!ReportRules.Categories(actor).Contains("awards") && sql.Contains("0 = 1"), "No award-report scope for restricted role " + role);
            else
            {
                Check(ReportRules.Categories(actor).Contains("awards") && sql.Contains("ContractReference") && sql.Contains("CompanyName") && sql.Contains("DepartmentId"), "Parameterized actual-field filters for " + role);
                Check(sql.Contains("AwardedAt") && sql.Contains("2026-11-14T21:00:00") && sql.Contains("2026-11-15T21:00:00"), "Inclusive Amman award-date window for " + role);
                Check(sql.Contains("SubmittedAt") && sql.Contains("Status") && !sql.Contains("PaymentTerms") && !sql.Contains("TechnicalDetails"), "Winner visibility gate without confidential proposal projection " + role);
                if (role is ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee) Check(sql.Contains("CommitteeMembers") && sql.Contains("EffectiveTo"), "Current assigned membership retained " + role);
            }
        }
        var admin = new BidAccount("admin", [ApplicationRoles.Admin, ApplicationRoles.SourcingOfficer], null, false, true, true, [], []);
        Check(!ReportRules.Categories(admin).Contains("awards"), "Admin confidentiality overrides combined roles");
        Check(ReportExports.Excel(document).Length > 100 && ReportExports.Pdf(document).Length > 100, "Existing genuine XLSX/PDF exports support award report");
        Console.WriteLine($"PASS: {n} database-free award report fields, unknown references, role/stage query filters and real export checks.");
    }
}
