using System.ComponentModel.DataAnnotations;
namespace SourcingTenderManagementSystem.Web.Models.Reports;

public sealed class ReportFilter
{
    public string Category { get; set; } = "";
    [StringLength(200)] public string? Search { get; set; }
    [StringLength(64)] public string? Status { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TenderId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, 100000)] public int Page { get; set; } = 1;
}
public sealed record ReportCell(string Text, decimal? Number = null)
{
    public static ReportCell Value(decimal value) => new(value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture), value);
}
public sealed record ReportTable(string Title, string[] Columns, IReadOnlyList<ReportCell[]> Rows);
public sealed record ReportMetric(string Label, string Value);
public sealed record ReportDocument(string Title, DateTimeOffset GeneratedAt, IReadOnlyList<ReportMetric> Metrics, IReadOnlyList<ReportTable> Tables, IReadOnlyList<string> Notes);
public sealed record ReportOption(Guid Id, string Label);
public sealed record ReportPage(ReportFilter Filter, string[] Categories, ReportDocument Document) { public string ScopeKey { get; init; } = ""; public IReadOnlyList<ReportOption> Departments { get; init; } = []; public IReadOnlyList<ReportOption> Tenders { get; init; } = []; }
