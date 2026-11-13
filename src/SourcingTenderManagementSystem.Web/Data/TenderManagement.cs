using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;
public sealed class TenderException(int status, string message) : Exception(message) { public int Status { get; } = status; }

public sealed class TenderManagement(ApplicationDbContext db, UserManager<IdentityUser> users, TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();
    public async Task<TenderActor> ActorAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal) ?? throw Error(403, "Sign in to access tenders.");
        var roles = await users.GetRolesAsync(user);
        var profile = await db.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (roles.Contains(ApplicationRoles.Supplier) || profile is not null)
        {
            if (roles.Count != 1 || !roles.Contains(ApplicationRoles.Supplier) || profile?.Status != SupplierStatus.Approved || await users.IsLockedOutAsync(user)) throw Error(403, "An approved eligible supplier is required.");
            return new(user.Id, false, false, false, false, profile.Id);
        }
        if (!await db.EmployeeDepartments.AnyAsync(x => x.UserId == user.Id)) throw Error(403, "Employee department is required.");
        var actor = new TenderActor(user.Id, roles.Contains(ApplicationRoles.SourcingOfficer), roles.Contains(ApplicationRoles.Admin), roles.Contains(ApplicationRoles.SourcingManager), roles.Contains(ApplicationRoles.RequestingDepartment), null);
        if (!actor.Internal && !actor.Requester) throw Error(403, "Tender access is not permitted.");
        return actor;
    }
    public async Task<IReadOnlyList<Tender>> ListAsync(TenderActor actor)
    {
        var query = db.Tenders.AsNoTracking();
        if (actor.SupplierId.HasValue) query = query.Where(t => t.Status != TenderStatus.Draft && db.TenderSuppliers.Any(s => s.TenderId == t.Id && s.SupplierProfileId == actor.SupplierId && db.Invitations.Any(i => i.TenderSupplierId == s.Id)));
        else if (!actor.Internal) query = query.Where(t => db.TenderPurchaseRequisitions.Any(l => l.TenderId == t.Id && db.PurchaseRequisitions.Any(r => r.Id == l.PurchaseRequisitionId && r.CreatedByUserId == actor.Id)));
        return await query.OrderByDescending(x => x.SubmissionDeadline).ThenBy(x => x.Id).ToListAsync();
    }
    public async Task<TenderDetails> DetailsAsync(Guid id, TenderActor actor)
    {
        var tender = await db.Tenders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw Error(404, "Tender not found.");
        var link = await db.TenderPurchaseRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenderId == id) ?? throw Error(404, "Tender provenance is unavailable.");
        var source = await db.PurchaseRequisitions.AsNoTracking().SingleAsync(x => x.Id == link.PurchaseRequisitionId);
        if (actor.SupplierId.HasValue)
        {
            if (tender.Status == TenderStatus.Draft || !await db.TenderSuppliers.AnyAsync(s => s.TenderId == id && s.SupplierProfileId == actor.SupplierId && db.Invitations.Any(i => i.TenderSupplierId == s.Id))) throw Error(404, "Tender not found.");
        }
        else if (!actor.Internal && (!actor.Requester || source.CreatedByUserId != actor.Id)) throw Error(404, "Tender not found.");
        var criteria = await CriteriaAsync(id);
        return new(tender, source, await db.PurchaseRequisitionItems.AsNoTracking().Where(x => x.PurchaseRequisitionId == source.Id).OrderBy(x => x.Id).ToListAsync(), criteria, await VersionAsync(tender, criteria), actor);
    }
    public async Task<IReadOnlyList<PurchaseRequisition>> EligibleRequestsAsync(TenderActor actor)
    {
        Manage(actor);
        return await db.PurchaseRequisitions.AsNoTracking().Where(r => r.Status == PurchaseRequestStatus.Approved && !db.TenderPurchaseRequisitions.Any(l => l.PurchaseRequisitionId == r.Id)).OrderBy(r => r.Title).ToListAsync();
    }
    public async Task<Guid> SaveAsync(Guid? id, TenderActor actor, TenderForm form)
    {
        Manage(actor); Validate(form);
        DateTimeOffset start, end;
        try { start = TenderTime.Utc(form.Start!.Value); end = TenderTime.Utc(form.Deadline!.Value); }
        catch(ArgumentException) { throw Error(400,"Select valid, unambiguous Asia/Amman submission times."); }
        if (start >= end) throw Error(400, "Submission start must be before the deadline.");
        await RequireMigrationAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Tender tender;
        if (id.HasValue)
        {
            tender = await LockAsync(id.Value); await CheckVersion(tender, form.Version);
            if (tender.Status != TenderStatus.Draft) throw Error(400, "Only Draft tender content and dates can be edited.");
            if (!await db.TenderPurchaseRequisitions.AnyAsync(x => x.TenderId == id && x.PurchaseRequisitionId == form.RequisitionId)) throw Error(400, "A tender's source requisition cannot be replaced.");
        }
        else
        {
            var source = await db.PurchaseRequisitions.FromSqlInterpolated($"SELECT * FROM dbo.PurchaseRequisitions WITH (UPDLOCK,HOLDLOCK) WHERE Id={form.RequisitionId!.Value}").SingleOrDefaultAsync();
            if (source is null || source.Status != PurchaseRequestStatus.Approved || await db.TenderPurchaseRequisitions.AnyAsync(x => x.PurchaseRequisitionId == source.Id)) throw Error(409, "Select an unconverted Approved requisition. It may already have been converted.");
            tender = new Tender { Id = Guid.NewGuid(), CreatedByUserId = actor.Id, Status = TenderStatus.Draft };
            db.Tenders.Add(tender); db.TenderPurchaseRequisitions.Add(new TenderPurchaseRequisition { TenderId = tender.Id, PurchaseRequisitionId = source.Id });
            source.Status = PurchaseRequestStatus.ConvertedToTender;
            Audit(actor.Id, "PurchaseRequisition", source.Id, "Requisition.ConvertedToTender", "Approved", "ConvertedToTender", "Linked tender: " + tender.Id);
        }
        var before = id.HasValue ? JsonSerializer.Serialize(tender) : null;
        tender.Title = form.Title.Trim(); tender.Description = form.Description.Trim(); tender.Terms = form.Terms.Trim(); tender.SubmissionStart = start; tender.SubmissionDeadline = end;
        Audit(actor.Id, "Tender", tender.Id, id.HasValue ? "Tender.Edited" : "Tender.Created", tender.Status.ToString(), tender.Status.ToString(), JsonSerializer.Serialize(new { Before = before, After = tender }));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return tender.Id;
    }
    public async Task ChangeStatusAsync(Guid id, TenderActor actor, string version, TenderStatus decision)
    {
        Manage(actor);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await LockAsync(id); await CheckVersion(tender, version);
        if (tender.Status == TenderStatus.Draft && decision == TenderStatus.Published)
        { if (Now >= tender.SubmissionDeadline) throw Error(400, "The submission deadline must be in the future before publication."); }
        else if (tender.Status == TenderStatus.Published && decision == TenderStatus.Open)
        {
            if (Now < tender.SubmissionStart || Now >= tender.SubmissionDeadline) throw Error(400, "Open the tender within its configured submission period.");
            var criteria = await CriteriaAsync(id);
            if (!Approved(criteria)) throw Error(400, "Criteria totaling exactly 100% must be approved before opening.");
        }
        else if (tender.Status == TenderStatus.Open && decision == TenderStatus.Closed)
        { if (Now < tender.SubmissionDeadline) throw Error(400, "The configured submission period has not ended."); }
        else throw Error(400, "Invalid tender transition. Use Draft -> Published -> Open -> Closed.");
        var previous = tender.Status; tender.Status = decision;
        Audit(actor.Id, "Tender", id, "Tender.StatusChanged", previous.ToString(), decision.ToString(), null);
        if (decision == TenderStatus.Closed) await ExpireLocked(tender);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task SaveCriteriaAsync(Guid id, TenderActor actor, CriteriaForm form)
    {
        Manage(actor);
        if (form.Items.Count == 0) throw Error(400, "Define at least one criterion; no defaults are supplied.");
        foreach (var item in form.Items)
        {
            Validate(item);
            if (!Enum.IsDefined(item.Kind!.Value) || decimal.Round(item.Weight!.Value, 6) != item.Weight) throw Error(400, "Use a valid criterion kind and a percentage with up to six decimal places.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await LockAsync(id); await CheckVersion(tender, form.Version);
        var rows = await db.EvaluationCriteria.Where(x => x.TenderId == id).ToListAsync();
        if (tender.Status is not (TenderStatus.Draft or TenderStatus.Published) || rows.Any(x => x.ApprovedAt.HasValue || x.ApprovedByUserId != null)) throw Error(400, "Approved criteria and weights are read-only.");
        var posted = form.Items.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (posted.Distinct().Count() != posted.Count || posted.Any(x => !rows.Any(r => r.Id == x))) throw Error(400, "A criterion does not belong to this tender.");
        foreach (var removed in rows.Where(x => !posted.Contains(x.Id)))
        { db.EvaluationCriteria.Remove(removed); Audit(actor.Id, "EvaluationCriterion", removed.Id, "Criteria.Removed", null, null, removed.Name); }
        foreach (var input in form.Items)
        {
            var row = input.Id.HasValue ? rows.Single(x => x.Id == input.Id) : new EvaluationCriterion { Id = Guid.NewGuid(), TenderId = id };
            var changed = !input.Id.HasValue || row.Name != input.Name.Trim() || row.Description != input.Description.Trim() || row.Kind != input.Kind || row.Weight != input.Weight;
            row.Name = input.Name.Trim(); row.Description = input.Description.Trim(); row.Kind = input.Kind!.Value; row.Weight = input.Weight!.Value;
            if (!input.Id.HasValue) db.EvaluationCriteria.Add(row);
            if (changed) Audit(actor.Id, "EvaluationCriterion", row.Id, "Criteria.Configured", null, null, JsonSerializer.Serialize(new { row.TenderId, row.Name, row.Description, row.Kind, row.Weight }));
        }
        Audit(actor.Id, "Tender", id, "Tender.CriteriaSaved", null, null, "Evaluation configuration updated.");
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task ApproveCriteriaAsync(Guid id, TenderActor actor, string version)
    {
        if (!actor.Approve) throw Error(403, "Only Sourcing Managers may approve criteria. Admin is read-only.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await LockAsync(id); await CheckVersion(tender, version);
        var rows = await db.EvaluationCriteria.Where(x => x.TenderId == id).ToListAsync();
        if (tender.Status is not (TenderStatus.Draft or TenderStatus.Published) || rows.Any(x => x.ApprovedAt.HasValue)) throw Error(400, "Criteria are already approved or the tender is no longer configurable.");
        if (rows.Count == 0 || rows.Sum(x => x.Weight) != 100m) throw Error(400, "The total percentage weight must equal exactly 100%.");
        var keys = rows.Select(x => x.Id.ToString()).ToArray();
        if (await db.AuditLogs.AnyAsync(x => x.RecordType == "EvaluationCriterion" && keys.Contains(x.RecordId) && x.Action == "Criteria.Configured" && x.ActorUserId == actor.Id)) throw Error(403, "Self-approval is prohibited for anyone who configured the current criteria.");
        // Refuse approval of legacy/unattributed rows; configuration provenance is required.
        var configured = await db.AuditLogs.Where(x => x.RecordType == "EvaluationCriterion" && keys.Contains(x.RecordId) && x.Action == "Criteria.Configured" && x.ActorUserId != null).Select(x => x.RecordId).Distinct().CountAsync();
        if (configured != rows.Count) throw Error(400, "All criteria require recorded configuration provenance before approval.");
        var now = Now; foreach (var row in rows) { row.ApprovedByUserId = actor.Id; row.ApprovedAt = now; }
        Audit(actor.Id, "Tender", id, "Tender.CriteriaApproved", null, null, "Approved evaluation criteria totaling 100%.");
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public static bool Approved(IReadOnlyList<EvaluationCriterion> criteria) => criteria.Count > 0 && criteria.Sum(x => x.Weight) == 100m && criteria.All(x => x.ApprovedAt.HasValue && !string.IsNullOrEmpty(x.ApprovedByUserId));
    public bool InSubmissionPeriod(Tender tender) => tender.Status == TenderStatus.Open && Now >= tender.SubmissionStart && Now < tender.SubmissionDeadline;
    public async Task<TenderInvitations> InvitationsAsync(Guid id, TenderActor actor)
    {
        if (!actor.Internal || (!actor.Officer && !actor.Admin)) throw Error(403, "Invitation management is restricted to officers and Admin oversight.");
        await DetailsAsync(id, actor); await ExpireAsync(id);
        var rows = await (from i in db.Invitations.AsNoTracking() join s in db.TenderSuppliers on i.TenderSupplierId equals s.Id join p in db.SupplierProfiles on s.SupplierProfileId equals p.Id join t in db.Tenders on s.TenderId equals t.Id where t.Id == id select new InvitationRow(i,t,p)).ToListAsync();
        var eligible = new List<SupplierProfile>();
        if (actor.Manage)
            foreach (var profile in await db.SupplierProfiles.AsNoTracking().Where(x => x.Status == SupplierStatus.Approved && !db.TenderSuppliers.Any(s => s.TenderId == id && s.SupplierProfileId == x.Id)).OrderBy(x => x.CompanyName).ToListAsync())
                if (await Eligible(profile)) eligible.Add(profile);
        return new(await DetailsAsync(id, actor), rows, eligible);
    }
    public async Task InviteAsync(Guid id, TenderActor actor, InviteForm form)
    {
        Manage(actor); await RequireMigrationAsync();
        if (form.SupplierIds.Count == 0 || form.SupplierIds.Distinct().Count() != form.SupplierIds.Count) throw Error(400, "Select distinct approved suppliers.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await LockAsync(id); await CheckVersion(tender, form.Version);
        if (tender.Status != TenderStatus.Published || Now >= tender.SubmissionDeadline) throw Error(400, "Invitations can be sent only while Published and before the submission deadline.");
        foreach (var supplierId in form.SupplierIds.OrderBy(x => x))
        {
            var profile = await db.SupplierProfiles.FromSqlInterpolated($"SELECT * FROM dbo.SupplierProfiles WITH (UPDLOCK,HOLDLOCK) WHERE Id={supplierId}").SingleOrDefaultAsync();
            if (profile is null || !await Eligible(profile)) throw Error(400, "A selected supplier is not approved and eligible.");
            if (await db.TenderSuppliers.AnyAsync(x => x.TenderId == id && x.SupplierProfileId == supplierId)) throw Error(409, "This supplier has already been invited. Resends are not supported.");
            var link = new TenderSupplier { Id = Guid.NewGuid(), TenderId = id, SupplierProfileId = supplierId };
            var invitation = new Invitation { Id = Guid.NewGuid(), TenderSupplierId = link.Id, SentByUserId = actor.Id, SentAt = Now, ExpiresAt = tender.SubmissionDeadline, Status = InvitationStatus.Pending };
            db.TenderSuppliers.Add(link); db.Invitations.Add(invitation);
            Notify(profile.UserId, tender.Id, "Tender invitation", "You have been invited to " + tender.Title + ". View your invitation inbox to accept or decline.");
            Audit(actor.Id, "Invitation", invitation.Id, "Invitation.Created", null, "Pending", "Tender: " + id);
        }
        Audit(actor.Id, "Tender", id, "Tender.InvitationsSent", null, null, "Invitations sent to selected eligible suppliers.");
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task<IReadOnlyList<InvitationRow>> InboxAsync(TenderActor actor)
    {
        if (!actor.SupplierId.HasValue) throw Error(403, "Supplier access is required.");
        var tenderIds = await db.TenderSuppliers.Where(x => x.SupplierProfileId == actor.SupplierId && db.Invitations.Any(i => i.TenderSupplierId == x.Id)).Select(x => x.TenderId).ToListAsync();
        foreach (var id in tenderIds) await ExpireAsync(id);
        return (await OwnedRowsAsync(actor)).OrderByDescending(x => x.Invitation.SentAt).ToArray();
    }
    public async Task<InvitationRow> InvitationAsync(Guid id, TenderActor actor)
    {
        var row = (await OwnedRowsAsync(actor,id)).SingleOrDefault() ?? throw Error(404, "Invitation not found.");
        await ExpireAsync(row.Tender.Id);
        return (await OwnedRowsAsync(actor,id)).Single();
    }
    public static string InvitationVersion(InvitationRow row) => Hash(new { row.Invitation, row.Tender.Status, row.Tender.SubmissionDeadline });
    public async Task RespondAsync(Guid id, TenderActor actor, string version, InvitationStatus decision)
    {
        if (decision is not (InvitationStatus.Accepted or InvitationStatus.Declined)) throw Error(400, "Select Accept or Decline.");
        var owned = (await OwnedRowsAsync(actor,id)).SingleOrDefault() ?? throw Error(404, "Invitation not found.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await LockAsync(owned.Tender.Id);
        var invitation = await db.Invitations.SingleAsync(x => x.Id == id);
        if (Now >= tender.SubmissionDeadline || tender.Status == TenderStatus.Closed)
        {
            await ExpireLocked(tender); await db.SaveChangesAsync(); await tx.CommitAsync();
            throw Error(409, "This invitation has expired.");
        }
        if (tender.Status is not (TenderStatus.Published or TenderStatus.Open) || invitation.Status != InvitationStatus.Pending) throw Error(409, "Only a Pending invitation can be answered once.");
        if (version != InvitationVersion(new(invitation,tender,owned.Supplier))) throw Error(409, "The invitation has changed. Reload before responding.");
        var profile = await db.SupplierProfiles.SingleAsync(x => x.Id == actor.SupplierId);
        if (!await Eligible(profile)) throw Error(403, "An approved eligible supplier is required.");
        if (Now >= tender.SubmissionDeadline)
        {
            await ExpireLocked(tender); await db.SaveChangesAsync(); await tx.CommitAsync();
            throw Error(409, "This invitation has expired.");
        }
        invitation.Status = decision; invitation.RespondedAt = Now;
        Notify(tender.CreatedByUserId, tender.Id, "Supplier invitation response", profile.CompanyName + " " + decision.ToString().ToLowerInvariant() + " the invitation for " + tender.Title + ".");
        Audit(actor.Id, "Invitation", id, "Invitation.Responded", "Pending", decision.ToString(), "Tender: " + tender.Id);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task ExpireAsync(Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await LockAsync(id); await ExpireLocked(tender); await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    private async Task ExpireLocked(Tender tender)
    {
        if (Now < tender.SubmissionDeadline && tender.Status != TenderStatus.Closed) return;
        var rows = await db.Invitations.Where(i => i.Status == InvitationStatus.Pending && db.TenderSuppliers.Any(s => s.Id == i.TenderSupplierId && s.TenderId == tender.Id)).ToListAsync();
        foreach (var invitation in rows) { invitation.Status = InvitationStatus.Expired; Audit(null, "Invitation", invitation.Id, "Invitation.Expired", "Pending", "Expired", "Submission period ended."); }
    }
    private IQueryable<Invitation> OwnedInvitations(TenderActor actor)
    {
        if (!actor.SupplierId.HasValue) throw Error(403, "Supplier access is required.");
        return db.Invitations.AsNoTracking().Where(i => db.TenderSuppliers.Any(s => s.Id == i.TenderSupplierId && s.SupplierProfileId == actor.SupplierId && db.Tenders.Any(t => t.Id == s.TenderId && t.Status != TenderStatus.Draft)));
    }
    private async Task<List<InvitationRow>> OwnedRowsAsync(TenderActor actor, Guid? id = null)
    {
        var invitations = OwnedInvitations(actor);
        if(id.HasValue) invitations = invitations.Where(i => i.Id == id.Value);
        // Filter entities before constructing the view record so ownership remains a SQL predicate.
        return await (from i in invitations join s in db.TenderSuppliers on i.TenderSupplierId equals s.Id join p in db.SupplierProfiles on s.SupplierProfileId equals p.Id join t in db.Tenders on s.TenderId equals t.Id select new InvitationRow(i,t,p)).ToListAsync();
    }
    private async Task<bool> Eligible(SupplierProfile profile)
    {
        var user = await users.FindByIdAsync(profile.UserId);
        return profile.Status == SupplierStatus.Approved && user is not null && !await users.IsLockedOutAsync(user) && (await users.GetRolesAsync(user)).SequenceEqual(new[] { ApplicationRoles.Supplier });
    }
    private async Task<Tender> LockAsync(Guid id) => await db.Tenders.FromSqlInterpolated($"SELECT * FROM dbo.Tenders WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync() ?? throw Error(404, "Tender not found.");
    private Task<List<EvaluationCriterion>> CriteriaAsync(Guid id) => db.EvaluationCriteria.AsNoTracking().Where(x => x.TenderId == id).OrderBy(x => x.Id).ToListAsync();
    private async Task<string> VersionAsync(Tender tender, IEnumerable<EvaluationCriterion> criteria) => Hash(new { Tender = tender, Criteria = criteria.OrderBy(x => x.Id), Events = await db.AuditLogs.AsNoTracking().Where(x => x.RecordType == "Tender" && x.RecordId == tender.Id.ToString()).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync() });
    private async Task CheckVersion(Tender tender, string? version) { if (string.IsNullOrEmpty(version) || version != await VersionAsync(tender, await CriteriaAsync(tender.Id))) throw Error(409, "This tender has changed. Reload before saving or acting again."); }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static void Validate(object value) { var errors = new List<ValidationResult>(); if (!Validator.TryValidateObject(value,new(value),errors,true)) throw Error(400,errors[0].ErrorMessage!); }
    private static void Manage(TenderActor actor) { if (!actor.Manage) throw Error(403, "Only Sourcing Officers manage tenders. Admin is read-only."); }
    private async Task RequireMigrationAsync() { if (!(await db.Database.GetAppliedMigrationsAsync()).Contains("20261113180000_TenderInvitationIntegrity")) throw Error(409, "Tender database deployment is awaiting approval."); }
    private void Notify(string recipient, Guid tender, string subject, string message) => db.Notifications.Add(new Notification { Id=Guid.NewGuid(), RecipientUserId=recipient, TenderId=tender, Subject=subject, Message=message, CreatedAt=Now });
    private void Audit(string? actor, string type, Guid id, string action, string? before, string? after, string? reason) => db.AuditLogs.Add(new AuditLog { Id=Guid.NewGuid(), ActorUserId=actor, RecordType=type, RecordId=id.ToString(), Action=action, PreviousStatus=before, NewStatus=after, Reason=reason, OccurredAt=Now });
    private static TenderException Error(int status,string message) => new(status,message);
}
