using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Reports;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.Web.Data;

public interface IReporting { Task<ReportPage> ReadAsync(ClaimsPrincipal user, ReportFilter filter, CancellationToken ct); }
// Read-only reports. Bid access delegates to the existing authoritative visibility query.
public sealed class Reporting(ApplicationDbContext db, BidData bids, IEvaluationManagement evaluations, IUnifiedReportManagement unified, TimeProvider clock) : IReporting
{
    private static ReportCell C(object? value) => new(value is Enum ? System.Text.RegularExpressions.Regex.Replace(value.ToString()!, "(?<=[a-z])(?=[A-Z])", " ") : value?.ToString() ?? "Not recorded");
    private static ReportCell N(decimal value) => ReportCell.Value(value);
    private static string Date(DateTimeOffset? value) => value.HasValue ? TenderTime.Display(value.Value) : "Not recorded";
    private static ReportTable Table(string name, string[] headers, IEnumerable<ReportCell[]> rows) => new(name, headers, rows.ToArray());
    private static T? Status<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        BidRules.Require(Enum.TryParse<T>(value, true, out var status) && Enum.IsDefined(status), 400, "Select a valid status for this report."); return status;
    }
    private static DateTimeOffset? Start(DateOnly? date) { try { return date.HasValue ? TenderTime.Utc(date.Value.ToDateTime(TimeOnly.MinValue)) : null; } catch (ArgumentException) { throw new BidException(400, "Choose valid Asia/Amman date boundaries."); } }
    private static DateTimeOffset? End(DateOnly? date) => date.HasValue ? (date.Value == DateOnly.MaxValue ? DateTimeOffset.MaxValue : Start(date.Value.AddDays(1))) : null;
    public IQueryable<PurchaseRequisition> RequestQuery(BidAccount actor) => db.PurchaseRequisitions.AsNoTracking().Where(q => ReportRules.Admin(actor) || actor.Roles.Contains(ApplicationRoles.SourcingOfficer) || q.CreatedByUserId == actor.UserId);
    public IQueryable<Tender> TenderQuery(BidAccount actor)
    {
        var broad = ReportRules.Admin(actor) || actor.Roles.Contains(ApplicationRoles.SourcingOfficer) || actor.Roles.Contains(ApplicationRoles.SourcingManager);
        return db.Tenders.AsNoTracking().Where(t => broad || actor.Roles.Contains(ApplicationRoles.RequestingDepartment) && db.TenderPurchaseRequisitions.Any(l => l.TenderId == t.Id && db.PurchaseRequisitions.Any(q => q.Id == l.PurchaseRequisitionId && q.CreatedByUserId == actor.UserId)) ||
            actor.Roles.Contains(ApplicationRoles.EvaluationCommittee) && actor.EvaluationTenders.Contains(t.Id) && BidRules.ClosedStages.Contains(t.Status) || actor.Roles.Contains(ApplicationRoles.TopCommittee) && actor.TopTenders.Contains(t.Id) && (t.Status == TenderStatus.AwaitingCommitteeDecision || t.Status == TenderStatus.Awarded));
    }
    private IQueryable<Tender> FilterTenders(BidAccount a, ReportFilter f)
    {
        var q = TenderQuery(a); if (f.TenderId.HasValue) q = q.Where(t => t.Id == f.TenderId); if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(t => t.Title.Contains(f.Search));
        if (f.DepartmentId.HasValue) q = q.Where(t => db.TenderPurchaseRequisitions.Any(l => l.TenderId == t.Id && db.PurchaseRequisitions.Any(r => r.Id == l.PurchaseRequisitionId && r.DepartmentId == f.DepartmentId)));
        var start = Start(f.From); var end = End(f.To); if (start.HasValue) q = q.Where(t => t.SubmissionDeadline >= start); if (end.HasValue) q = q.Where(t => t.SubmissionDeadline < end); return q;
    }
    private async Task<BidAccount> Account(ClaimsPrincipal user, CancellationToken ct) => await bids.AccountAsync(user, clock.GetUtcNow(), ct) ?? throw new BidException(403, "An authorized account is required.");
    public async Task<ReportPage> ReadAsync(ClaimsPrincipal user, ReportFilter f, CancellationToken ct)
    {
        ReportRules.Validate(f); var a = await Account(user, ct);
        var top = a.Internal && a.Roles.Contains(ApplicationRoles.TopCommittee) && await db.Tenders.AnyAsync(t => a.TopTenders.Contains(t.Id) && (t.Status == TenderStatus.AwaitingCommitteeDecision || t.Status == TenderStatus.Awarded), ct);
        var categories = ReportRules.Categories(a, top); BidRules.Require(categories.Length > 0, 403, "No reports are available for this account.");
        if (a.Supplier) BidRules.Require(!f.DepartmentId.HasValue, 400, "Department filters are not available on supplier reports.");
        if (string.IsNullOrWhiteSpace(f.Category)) f.Category = categories[0]; BidRules.Require(categories.Contains(f.Category), 403, "This report is not available to your account.");
        var scope = new List<string>(); var metrics = new List<ReportMetric>(); var tables = new List<ReportTable>(); var notes = new List<string> { "All records and exports use your current permissions. Dates display in Asia/Amman. Metrics cover the matching records, not just the current table page." };
        if (f.Category == "requests")
        {
            BidRules.Require(!f.TenderId.HasValue, 400, "Use the tender report to filter by tender.");
            var q = RequestQuery(a); if (f.DepartmentId.HasValue) q = q.Where(x => x.DepartmentId == f.DepartmentId); if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(x => x.Title.Contains(f.Search));
            var status = Status<PurchaseRequestStatus>(f.Status); if (status.HasValue) q = q.Where(x => x.Status == status);
            var start = Start(f.From); var end = End(f.To); if (start.HasValue) q = q.Where(x => x.CreatedAt >= start); if (end.HasValue) q = q.Where(x => x.CreatedAt < end);
            var rows = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(rows.Count); scope.AddRange(rows.Select(x => x.Id.ToString()));
            var ids = rows.Select(x => x.Id).ToArray(); var keys = ids.Select(x => x.ToString()).ToArray();
            var submitted = await db.AuditLogs.AsNoTracking().Where(x => x.RecordType == "PurchaseRequisition" && keys.Contains(x.RecordId) && x.Action == "Requisition.Submitted").GroupBy(x => x.RecordId).Select(g => new { Id = g.Key, At = g.Min(x => x.OccurredAt) }).ToListAsync(ct);
            var approved = await db.PurchaseRequisitionReviews.AsNoTracking().Where(x => ids.Contains(x.PurchaseRequisitionId) && x.Outcome == PurchaseRequestStatus.Approved).GroupBy(x => x.PurchaseRequisitionId).Select(g => new { Id = g.Key, At = g.Min(x => x.ReviewedAt) }).ToListAsync(ct);
            var depts = await db.Departments.AsNoTracking().Where(d => rows.Select(x => x.DepartmentId).Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
            decimal? Days(Guid id) => ReportRules.ApprovalDays(submitted.FirstOrDefault(x => x.Id == id.ToString())?.At, approved.FirstOrDefault(x => x.Id == id)?.At);
            var durations = rows.Select(x => Days(x.Id)).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
            metrics.Add(new("Matching requests", rows.Count.ToString())); metrics.Add(new("Average approval days", durations.Length > 0 ? durations.Average().ToString("0.##", CultureInfo.InvariantCulture) : "Not available")); metrics.Add(new("Requests with complete approval timing", durations.Length.ToString()));
            tables.Add(Table("Purchase requests", ["Request", "Department", "Status", "Priority", "Created (Amman)", "First submission (Amman)", "First approval (Amman)", "Approval days"], rows.Select(x => new[] { C(x.Title), C(depts.GetValueOrDefault(x.DepartmentId)), C(x.Status), C(x.Priority), C(Date(x.CreatedAt)), C(Date(submitted.FirstOrDefault(s => s.Id == x.Id.ToString())?.At)), C(Date(approved.FirstOrDefault(s => s.Id == x.Id)?.At)), Days(x.Id) is decimal d ? N(d) : C(null) })));
            tables.Add(Table("Status breakdown", ["Status", "Requests"], rows.GroupBy(x => x.Status).Select(g => new[] { C(g.Key), N(g.Count()) })));
            tables.Add(Table("Department breakdown", ["Department", "Requests"], rows.GroupBy(x => x.DepartmentId).Select(g => new[] { C(depts.GetValueOrDefault(g.Key)), N(g.Count()) })));
            notes.Add("Date filters apply to request creation. Approval duration uses the first recorded submission and first approval; incomplete or inconsistent timestamps are excluded from the average. Resubmissions do not reset the clock.");
        }
        else if (f.Category == "tenders")
        {
            var q = FilterTenders(a, f); var status = Status<TenderStatus>(f.Status); if (status.HasValue) q = q.Where(t => t.Status == status);
            var rows = await q.OrderByDescending(t => t.SubmissionDeadline).ThenBy(t => t.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(rows.Count); scope.AddRange(rows.Select(x => x.Id.ToString())); var ids = rows.Select(t => t.Id).ToArray();
            var invited = await (from i in db.Invitations.AsNoTracking() join l in db.TenderSuppliers on i.TenderSupplierId equals l.Id where ids.Contains(l.TenderId) group i by l.TenderId into g select new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
            var visible = bids.VisibleQuery(a, clock.GetUtcNow()).Where(b => ids.Contains(b.TenderId) && b.SubmittedAt != null);
            var offers = await visible.GroupBy(b => b.TenderId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
            var started = await db.AuditLogs.AsNoTracking().Where(x => x.RecordType == "Tender" && x.Action == "Tender.EvaluationStarted" && ids.Select(id => id.ToString()).Contains(x.RecordId)).GroupBy(x => x.RecordId).Select(g => new { Id = g.Key, At = g.Min(x => x.OccurredAt) }).ToListAsync(ct);
            bool Confidential(Tender t) => !ReportRules.Admin(a) && BidRules.ClosedStages.Contains(t.Status) && (a.Roles.Contains(ApplicationRoles.SourcingOfficer) || a.Roles.Contains(ApplicationRoles.SourcingManager) || a.Roles.Contains(ApplicationRoles.EvaluationCommittee) && a.EvaluationTenders.Contains(t.Id) || a.Roles.Contains(ApplicationRoles.TopCommittee) && a.TopTenders.Contains(t.Id) && (t.Status is TenderStatus.AwaitingCommitteeDecision or TenderStatus.Awarded));
            metrics.Add(new("Matching tenders", rows.Count.ToString())); metrics.Add(new("Open", rows.Count(t => t.Status == TenderStatus.Open).ToString())); metrics.Add(new("Closed", rows.Count(t => t.Status == TenderStatus.Closed).ToString())); metrics.Add(new("Awarded", rows.Count(t => t.Status == TenderStatus.Awarded).ToString()));
            tables.Add(Table("Tender activity", ["Tender", "Status", "Deadline (Amman)", "Invited suppliers", "Submitted offers", "Participation %", "Zero submitted offers", "Current evaluation elapsed days"], rows.Select(t =>
            {
                var allowed = Confidential(t); var count = offers.FirstOrDefault(b => b.Id == t.Id)?.Count ?? 0; var invitations = invited.FirstOrDefault(i => i.Id == t.Id)?.Count ?? 0; var rate = ReportRules.Participation(count, invitations); var began = started.FirstOrDefault(s => s.Id == t.Id.ToString())?.At;
                return new[] { C(t.Title), C(t.Status), C(Date(t.SubmissionDeadline)), allowed ? N(invitations) : C("Restricted"), allowed ? N(count) : C("Restricted"), allowed && rate.HasValue ? N(rate.Value) : C(allowed ? "Not available" : "Restricted"), allowed ? C(count == 0 ? "Yes" : "No") : C("Restricted"), allowed && t.Status == TenderStatus.UnderEvaluation && began.HasValue ? N((decimal)(clock.GetUtcNow() - began.Value).TotalDays) : C("Not available") };
            })));
            tables.Add(Table("Status breakdown", ["Status", "Tenders"], rows.GroupBy(t => t.Status).Select(g => new[] { C(g.Key), N(g.Count()) })));
            notes.Add("Date filters apply to submission deadlines. Participation = submitted suppliers / invited suppliers Ã— 100; drafts are excluded. Counts are confidential until authorized after closure. Evaluation elapsed days applies only to current Under Evaluation tenders, from the first start event through now (including rework); no delay threshold is inferred.");
        }
        else if (f.Category == "suppliers")
        {
            BidRules.Require(ReportRules.Admin(a), 403, "Supplier verification reports require Admin access."); BidRules.Require(!f.DepartmentId.HasValue && !f.TenderId.HasValue && !f.From.HasValue && !f.To.HasValue, 400, "Supplier applications do not have a registration date. Use company search or status instead.");
            var q = db.SupplierProfiles.AsNoTracking(); if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(s => s.CompanyName.Contains(f.Search)); var status = Status<SupplierStatus>(f.Status); if (status.HasValue) q = q.Where(s => s.Status == status);
            var rows = await q.OrderBy(s => s.CompanyName).ThenBy(s => s.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(rows.Count); scope.AddRange(rows.Select(x => x.Id.ToString())); var ids = rows.Select(s => s.Id).ToArray(); var docs = await db.SupplierDocuments.AsNoTracking().Where(d => ids.Contains(d.SupplierProfileId)).Select(d => new { d.SupplierProfileId, d.ExpiresOn }).ToListAsync(ct); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "Asia/Amman").DateTime);
            metrics.Add(new("Matching suppliers", rows.Count.ToString())); metrics.Add(new("Approved", rows.Count(s => s.Status == SupplierStatus.Approved).ToString())); metrics.Add(new("Suspended", rows.Count(s => s.Status == SupplierStatus.Suspended).ToString()));
            tables.Add(Table("Supplier verification and document expiry", ["Company", "Application status", "Reviewed (Amman)", "Registration documents", "Expired before today", "Expiry not recorded"], rows.Select(s => new[] { C(s.CompanyName), C(s.Status), C(Date(s.ReviewedAt)), N(docs.Count(d => d.SupplierProfileId == s.Id)), N(docs.Count(d => d.SupplierProfileId == s.Id && d.ExpiresOn.HasValue && d.ExpiresOn < today)), N(docs.Count(d => d.SupplierProfileId == s.Id && !d.ExpiresOn.HasValue)) })));
            tables.Add(Table("Status breakdown", ["Status", "Suppliers"], rows.GroupBy(s => s.Status).Select(g => new[] { C(g.Key), N(g.Count()) })));
            notes.Add("Missing expiry dates mean unknown, not valid indefinitely. Supplier performance is not scored because fulfillment/performance rules and data are not defined. Application approval is the existing verification state; no additional legal verification decision is inferred.");
        }
        else if (f.Category is "offers" or "evaluations" or "decisions")
        {
            var q = bids.VisibleQuery(a, clock.GetUtcNow()); if (f.TenderId.HasValue) q = q.Where(b => b.TenderId == f.TenderId);
            if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(b => db.Tenders.Any(t => t.Id == b.TenderId && t.Title.Contains(f.Search)));
            if (f.DepartmentId.HasValue) q = q.Where(b => db.TenderPurchaseRequisitions.Any(l => l.TenderId == b.TenderId && db.PurchaseRequisitions.Any(r => r.Id == l.PurchaseRequisitionId && r.DepartmentId == f.DepartmentId)));
            var status = Status<BidStatus>(f.Status); if (status.HasValue) q = q.Where(b => b.Status == status);
            var start = Start(f.From); var end = End(f.To); if (start.HasValue) q = q.Where(b => b.SubmittedAt >= start); if (end.HasValue) q = q.Where(b => b.SubmittedAt < end);
            var rows = await q.OrderByDescending(b => b.SubmittedAt).ThenBy(b => b.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(rows.Count); scope.AddRange(rows.Select(x => x.Id.ToString())); var ids = rows.Select(b => b.Id).ToArray();
            if (f.Category == "offers")
            {
                var items = await db.BidItems.AsNoTracking().Where(i => ids.Contains(i.BidId)).OrderBy(i => i.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(items.Count);
                var charges = await db.BidCostComponents.AsNoTracking().Where(c => ids.Contains(c.BidId)).OrderBy(c => c.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(charges.Count);
                var tenderIds = rows.Select(b => b.TenderId).Distinct().ToArray();
                var titles = await db.Tenders.AsNoTracking().Where(t => tenderIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
                var companies = await (from b in db.Bids.AsNoTracking() join l in db.TenderSuppliers on b.TenderSupplierId equals l.Id join company in db.SupplierProfiles on l.SupplierProfileId equals company.Id where ids.Contains(b.Id) select new { b.Id, company.CompanyName }).ToDictionaryAsync(x => x.Id, x => x.CompanyName, ct);
                var rowLookup = rows.ToDictionary(b => b.Id); var itemLookup = items.ToLookup(i => i.BidId); var chargeLookup = charges.ToLookup(c => c.BidId);
                var data = new List<ReportCell[]>();
                foreach (var bid in rows)
                {
                    BidRules.Require(bid.Currency == "JOD", 409, "A stored offer currency conflicts with the approved JOD-only policy."); var snapshot = new BidSnapshot(bid, itemLookup[bid.Id].ToArray(), chargeLookup[bid.Id].ToArray(), []);
                    data.Add([C(titles[bid.TenderId]), C(companies.GetValueOrDefault(bid.Id)), C(bid.Status), C(Date(bid.SubmittedAt)), N(BidRules.Total(snapshot)), C(bid.Currency), C(bid.DeliveryTimeframe), C(bid.PaymentTerms), C(bid.OfferValidUntil)]);
                }
                metrics.Add(new("Matching offers", rows.Count.ToString())); tables.Add(Table("Commercial offers", ["Tender", "Supplier", "Status", "Submitted (Amman)", "Total", "Currency", "Delivery", "Payment terms", "Offer valid until"], data));
                tables.Add(Table("Itemized pricing", ["Tender", "Supplier", "Item", "Quantity", "Unit price JOD", "Item total JOD", "Specifications"], items.Select(i => { var bid = rowLookup[i.BidId]; return new[] { C(titles[bid.TenderId]), C(companies.GetValueOrDefault(bid.Id)), C(i.Description), N(i.OfferedQuantity), N(i.UnitPrice), N(checked(i.OfferedQuantity * i.UnitPrice)), C(i.Specifications) }; })));
                tables.Add(Table("Taxes, fees and additional costs", ["Tender", "Supplier", "Cost type", "Amount", "Currency", "Notes"], charges.Select(c => { var bid = rowLookup[c.BidId]; return new[] { C(titles[bid.TenderId]), C(companies.GetValueOrDefault(bid.Id)), C(c.CostType), N(c.Amount), C(c.Currency), C(c.Notes) }; })));
                notes.Add("Totals reuse the bid service's decimal calculation. Only your own offers are visible to suppliers; internal readers retain closure and tender-assignment restrictions. Date filters apply to submission date; drafts are excluded when a date filter is used.");
            }
            else if (f.Category == "evaluations")
            {
                var data = new List<ReportCell[]>(); foreach (var bid in rows) { var page = await evaluations.PageAsync(user, bid.Id, ct); if (page.Results.Count == 0 || bid.Status == BidStatus.NonCompliant) data.Add([C(page.Tender.Title), C(page.SupplierName), C(bid.Status), C(bid.Status == BidStatus.NonCompliant ? "Excluded: Non-Compliant" : "Awaiting complete confirmation"), C(null), C(null), C(null), N(page.ConfirmedMembers)]); foreach (var result in page.Results.Where(_ => bid.Status != BidStatus.NonCompliant)) data.Add([C(page.Tender.Title), C(page.SupplierName), C(bid.Status), C(result.Criterion.Name), C(result.Criterion.Kind), N(result.Average), N(result.WeightedPoints), N(page.ConfirmedMembers)]); }
                ReportRules.Limit(data.Count); tables.Add(Table("Confirmed evaluation results", ["Tender", "Supplier", "Compliance / bid status", "Criterion", "Kind", "Average / 100", "Weighted points", "Confirmed members"], data)); notes.Add("Results come from the existing evaluation service. Partial drafts and replaced-member scores are not treated as final results. Non-Compliant rows identify excluded offers; use the existing protected compliance page for their detailed reasons.");
            }
            else
            {
                var tenderIds = rows.Select(b => b.TenderId).Distinct().ToArray(); var decisions = await db.TenderApprovals.AsNoTracking().Where(d => tenderIds.Contains(d.TenderId)).OrderByDescending(d => d.DecidedAt).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(decisions.Count);
                tables.Add(Table("Documented decisions", ["Tender ID", "Report version", "Stage", "Decision", "Decided (Amman)", "Comments", "Justification"], decisions.Select(d => new[] { C(d.TenderId), C(d.EvaluationReportId), C(d.Stage), C(d.Decision), C(Date(d.DecidedAt)), C(d.Comments), C(d.Justification) })));
                var votes = await db.CommitteeVotes.AsNoTracking().Where(v => tenderIds.Contains(v.TenderId) && db.TenderApprovals.Any(r => r.Id == v.TenderApprovalId && r.TenderId == v.TenderId && r.Stage == ApprovalStage.TopCommittee && r.RoundClosedAt != null && db.CommitteeVotes.Count(other => other.TenderApprovalId == r.Id && other.TenderId == r.TenderId) == 4)).OrderBy(v => v.TenderId).ThenBy(v => v.Id).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(votes.Count); tables.Add(Table("Existing committee voting records", ["Tender ID", "Approval ID", "Membership ID", "Decision", "Justification", "Voted (Amman)"], votes.Select(v => new[] { C(v.TenderId), C(v.TenderApprovalId), C(v.CommitteeMemberId), C(v.Decision), C(v.Justification), C(Date(v.VotedAt)) })));
                var awards = await db.TenderAwards.AsNoTracking().Where(w => tenderIds.Contains(w.TenderId)).Take(ReportRules.RowLimit + 1).ToListAsync(ct); ReportRules.Limit(awards.Count); tables.Add(Table("Recorded awards / contract references", ["Tender ID", "Winning bid ID", "Approval ID", "Awarded (Amman)", "Contract reference"], awards.Select(w => new[] { C(w.TenderId), C(w.BidId), C(w.TenderApprovalId), C(Date(w.AwardedAt)), C(w.ContractReference) })));
                notes.Add("No votes, awards or contracts are created by reporting. Empty sections reflect the stored data. Partial committee votes remain sealed. Date/status filters select source bids by submission date/status; decisions and award rows are then limited to those authorized tenders. This is not a global security-audit report.");
            }
            var fresh = await Account(user, ct); BidRules.Require(await bids.VisibleQuery(fresh, clock.GetUtcNow()).CountAsync(b => ids.Contains(b.Id), ct) == ids.Length, 403, "Offer access changed while preparing the report.");
        }
        else if (f.Category == "unified")
        {
            BidRules.Require(string.IsNullOrWhiteSpace(f.Search) && string.IsNullOrWhiteSpace(f.Status) && !f.DepartmentId.HasValue && !f.From.HasValue && !f.To.HasValue, 400, "The unified report uses its tender ID only.");
            if (!f.TenderId.HasValue) notes.Add("Select an available tender and apply the filter to view or export its unified report.");
            else
            {
                var page = await unified.PageAsync(user, f.TenderId.Value, ct); var report = page.Report; scope.Add(page.Tender.Id.ToString());
                tables.Add(Table("Report narrative", ["Section", "Content"], new[] { new[] { C("Tender"), C(page.Tender.Title) }, new[] { C("State"), C(report?.SubmittedAt.HasValue == true ? "Submitted" : "Draft / live authorized snapshot") }, new[] { C("Version"), C(report?.Id) }, new[] { C("Submitted (Amman)"), C(Date(report?.SubmittedAt)) }, new[] { C("Technical summary"), C(report?.TechnicalSummary) }, new[] { C("Financial summary"), C(report?.FinancialSummary) }, new[] { C("Observations"), C(report?.Observations) }, new[] { C("Non-compliance"), C(report?.NonCompliance) }, new[] { C("Final result"), C(report?.FinalResult) }, new[] { C("Recommendation"), C(report?.Recommendation) } }));
                if (page.Snapshot is { } s)
                {
                    tables.Add(Table("Approved criteria and weights", ["Criterion", "Kind", "Weight %"], s.Criteria.Select(c => new[] { C(c.Name), C(c.Kind), N(c.Weight) })));
                    tables.Add(Table("Itemized commercial offers", ["Supplier", "Item", "Quantity", "Unit price JOD", "Item total JOD", "Specifications"], s.Offers.SelectMany(o => o.Items.Select(i => new[] { C(o.Supplier), C(i.Description), N(i.Quantity), N(i.UnitPrice), N(i.Total), C(i.Specifications) }))));
                    tables.Add(Table("Additional costs", ["Supplier", "Type", "Amount JOD"], s.Offers.SelectMany(o => o.Charges.Select(c => new[] { C(o.Supplier), C(c.Type), N(c.Amount) }))));
                    tables.Add(Table("Supplier comparison", ["Supplier", "Bid status", "JOD total", "Confirmed members", "Payment terms", "Delivery", "Validity", "Compliance reasons"], s.Offers.Select(o => new[] { C(o.Supplier), C(o.Status), N(o.Total), N(o.ConfirmedMembers), C(o.PaymentTerms), C(o.Delivery), C(o.ValidUntil), C(string.Join("; ", o.ComplianceReasons)) })));
                    tables.Add(Table("Technical and financial scores", ["Supplier", "Criterion", "Average / 100", "Weighted points"], s.Offers.SelectMany(o => o.Results.Select(v => new[] { C(o.Supplier), C(s.Criteria.Single(c => c.Id == v.CriterionId).Name), N(v.Average), N(v.WeightedPoints) }))));
                    tables.Add(Table("Confirmed observations", ["Supplier", "Member", "Criterion", "Score", "Observation", "Non-compliance", "Confirmed (Amman)"], s.Offers.SelectMany(o => o.Notes.Select(n => new[] { C(o.Supplier), C(n.MemberName), C(s.Criteria.Single(c => c.Id == n.CriterionId).Name), N(n.Score), C(n.Notes), C(n.NonCompliance), C(Date(n.ConfirmedAt)) }))));
                }
                tables.Add(Table("Report transaction audit", ["Action", "At (Amman)", "Previous state", "New state", "Reason"], page.History.Select(h => new[] { C(h.Action), C(Date(h.OccurredAt)), C(h.PreviousStatus), C(h.NewStatus), C(h.Reason) })));
                notes.Add("The existing unified-report service authorizes and supplies this document. Submitted snapshots retain their saved contents; no global audit permissions are added.");
            }
        }
        else if (f.Category == "overview")
        {
            BidRules.Require(top, 403, "An effective final-stage Top Committee assignment is required."); BidRules.Require(string.IsNullOrWhiteSpace(f.Status) && !f.DepartmentId.HasValue && !f.TenderId.HasValue && !f.From.HasValue && !f.To.HasValue, 400, "Comprehensive statistics use all site data; use company search to filter the transaction-frequency table.");
            metrics.Add(new("Registered suppliers", (await db.SupplierProfiles.CountAsync(ct)).ToString())); metrics.Add(new("Approved suppliers", (await db.SupplierProfiles.CountAsync(s => s.Status == SupplierStatus.Approved, ct)).ToString())); metrics.Add(new("Departments", (await db.Departments.CountAsync(ct)).ToString())); metrics.Add(new("Purchase requests", (await db.PurchaseRequisitions.CountAsync(ct)).ToString())); metrics.Add(new("Tenders", (await db.Tenders.CountAsync(ct)).ToString()));
            var q = db.SupplierProfiles.AsNoTracking(); if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(s => s.CompanyName.Contains(f.Search)); var suppliers = await q.OrderBy(s => s.CompanyName).ThenBy(s => s.Id).Take(ReportRules.RowLimit + 1).Select(s => new { s.Id, s.CompanyName }).ToListAsync(ct); ReportRules.Limit(suppliers.Count); scope.AddRange(suppliers.Select(x => x.Id.ToString()));
            var awards = await (from w in db.TenderAwards.AsNoTracking() join b in db.Bids on w.BidId equals b.Id join l in db.TenderSuppliers on b.TenderSupplierId equals l.Id where suppliers.Select(s => s.Id).Contains(l.SupplierProfileId) group w by l.SupplierProfileId into g select new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
            tables.Add(Table("Recorded award frequency by supplier", ["Supplier", "Recorded awards"], suppliers.Select(s => new[] { C(s.CompanyName), N(awards.FirstOrDefault(w => w.Id == s.Id)?.Count ?? 0) })));
            notes.Add("Site counts are the comprehensive Top Committee statistics required by V5. Frequency is explicitly recorded awards, not an invented fulfillment/performance score or a count of bids. Award execution is a separate module; no award records are fabricated.");
        }
        foreach (var table in tables) ReportRules.Limit(table.Rows.Count);
        var requestScope = RequestQuery(a); var tenderScope = TenderQuery(a); var bidScope = bids.VisibleQuery(a, clock.GetUtcNow());
        var departments = new List<ReportOption>();
        if (categories.Contains("requests")) departments = await db.Departments.AsNoTracking().Where(d => requestScope.Any(q => q.DepartmentId == d.Id)).OrderBy(d => d.Name).Select(d => new ReportOption(d.Id, d.Name)).ToListAsync(ct);
        else if (!a.Supplier) departments = await db.Departments.AsNoTracking().Where(d => db.PurchaseRequisitions.Any(q => q.DepartmentId == d.Id && db.TenderPurchaseRequisitions.Any(l => l.PurchaseRequisitionId == q.Id && tenderScope.Any(t => t.Id == l.TenderId)))).OrderBy(d => d.Name).Select(d => new ReportOption(d.Id, d.Name)).ToListAsync(ct);
        var tenders = new List<ReportOption>();
        if (f.Category is "offers" or "evaluations" or "decisions" or "unified") tenders = await db.Tenders.AsNoTracking().Where(t => bidScope.Any(b => b.TenderId == t.Id)).OrderBy(t => t.Title).Take(ReportRules.RowLimit + 1).Select(t => new ReportOption(t.Id, t.Title)).ToListAsync(ct);
        ReportRules.Limit(tenders.Count);
        var applied = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) applied.Add("Search: " + f.Search);
        if (!string.IsNullOrWhiteSpace(f.Status)) applied.Add("Status: " + f.Status);
        if (f.DepartmentId.HasValue) applied.Add("Department: " + (departments.FirstOrDefault(d => d.Id == f.DepartmentId)?.Label ?? "Unavailable selection"));
        if (f.TenderId.HasValue) applied.Add("Tender: " + (tenders.FirstOrDefault(t => t.Id == f.TenderId)?.Label ?? "Selected tender"));
        if (f.From.HasValue) applied.Add("From: " + f.From.Value.ToString("yyyy-MM-dd"));
        if (f.To.HasValue) applied.Add("To (inclusive): " + f.To.Value.ToString("yyyy-MM-dd"));
        notes.Add(applied.Count > 0 ? "Applied filters — " + string.Join("; ", applied) : "Applied filters — all records in your authorized scope.");
        var final = await Account(user, ct); BidRules.Require(Identity(a) == Identity(final), 403, "Account permissions changed while generating the report.");
        return new(f, categories, new(ReportRules.Label(f.Category), clock.GetUtcNow(), metrics, tables, notes)) { Departments = departments, Tenders = tenders, ScopeKey = Identity(final) + "|" + string.Join(",", scope.Order()) };
    }
    private static string Identity(BidAccount a) => string.Join("|", a.UserId, a.Enabled, a.EmployeeDepartment, a.SupplierId, a.SupplierApproved, string.Join(",", a.Roles.Order()), string.Join(",", a.EvaluationTenders.Order()), string.Join(",", a.TopTenders.Order()));
}
