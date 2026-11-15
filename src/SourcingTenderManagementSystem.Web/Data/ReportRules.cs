using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Data;

public static class ReportRules
{
    public const int RowLimit = 10000;
    public static bool Admin(BidAccount a) => a.Enabled && a.EmployeeDepartment && a.SupplierId is null && !a.Roles.Contains(ApplicationRoles.Supplier) && a.Roles.Contains(ApplicationRoles.Admin);
    public static string[] Categories(BidAccount a, bool topOverview = false)
    {
        if (Admin(a)) return ["requests", "tenders", "suppliers"];
        if (a.Supplier) return ["offers"];
        if (!a.Internal) return [];
        var roles = a.Roles; var result = new List<string>();
        if (roles.Contains(ApplicationRoles.RequestingDepartment) || roles.Contains(ApplicationRoles.SourcingOfficer)) result.Add("requests");
        if (roles.Any(r => r is ApplicationRoles.RequestingDepartment or ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee)) result.Add("tenders");
        if (roles.Any(r => r is ApplicationRoles.SourcingOfficer or ApplicationRoles.SourcingManager or ApplicationRoles.EvaluationCommittee or ApplicationRoles.TopCommittee)) result.AddRange(["offers", "evaluations", "decisions", "unified", "awards"]);
        if (topOverview && roles.Contains(ApplicationRoles.TopCommittee)) result.Add("overview");
        return result.ToArray();
    }
    public static void Validate(ReportFilter f) { BidRules.Require(f.Page >= 1 && f.Page <= 100000 && (f.Search?.Length ?? 0) <= 200 && (f.Status?.Length ?? 0) <= 64, 400, "Invalid report filters."); BidRules.Require(!f.From.HasValue || !f.To.HasValue || f.From <= f.To, 400, "The start date must not follow the end date."); }
    public static decimal? ApprovalDays(DateTimeOffset? firstSubmission, DateTimeOffset? firstApproval) => firstSubmission.HasValue && firstApproval >= firstSubmission ? (decimal)(firstApproval.Value - firstSubmission.Value).TotalDays : null;
    public static decimal? Participation(int submitted, int invited) => invited > 0 ? 100m * submitted / invited : null;
    public static void Limit(int count) => BidRules.Require(count <= RowLimit, 400, "More than 10,000 records match. Refine the filters before viewing or exporting.");
    public static string Label(string category) => category switch { "requests" => "Purchase requests", "tenders" => "Tenders", "suppliers" => "Supplier verification", "offers" => "Offers & commercial analysis", "evaluations" => "Technical & financial results", "decisions" => "Approvals & decisions", "unified" => "Unified evaluation report", "overview" => "Top Committee statistics", "awards" => "Awards & contract references", _ => "Reports" };
}
