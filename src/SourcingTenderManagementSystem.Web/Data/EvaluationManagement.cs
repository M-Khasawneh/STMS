using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IEvaluationManagement
{
    Task<EvaluationPage> PageAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default);
    Task SaveAsync(ClaimsPrincipal user, Guid bidId, EvaluationForm form, bool confirm, CancellationToken ct = default);
    Task StartAsync(ClaimsPrincipal user, Guid tenderId, string version, CancellationToken ct = default);
}
public sealed class EvaluationManagement(ApplicationDbContext db, IBidData bids, TenderManagement tenders, TimeProvider clock) : IEvaluationManagement
{
    private async Task<(BidAccount Actor, Bid Bid, BidContext Context)> Access(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var actor = await bids.AccountAsync(principal, clock.GetUtcNow(), ct);
        BidRules.Require(actor is not null && actor.Internal, 403, "An authorized internal account is required.");
        var bid = await bids.ReadAsync(id, actor, clock.GetUtcNow(), ct) ?? throw new BidException(404, "Offer not found.");
        var context = await bids.ContextAsync(bid.Bid.TenderId, ct) ?? throw new BidException(404, "Tender not found.");
        return (actor, bid.Bid, context);
    }
    private async Task<List<CommitteeMember>> Members(Guid id, CancellationToken ct)
    {
        BidRules.Require(await db.CommitteeAssignments.CountAsync(x => x.TenderId == id && x.Kind == CommitteeKind.Evaluation, ct) <= 1,
            409, "Multiple evaluation committees exist for this tender. Resolve the data conflict first.");
        var now = clock.GetUtcNow();
        var rows = await (from m in db.CommitteeMembers.AsNoTracking() join a in db.CommitteeAssignments on new { m.CommitteeAssignmentId, m.TenderId }
            equals new { CommitteeAssignmentId = a.Id, a.TenderId }
            where m.TenderId == id && a.Kind == CommitteeKind.Evaluation && (m.EffectiveFrom == null || m.EffectiveFrom <= now) && (m.EffectiveTo == null || now < m.EffectiveTo)
            select m).ToListAsync(ct);
        BidRules.Require(rows.Count <= 4 && rows.Select(x => x.UserId).Distinct().Count() == rows.Count && rows.Select(x => x.Position).Distinct().Count() == rows.Count,
            409, "Committee membership is inconsistent. Review the assignments first.");
        return rows;
    }
    private async Task<bool> EligibleMember(CommitteeMember member, CancellationToken ct)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, member.UserId)], "EvaluationEligibility"));
        var actor = await bids.AccountAsync(principal, clock.GetUtcNow(), ct);
        if (actor is null || !actor.Internal || !actor.Roles.Contains(member.Position == CommitteeRules.Positions[0] ? ApplicationRoles.SourcingManager : ApplicationRoles.EvaluationCommittee)) return false;
        if (member.Position == CommitteeRules.Positions[1]) return await (from employee in db.EmployeeDepartments
            join source in db.PurchaseRequisitions on employee.DepartmentId equals source.DepartmentId
            join link in db.TenderPurchaseRequisitions on source.Id equals link.PurchaseRequisitionId
            where link.TenderId == member.TenderId && employee.UserId == member.UserId select employee).AnyAsync(ct);
        return CommitteeRules.Positions.Contains(member.Position);
    }
    private async Task<List<EvaluationScore>> Scores(Guid bidId, CancellationToken ct)
    {
        var technical = await db.TechnicalEvaluations.AsNoTracking().Where(x => x.BidId == bidId).ToListAsync(ct);
        var financial = await db.FinancialEvaluations.AsNoTracking().Where(x => x.BidId == bidId).ToListAsync(ct);
        return technical.Select(x => new EvaluationScore(x.Id, x.CommitteeMemberId, x.EvaluationCriterionId, x.Score, x.Notes, x.NonCompliance, x.EvaluatedAt, x.SubmittedAt))
            .Concat(financial.Select(x => new EvaluationScore(x.Id, x.CommitteeMemberId, x.EvaluationCriterionId, x.Score, x.Notes, null, x.EvaluatedAt, x.SubmittedAt))).ToList();
    }
    public async Task<EvaluationPage> PageAsync(ClaimsPrincipal user, Guid bidId, CancellationToken ct = default)
    {
        var access = await Access(user, bidId, ct);
        var members = await Members(access.Bid.TenderId, ct);
        var member = members.SingleOrDefault(x => x.UserId == access.Actor.UserId);
        var scores = await Scores(bidId, ct);
        var own = scores.Where(x => member is not null && x.MemberId == member.Id).ToArray();
        var form = new EvaluationForm { Version = EvaluationRules.Version(access.Context.Tender, access.Bid, member?.Id, own),
            Items = access.Context.Criteria.Select(c => {
                var score = own.SingleOrDefault(x => x.CriterionId == c.Id);
                return new EvaluationLine { CriterionId = c.Id, Score = score?.Score, Notes = score?.Notes, NonCompliance = score?.NonCompliance };
            }).ToList() };
        var confirmed = own.Any(x => x.SubmittedAt.HasValue);
        var canEdit = member is not null && !confirmed && access.Context.Tender.Status == TenderStatus.UnderEvaluation &&
            access.Bid.Status == BidStatus.Compliant && await EligibleMember(member, ct);
        var eligible = new List<CommitteeMember>();
        foreach (var m in members) if (await EligibleMember(m, ct)) eligible.Add(m);
        var mine = await db.CommitteeMembers.AsNoTracking().Where(x => x.TenderId == access.Bid.TenderId && x.UserId == access.Actor.UserId).Select(x => x.Id).ToListAsync(ct);
        var company = await bids.SupplierNameAsync(bidId, access.Actor, clock.GetUtcNow(), ct) ?? "Supplier";
        await Access(user, bidId, ct);
        return new(access.Bid, access.Context.Tender, company, access.Context.Criteria, form, canEdit, confirmed,
            eligible.Count(m => EvaluationRules.Complete(scores, m.Id, access.Context.Criteria)), EvaluationRules.Aggregate(scores, eligible, access.Context.Criteria),
            scores.Where(x => x.MemberId.HasValue && mine.Contains(x.MemberId.Value)).OrderByDescending(x => x.At).ToArray());
    }
    public async Task SaveAsync(ClaimsPrincipal user, Guid bidId, EvaluationForm form, bool confirm, CancellationToken ct = default)
    {
        await BidManagement.Mutate(async () => {
            var preliminary = await Access(user, bidId, ct);
            await using var tx = await bids.BeginAsync(preliminary.Bid.TenderId, ct);
            var access = await Access(user, bidId, ct);
            var members = await Members(access.Bid.TenderId, ct);
            var member = members.SingleOrDefault(x => x.UserId == access.Actor.UserId);
            BidRules.Require(member is not null && await EligibleMember(member, ct), 403, "An effective appointment to this tender is required.");
            BidRules.Require(access.Context.Tender.Status == TenderStatus.UnderEvaluation && access.Bid.Status == BidStatus.Compliant, 409,
                "Only compliant offers in an Under Evaluation tender may be scored.");
            var scores = (await Scores(bidId, ct)).Where(x => x.MemberId == member.Id).ToArray();
            BidRules.Require(!scores.Any(x => x.SubmittedAt.HasValue), 409, "Your confirmed evaluation is read-only.");
            ComplianceRules.Version(form.Version, EvaluationRules.Version(access.Context.Tender, access.Bid, member.Id, scores));
            EvaluationRules.Validate(form, access.Context.Criteria, confirm);
            var technical = await db.TechnicalEvaluations.Where(x => x.BidId == bidId && x.CommitteeMemberId == member.Id).ToListAsync(ct);
            var financial = await db.FinancialEvaluations.Where(x => x.BidId == bidId && x.CommitteeMemberId == member.Id).ToListAsync(ct);
            var now = clock.GetUtcNow();
            foreach (var input in form.Items)
            {
                var criterion = access.Context.Criteria.Single(x => x.Id == input.CriterionId);
                if (criterion.Kind == EvaluationKind.Technical) {
                    var row = technical.SingleOrDefault(x => x.EvaluationCriterionId == criterion.Id);
                    if (!input.Score.HasValue) { if (row is not null) db.TechnicalEvaluations.Remove(row); continue; }
                    if (row is null) {
                        row = new TechnicalEvaluation { Id = Guid.NewGuid(), TenderId = access.Bid.TenderId, BidId = bidId, EvaluationCriterionId = criterion.Id, CommitteeMemberId = member.Id };
                        db.TechnicalEvaluations.Add(row);
                    }
                    row.Score = input.Score.Value; row.Notes = input.Notes?.Trim() ?? ""; row.NonCompliance = input.NonCompliance?.Trim();
                    row.EvaluatorUserId = access.Actor.UserId; row.RecordedByUserId = access.Actor.UserId; row.EvaluatedAt = now; row.SubmittedAt = confirm ? now : null;
                } else {
                    BidRules.Require(string.IsNullOrWhiteSpace(input.NonCompliance), 400, "Record non-compliance under technical criteria.");
                    var row = financial.SingleOrDefault(x => x.EvaluationCriterionId == criterion.Id);
                    if (!input.Score.HasValue) { if (row is not null) db.FinancialEvaluations.Remove(row); continue; }
                    if (row is null) {
                        row = new FinancialEvaluation { Id = Guid.NewGuid(), TenderId = access.Bid.TenderId, BidId = bidId, EvaluationCriterionId = criterion.Id, CommitteeMemberId = member.Id };
                        db.FinancialEvaluations.Add(row);
                    }
                    row.Score = input.Score.Value; row.Notes = input.Notes?.Trim() ?? "";
                    row.EvaluatorUserId = access.Actor.UserId; row.RecordedByUserId = access.Actor.UserId; row.EvaluatedAt = now; row.SubmittedAt = confirm ? now : null;
                }
            }
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = access.Actor.UserId, RecordType = "Bid", RecordId = bidId.ToString(),
                Action = confirm ? "EvaluationConfirmed" : "EvaluationDraftSaved", OccurredAt = now, Reason = $"Committee membership {member.Id:D}" });
            await db.SaveChangesAsync(ct);
            await Access(user, bidId, ct);
            BidRules.Require(await EligibleMember(member, ct) && CommitteeRules.Effective(member, clock.GetUtcNow()), 403, "Committee eligibility changed.");
            await tx.CommitAsync(ct);
            return true;
        });
    }
    public Task StartAsync(ClaimsPrincipal user, Guid tenderId, string version, CancellationToken ct = default) =>
        BidManagement.Mutate(async () => { await StartCoreAsync(user, tenderId, version, ct); return true; });
    private async Task StartCoreAsync(ClaimsPrincipal user, Guid tenderId, string version, CancellationToken ct)
    {
        var actor = await bids.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(actor is not null && ComplianceRules.Officer(actor), 403, "Only an authorized Sourcing Officer may start evaluation.");
        await using var tx = await bids.BeginAsync(tenderId, ct);
        actor = await bids.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(actor is not null && ComplianceRules.Officer(actor), 403, "Officer eligibility changed.");
        var detail = await tenders.DetailsAsync(tenderId, await tenders.ActorAsync(user));
        ComplianceRules.Version(version, detail.Version);
        BidRules.Require(detail.Tender.Status == TenderStatus.Closed && TenderManagement.Approved(detail.Criteria), 409, "A Closed tender with approved criteria is required.");
        var members = await Members(tenderId, ct);
        BidRules.Require(members.Count == 4 && CommitteeRules.Positions.All(p => members.Any(m => m.Position == p)), 409, "Assign all four committee positions before starting.");
        foreach (var member in members) BidRules.Require(await EligibleMember(member, ct), 409, "A committee appointment is no longer eligible.");
        BidRules.Require(await db.Bids.AnyAsync(x => x.TenderId == tenderId && x.Status == BidStatus.Compliant && x.SubmittedAt != null, ct), 409, "At least one compliant submitted offer is required.");
        BidRules.Require(!await db.Bids.AnyAsync(x => x.TenderId == tenderId && x.SubmittedAt != null && x.Status != BidStatus.Compliant && x.Status != BidStatus.NonCompliant, ct),
            409, "Complete compliance decisions for every submitted offer before starting evaluation.");
        BidRules.Require(!await db.ClarificationRequests.AnyAsync(q => q.BidId != null && q.Status != ClarificationStatus.Resolved &&
            db.Bids.Any(b => b.Id == q.BidId && b.TenderId == tenderId), ct), 409, "Resolve outstanding bid clarifications before starting evaluation.");
        var tender = await db.Tenders.SingleAsync(x => x.Id == tenderId, ct);
        tender.Status = TenderStatus.UnderEvaluation;
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = actor.UserId, RecordType = "Tender", RecordId = tenderId.ToString(),
            Action = "Tender.EvaluationStarted", PreviousStatus = "Closed", NewStatus = "UnderEvaluation", OccurredAt = clock.GetUtcNow() });
        foreach (var member in members) db.Notifications.Add(new Notification { Id = Guid.NewGuid(), RecipientUserId = member.UserId, TenderId = tenderId,
            Subject = "Evaluation started", Message = "Evaluation has started for " + tender.Title + ". Open Submitted offers to evaluate compliant offers.", CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        var finalActor = await bids.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(finalActor is not null && ComplianceRules.Officer(finalActor), 403, "Officer eligibility changed.");
        await tx.CommitAsync(ct);
    }
}
