using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IUnifiedReportManagement
{
    Task<UnifiedReportPage> PageAsync(ClaimsPrincipal user, Guid tenderId, CancellationToken ct = default, Guid? reportId = null);
    Task DecideAsync(ClaimsPrincipal user, Guid tenderId, ReportDecisionForm form, CancellationToken ct = default);
    Task SaveAsync(ClaimsPrincipal user, Guid tenderId, UnifiedReportForm form, bool submit, CancellationToken ct = default);
}
public sealed class UnifiedReportManagement(ApplicationDbContext db, IBidData bids, EvaluationManagement evaluations, TimeProvider clock) : IUnifiedReportManagement
{
    private async Task<(BidAccount Actor, Tender Tender)> Access(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var actor = await bids.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(actor is not null && actor.Internal, 403, "Authorized internal report access is required.");
        var visible = (await bids.ListAsync(actor, clock.GetUtcNow(), ct)).FirstOrDefault(x => x.TenderId == id);
        BidRules.Require(visible is not null, 404, "Report not found for an authorized tender.");
        var context = await bids.ContextAsync(id, ct) ?? throw new BidException(404, "Tender not found.");
        return (actor, context.Tender);
    }
    private async Task<bool> Author(BidAccount actor, Guid id, CancellationToken ct)
    {
        if (!actor.Roles.Contains(ApplicationRoles.EvaluationCommittee)) return false;
        var now = clock.GetUtcNow();
        return await (from m in db.CommitteeMembers join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId }
            equals new { CommitteeAssignmentId = a.Id, a.TenderId }
            where m.TenderId == id && a.Kind == CommitteeKind.Evaluation && m.UserId == actor.UserId && m.Position == CommitteeRules.Positions[3] &&
                (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo)
            select m).AnyAsync(ct);
    }
    private async Task<EvaluationReport?> Report(Guid id, CancellationToken ct)
    {
        var reports = await db.EvaluationReports.AsNoTracking().Where(x => x.TenderId == id).ToListAsync(ct);
        var decisions = await db.TenderApprovals.AsNoTracking().Where(x => x.TenderId == id && x.Stage == ApprovalStage.Manager).ToListAsync(ct);
        return ReportReviewRules.Current(reports, decisions, await db.TenderApprovals.AsNoTracking().Where(x => x.TenderId == id && x.Stage == ApprovalStage.TopCommittee).ToListAsync(ct));
    }
    private static bool Ready(ReportSnapshot snapshot) => snapshot.Members.Count == 4 && snapshot.Criteria.Count > 0 &&
        snapshot.Offers.Any(x => x.Status == BidStatus.Compliant) && snapshot.Offers.All(x => x.Status == BidStatus.NonCompliant ||
            x.Status == BidStatus.Compliant && x.ConfirmedMembers == 4 && x.Results.Count == snapshot.Criteria.Count);
    private static string Version(Tender tender, EvaluationReport? report, ReportSnapshot? snapshot) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { tender.Status, report, snapshot })));

    public async Task<UnifiedReportPage> PageAsync(ClaimsPrincipal user, Guid tenderId, CancellationToken ct = default, Guid? reportId = null)
    {
        var access = await Access(user, tenderId, ct);
        var current = await Report(tenderId, ct);
        var report = reportId.HasValue ? await db.EvaluationReports.AsNoTracking().SingleOrDefaultAsync(x => x.TenderId == tenderId && x.Id == reportId, ct) : current;
        BidRules.Require(!reportId.HasValue || report is not null, 404, "Report version not found.");
        var archived = report?.Id != current?.Id;
        var revision = await IsRevision(tenderId, ct);
        ReportSnapshot? snapshot;
        if (report?.SubmittedAt.HasValue == true || revision && report is not null) {
            try {
                var stored = JsonSerializer.Deserialize<ReportSnapshot>(report.Comparison);
                snapshot = stored is { FormatVersion: 1, Members: not null, Criteria: not null, Offers: not null } && stored.TenderId == tenderId ? stored : null;
            }
            catch (JsonException) { snapshot = null; } // Preserve display of legacy reports stored as ordinary text.
        } else snapshot = await Capture(user, access.Actor, access.Tender, ct);
        var form = new UnifiedReportForm { Version = Version(access.Tender, report, snapshot), RecommendedBidId = report?.RecommendedBidId,
            Observations = report?.Observations ?? "", NonCompliance = report?.NonCompliance ?? "", FinalResult = report?.FinalResult ?? "", Recommendation = report?.Recommendation ?? "" };
        var history = await db.AuditLogs.AsNoTracking().Where(x => x.RecordType == "EvaluationReport" && x.RecordId == tenderId.ToString())
            .OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
        var canEdit = !archived && access.Tender.Status == TenderStatus.UnderEvaluation && report?.SubmittedAt is null && await Author(access.Actor, tenderId, ct);
        await Access(user, tenderId, ct);
        var manager = await Manager(tenderId, ct);
        var canReview = !archived && report?.SubmittedAt.HasValue == true && snapshot is not null &&
            access.Tender.Status == TenderStatus.AwaitingManagerReview && manager is not null && ReportReviewRules.Manager(access.Actor, manager, tenderId, clock.GetUtcNow());
        return new(access.Tender, report, snapshot, form, canEdit, snapshot is not null && Ready(snapshot), history) {
            CanReview = canReview, Archived = archived,
            DecisionForm = new() { Version = canReview ? ReportReviewRules.Version(access.Tender, report!, manager!) : "" },
            Versions = await db.EvaluationReports.AsNoTracking().Where(x => x.TenderId == tenderId && x.SubmittedAt != null).OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).ToListAsync(ct),
            Decisions = await db.TenderApprovals.AsNoTracking().Where(x => x.TenderId == tenderId && x.Stage == ApprovalStage.Manager).OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToListAsync(ct)
        };
    }
    private async Task<ReportSnapshot> Capture(ClaimsPrincipal user, BidAccount actor, Tender tender, CancellationToken ct)
    {
        var context = await bids.ContextAsync(tender.Id, ct) ?? throw new BidException(404, "Tender not found.");
        BidRules.Require(TenderManagement.Approved(context.Criteria), 409, "Approved evaluation criteria are required.");
        var now = clock.GetUtcNow();
        var members = await (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId }
            equals new { CommitteeAssignmentId = a.Id, a.TenderId } join u in db.Users on m.UserId equals u.Id
            where m.TenderId == tender.Id && a.Kind == CommitteeKind.Evaluation && (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo)
            orderby m.Position, m.Id select new ReportMember(m.Id, m.UserId, u.UserName ?? u.Id, m.Position)).ToListAsync(ct);
        var offers = new List<ReportOffer>();
        foreach (var bid in (await bids.ListAsync(actor, now, ct)).Where(x => x.TenderId == tender.Id).OrderBy(x => x.Id))
        {
            var raw = await bids.ReadAsync(bid.Id, actor, now, ct) ?? throw new BidException(404, "Offer no longer available.");
            var page = await evaluations.PageAsync(user, bid.Id, ct);
            var notes = new List<ReportNote>();
            if (page.Results.Count > 0) {
                var ids = members.Select(x => x.Id).ToArray();
                var technical = await db.TechnicalEvaluations.AsNoTracking().Where(x => x.BidId == bid.Id && x.CommitteeMemberId != null && ids.Contains(x.CommitteeMemberId.Value) && x.SubmittedAt != null).ToListAsync(ct);
                var financial = await db.FinancialEvaluations.AsNoTracking().Where(x => x.BidId == bid.Id && x.CommitteeMemberId != null && ids.Contains(x.CommitteeMemberId.Value) && x.SubmittedAt != null).ToListAsync(ct);
                notes.AddRange(technical.Select(x => new ReportNote(x.CommitteeMemberId!.Value, members.Single(m => m.Id == x.CommitteeMemberId).Name, x.EvaluationCriterionId, x.Score, x.Notes, x.NonCompliance, x.SubmittedAt!.Value)));
                notes.AddRange(financial.Select(x => new ReportNote(x.CommitteeMemberId!.Value, members.Single(m => m.Id == x.CommitteeMemberId).Name, x.EvaluationCriterionId, x.Score, x.Notes, null, x.SubmittedAt!.Value)));
            }
            var reasons = await db.ComplianceReviews.AsNoTracking().Where(x => x.BidId == bid.Id && x.Reasons != null)
                .OrderBy(x => x.ReviewedAt).ThenBy(x => x.Id).Select(x => x.Outcome + ": " + x.Reasons).ToListAsync(ct);
            offers.Add(new(bid.Id, page.SupplierName, bid.Status, page.ConfirmedMembers, BidRules.Total(raw), raw.Bid.PaymentTerms,
                raw.Bid.DeliveryTimeframe, raw.Bid.OfferValidUntil, raw.Items.OrderBy(x => x.PurchaseRequisitionItemId)
                    .Select(x => new ReportLine(x.Description, x.Specifications, x.OfferedQuantity, x.UnitPrice, x.OfferedQuantity * x.UnitPrice)).ToArray(),
                raw.Charges.OrderBy(x => x.Id).Select(x => new ReportCharge(x.CostType, x.Amount)).ToArray(),
                page.Results.OrderBy(x => x.Criterion.Id).Select(x => new ReportPoints(x.Criterion.Id, x.Average, x.WeightedPoints)).ToArray(),
                notes.OrderBy(x => x.MemberId).ThenBy(x => x.CriterionId).ToArray(), reasons));
        }
        return new(1, tender.Id, tender.Title, members, context.Criteria.OrderBy(x => x.Id).Select(x => new ReportCriterion(x.Id, x.Name, x.Kind, x.Weight)).ToArray(), offers);
    }
    public async Task SaveAsync(ClaimsPrincipal user, Guid tenderId, UnifiedReportForm form, bool submit, CancellationToken ct = default)
    {
        await Access(user, tenderId, ct);
        await BidManagement.Mutate(async () => {
            await using var tx = await bids.BeginAsync(tenderId, ct);
            var access = await Access(user, tenderId, ct);
            BidRules.Require(await Author(access.Actor, tenderId, ct), 403, "Only the currently appointed Technical Representative may prepare and submit this report.");
            var report = await Report(tenderId, ct);
            BidRules.Require(access.Tender.Status == TenderStatus.UnderEvaluation && report?.SubmittedAt is null, 409, "Submitted reports are immutable. Preparation requires an Under Evaluation tender.");
            var snapshot = await IsRevision(tenderId, ct) && report is not null ? StoredSnapshot(report) : await Capture(user, access.Actor, access.Tender, ct);
            ComplianceRules.Version(form.Version, Version(access.Tender, report, snapshot));
            BidRules.Require(Ready(snapshot), 409, "All four current members must confirm every compliant offer before preparing the report.");
            foreach (var text in new[] { form.Observations, form.NonCompliance, form.FinalResult, form.Recommendation })
                BidRules.Require((text?.Length ?? 0) <= 12000, 400, "Report text must not exceed 12000 characters per field.");
            BidRules.Require(form.RecommendedBidId is null || snapshot.Offers.Any(x => x.BidId == form.RecommendedBidId && x.Status == BidStatus.Compliant && x.Results.Count == snapshot.Criteria.Count),
                400, "Recommend a compliant fully evaluated offer from this tender.");
            if (submit) {
                BidRules.Require(report is not null, 409, "Save a draft before submitting the report.");
                BidRules.Require(form.RecommendedBidId.HasValue && !string.IsNullOrWhiteSpace(form.FinalResult) && !string.IsNullOrWhiteSpace(form.Recommendation),
                    400, "Choose a recommended offer and provide the final result and a substantiated recommendation.");
                BidRules.Require(report.Comparison == JsonSerializer.Serialize(snapshot), 409,
                    "Evaluation inputs have changed. Review the full report and save a refreshed draft before submitting.");
                BidRules.Require(report.RecommendedBidId == form.RecommendedBidId && report.Observations == (form.Observations?.Trim() ?? "") &&
                    report.NonCompliance == (form.NonCompliance?.Trim() ?? "") && report.FinalResult == form.FinalResult.Trim() && report.Recommendation == form.Recommendation.Trim(),
                    409, "Save your findings and recommendation as a draft before confirming submission.");
            }
            if (report is null) { report = new EvaluationReport { Id = Guid.NewGuid(), TenderId = tenderId }; db.EvaluationReports.Add(report); }
            else db.Attach(report);
            report.PreparedByUserId = access.Actor.UserId;
            report.RecommendedBidId = form.RecommendedBidId;
            report.Observations = form.Observations?.Trim() ?? ""; report.NonCompliance = form.NonCompliance?.Trim() ?? "";
            report.FinalResult = form.FinalResult?.Trim() ?? ""; report.Recommendation = form.Recommendation?.Trim() ?? "";
            report.Comparison = JsonSerializer.Serialize(snapshot);
            report.TechnicalSummary = Summary(snapshot, EvaluationKind.Technical);
            report.FinancialSummary = Summary(snapshot, EvaluationKind.Financial);
            var now = clock.GetUtcNow();
            if (submit) {
                report.SubmittedAt = now;
                var tender = await db.Tenders.SingleAsync(x => x.Id == tenderId, ct);
                tender.Status = TenderStatus.AwaitingManagerReview;
                var manager = await Manager(tenderId, ct);
                BidRules.Require(manager is not null, 409, "A currently appointed Sourcing Manager is required.");
                var recipient = await bids.AccountAsync(Principal(manager.UserId), now, ct);
                BidRules.Require(recipient is not null && ReportReviewRules.Manager(recipient, manager, tenderId, now), 409, "The appointed manager is no longer eligible.");
                db.Notifications.Add(new Notification { Id = Guid.NewGuid(), RecipientUserId = manager.UserId, TenderId = tenderId,
                    Subject = "Unified report submitted", Message = "The committee report for " + tender.Title + " is ready for your review.", CreatedAt = now });
                db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = access.Actor.UserId, RecordType = "Tender", RecordId = tenderId.ToString(),
                    Action = "Tender.ReportSubmitted", PreviousStatus = "UnderEvaluation", NewStatus = "AwaitingManagerReview", Reason = "Report " + report.Id, OccurredAt = now });
            }
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = access.Actor.UserId, RecordType = "EvaluationReport", RecordId = tenderId.ToString(),
                Action = submit ? "ReportSubmitted" : "ReportDraftSaved", Reason = "Report " + report.Id, NewStatus = submit ? "AwaitingManagerReview" : "Draft", OccurredAt = now });
            await db.SaveChangesAsync(ct);
            var final = await Access(user, tenderId, ct);
            BidRules.Require(await Author(final.Actor, tenderId, ct), 403, "Technical representative eligibility changed.");
            await tx.CommitAsync(ct);
            return true;
        });
    }
    private Task<bool> IsRevision(Guid id, CancellationToken ct) => db.TenderApprovals.AnyAsync(x => x.TenderId == id && (x.Stage == ApprovalStage.Manager || x.Stage == ApprovalStage.TopCommittee && x.RoundClosedAt != null) &&
        (x.Decision == DecisionStatus.ManagerRejected || x.Decision == DecisionStatus.CommitteeRejected || x.Decision == DecisionStatus.ReturnedForReview), ct);
    private static ClaimsPrincipal Principal(string id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "ReportEligibility"));
    private static ReportSnapshot StoredSnapshot(EvaluationReport report)
    {
        try {
            var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(report.Comparison);
            BidRules.Require(snapshot is { FormatVersion: 1, Members: not null, Criteria: not null, Offers: not null } && snapshot.TenderId == report.TenderId,
                409, "A valid preserved report snapshot is required.");
            return snapshot;
        } catch (JsonException) { throw new BidException(409, "The preserved report snapshot is invalid."); }
    }
    private async Task<CommitteeMember?> Manager(Guid id, CancellationToken ct)
    {
        BidRules.Require(await db.CommitteeAssignments.CountAsync(x => x.TenderId == id && x.Kind == CommitteeKind.Evaluation, ct) <= 1,
            409, "Multiple evaluation committees exist. Resolve the data conflict first.");
        var now = clock.GetUtcNow();
        var managers = await (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId }
            equals new { CommitteeAssignmentId = a.Id, a.TenderId }
            where m.TenderId == id && a.Kind == CommitteeKind.Evaluation && m.Position == CommitteeRules.Positions[0] &&
                (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo) select m).ToListAsync(ct);
        BidRules.Require(managers.Count <= 1, 409, "Multiple appointed managers exist. Resolve the data conflict first.");
        return managers.SingleOrDefault();
    }
    public async Task DecideAsync(ClaimsPrincipal user, Guid tenderId, ReportDecisionForm form, CancellationToken ct = default)
    {
        await Access(user, tenderId, ct);
        await BidManagement.Mutate(async () => {
            await using var tx = await bids.BeginAsync(tenderId, ct);
            var access = await Access(user, tenderId, ct);
            var manager = await Manager(tenderId, ct);
            var now = clock.GetUtcNow();
            BidRules.Require(manager is not null && ReportReviewRules.Manager(access.Actor, manager, tenderId, now), 403, "Only this tender's currently appointed Sourcing Manager may decide the report.");
            var report = await Report(tenderId, ct) ?? throw new BidException(409, "A submitted report is required.");
            ReportReviewRules.Validate(access.Tender, report, form);
            ComplianceRules.Version(form.Version, ReportReviewRules.Version(access.Tender, report, manager));
            _ = StoredSnapshot(report);
            BidRules.Require(!await db.TenderApprovals.AnyAsync(x => x.EvaluationReportId == report.Id && x.Stage == ApprovalStage.Manager, ct), 409, "This report already has a manager decision.");
            var decision = new TenderApproval { Id = Guid.NewGuid(), TenderId = tenderId, EvaluationReportId = report.Id, Stage = ApprovalStage.Manager,
                Decision = form.Decision, ActorUserId = access.Actor.UserId, CommitteeAssignmentId = manager.CommitteeAssignmentId,
                Comments = form.Comments?.Trim() ?? "", Justification = form.Comments?.Trim() ?? "", DecidedAt = now };
            db.TenderApprovals.Add(decision);
            var tender = await db.Tenders.SingleAsync(x => x.Id == tenderId, ct);
            var approved = form.Decision == DecisionStatus.ManagerApproved;
            tender.Status = approved ? TenderStatus.AwaitingCommitteeDecision : TenderStatus.UnderEvaluation;
            if (!approved) db.EvaluationReports.Add(ReportReviewRules.Revision(report));
            var members = await (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId }
                equals new { CommitteeAssignmentId = a.Id, a.TenderId }
                where m.TenderId == tenderId && (approved ? a.Kind == CommitteeKind.TopCommittee : a.Kind == CommitteeKind.Evaluation) &&
                    (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo) select m.UserId).Distinct().ToListAsync(ct);
            foreach (var recipientId in members.Where(x => x != access.Actor.UserId)) {
                var recipient = await bids.AccountAsync(Principal(recipientId), now, ct);
                if (recipient is null || !recipient.Internal || (approved ? !recipient.Roles.Contains(ApplicationRoles.TopCommittee) : !recipient.Roles.Contains(ApplicationRoles.EvaluationCommittee))) continue;
                db.Notifications.Add(new Notification { Id = Guid.NewGuid(), RecipientUserId = recipientId, TenderId = tenderId, CreatedAt = now,
                    Subject = "Manager report decision", Message = "The manager recorded " + form.Decision + " for " + tender.Title + ". Review the report and documented comments." });
            }
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = access.Actor.UserId, RecordType = "EvaluationReport", RecordId = tenderId.ToString(),
                Action = form.Decision.ToString(), PreviousStatus = "AwaitingManagerReview", NewStatus = tender.Status.ToString(), Reason = "Report " + report.Id + ": " + decision.Comments, OccurredAt = now });
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = access.Actor.UserId, RecordType = "Tender", RecordId = tenderId.ToString(),
                Action = "Tender.ManagerDecision", PreviousStatus = "AwaitingManagerReview", NewStatus = tender.Status.ToString(), Reason = "Decision " + decision.Id, OccurredAt = now });
            await db.SaveChangesAsync(ct);
            var final = await Access(user, tenderId, ct);
            var currentManager = await Manager(tenderId, ct);
            BidRules.Require(currentManager is not null && currentManager.Id == manager.Id && ReportReviewRules.Manager(final.Actor, currentManager, tenderId, clock.GetUtcNow()), 403, "Manager eligibility changed.");
            await tx.CommitAsync(ct);
            return true;
        });
    }
    private static string Summary(ReportSnapshot snapshot, EvaluationKind kind) => string.Join(Environment.NewLine,
        snapshot.Offers.Where(x => x.Status == BidStatus.Compliant).Select(x => x.Supplier + " — " +
            x.Results.Where(p => snapshot.Criteria.Single(c => c.Id == p.CriterionId).Kind == kind).Sum(p => p.WeightedPoints).ToString("0.######", CultureInfo.InvariantCulture) + " weighted points"));
}
