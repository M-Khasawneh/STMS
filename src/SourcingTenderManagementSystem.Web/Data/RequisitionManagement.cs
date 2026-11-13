using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed class RequisitionException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class RequisitionManagement(ApplicationDbContext database, UserManager<IdentityUser> users, TimeProvider clock)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
    public async Task<RequisitionActor> ActorAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal) ?? throw new RequisitionException(403, "An employee account is required.");
        var roles = await users.GetRolesAsync(user);
        var department = await database.EmployeeDepartments.AsNoTracking().Where(x => x.UserId == user.Id).Select(x => (Guid?)x.DepartmentId).SingleOrDefaultAsync();
        if (department is null || await database.SupplierProfiles.AnyAsync(x => x.UserId == user.Id)) throw new RequisitionException(403, "An employee department is required.");
        var actor = new RequisitionActor(user.Id, department.Value, roles.Contains(ApplicationRoles.RequestingDepartment), roles.Contains(ApplicationRoles.SourcingOfficer), roles.Contains(ApplicationRoles.Admin), roles.Contains(ApplicationRoles.Supplier));
        if (!actor.CanAccess) throw new RequisitionException(403, "Requisition access is not permitted.");
        return actor;
    }
    public Task<string> DepartmentNameAsync(Guid id) => database.Departments.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).SingleAsync();
    public async Task<RequisitionList> ListAsync(RequisitionActor actor)
    {
        var query = database.PurchaseRequisitions.AsNoTracking();
        if (!actor.CanAccess) throw new RequisitionException(403, "Requisition access is not permitted.");
        if (!actor.Admin && !actor.Officer) query = query.Where(x => x.CreatedByUserId == actor.UserId);
        return new(await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(), actor);
    }
    public async Task<RequisitionDetails> DetailsAsync(Guid id, RequisitionActor actor)
    {
        var request = await database.PurchaseRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (request is null || !actor.CanRead(request)) throw new RequisitionException(404, "Requisition not found.");
        var items = await ItemsAsync(id);
        var history = await HistoryAsync(id);
        return new(request, items, await database.PurchaseRequisitionReviews.AsNoTracking().Where(x => x.PurchaseRequisitionId == id).OrderBy(x => x.ReviewedAt).ThenBy(x => x.Id).ToListAsync(),
            history, await DepartmentNameAsync(request.DepartmentId), await database.Users.Where(x => x.Id == request.CreatedByUserId).Select(x => x.UserName ?? "").SingleAsync(), Version(request, items, history), actor);
    }
    public async Task<Guid> SaveAsync(Guid? id, RequisitionActor actor, RequisitionForm form, bool submit)
    {
        Validate(form, submit);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        PurchaseRequisition request;
        List<PurchaseRequisitionItem> oldItems;
        if (id.HasValue)
        {
            request = await LockAsync(id.Value, actor);
            if (!actor.CanEdit(request)) throw new RequisitionException(403, "Only the owner may edit Draft or Returned requisitions. Admins and officers cannot edit content.");
            oldItems = await database.PurchaseRequisitionItems.Where(x => x.PurchaseRequisitionId == id).ToListAsync();
            await CheckVersionAsync(request, oldItems, form.Version);
        }
        else
        {
            if (!actor.CanCreate) throw new RequisitionException(403, "Only requesting employees may create requisitions.");
            request = new PurchaseRequisition { Id = Guid.NewGuid(), DepartmentId = actor.DepartmentId, CreatedByUserId = actor.UserId, Status = PurchaseRequestStatus.Draft, CreatedAt = clock.GetUtcNow() };
            oldItems = [];
            database.PurchaseRequisitions.Add(request);
        }
        var before = Snapshot(request, oldItems);
        var existingIds = oldItems.Select(x => x.Id).ToHashSet();
        var postedIds = form.Items.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (postedIds.Distinct().Count() != postedIds.Count || postedIds.Any(x => !existingIds.Contains(x))) throw new RequisitionException(400, "An item does not belong to this requisition.");
        request.Title = form.Title.Trim(); request.Reason = form.Reason.Trim(); request.Priority = form.Priority; request.TargetDate = form.TargetDate!.Value;
        database.PurchaseRequisitionItems.RemoveRange(oldItems.Where(x => !postedIds.Contains(x.Id)));
        var finalItems = new List<PurchaseRequisitionItem>();
        foreach (var input in form.Items)
        {
            var item = input.Id.HasValue ? oldItems.Single(x => x.Id == input.Id.Value) : new PurchaseRequisitionItem { Id = Guid.NewGuid(), PurchaseRequisitionId = request.Id };
            item.Description = input.Description.Trim(); item.Quantity = input.Quantity; item.Specifications = input.Specifications.Trim();
            if (!input.Id.HasValue) database.PurchaseRequisitionItems.Add(item);
            finalItems.Add(item);
        }
        Audit(actor, request, id.HasValue ? "Requisition.Edited" : "Requisition.Created", request.Status, request.Status,
            JsonSerializer.Serialize(new { Before = id.HasValue ? before : null, After = Snapshot(request, finalItems) }));
        if (submit) await SubmitCoreAsync(request, actor, finalItems);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return request.Id;
    }
    public async Task SubmitAsync(Guid id, RequisitionActor actor, string? version)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await LockAsync(id, actor);
        if (!actor.CanEdit(request)) throw new RequisitionException(403, "Only the owner may submit a Draft or Returned requisition.");
        var items = await ItemsAsync(id);
        await CheckVersionAsync(request, items, version);
        Validate(new RequisitionForm { Title = request.Title, Reason = request.Reason, Priority = request.Priority, TargetDate = request.TargetDate,
            Items = items.Select(x => new RequisitionItemForm { Description = x.Description, Quantity = x.Quantity, Specifications = x.Specifications }).ToList() }, true);
        await SubmitCoreAsync(request, actor, items);
        await database.SaveChangesAsync(); await transaction.CommitAsync();
    }
    public async Task ReviewAsync(Guid id, RequisitionActor actor, RequisitionActionForm form)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await LockAsync(id, actor);
        if (!actor.CanReview(request)) throw new RequisitionException(403, "Officers cannot review their own requisitions or requisitions from their own department. Admins have read-only access.");
        var reviewItems = await ItemsAsync(id);
        await CheckVersionAsync(request, reviewItems, form.Version);
        if (form.Decision is not PurchaseRequestStatus decision || !RequisitionActor.ReviewTransition(request.Status, decision)) throw new RequisitionException(400, "Invalid transition. Start review before approving, rejecting or returning a request.");
        if (decision is PurchaseRequestStatus.Rejected or PurchaseRequestStatus.Returned && string.IsNullOrWhiteSpace(form.Comments)) throw new RequisitionException(400, "A reason is required for rejection or return.");
        var previous = request.Status; request.Status = decision;
        database.PurchaseRequisitionReviews.Add(new PurchaseRequisitionReview { Id = Guid.NewGuid(), PurchaseRequisitionId = id, ReviewerUserId = actor.UserId, Outcome = decision, Comments = form.Comments?.Trim() ?? "", ReviewedAt = clock.GetUtcNow() });
        var auditReason = decision == PurchaseRequestStatus.Returned
            ? JsonSerializer.Serialize(new { Comments = form.Comments!.Trim(), ReturnContent = CanonicalContent(request, reviewItems) })
            : form.Comments?.Trim();
        Audit(actor, request, "Requisition.Reviewed", previous, decision, auditReason);
        await database.SaveChangesAsync(); await transaction.CommitAsync();
    }
    private async Task SubmitCoreAsync(PurchaseRequisition request, RequisitionActor actor, IEnumerable<PurchaseRequisitionItem> items)
    {
        var previous = request.Status;
        if (previous is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.Returned)) throw new RequisitionException(400, "This requisition cannot be submitted.");
        if (previous == PurchaseRequestStatus.Returned)
        {
            var returned = (await HistoryAsync(request.Id)).LastOrDefault(x => x.Action == "Requisition.Reviewed" && x.NewStatus == "Returned");
            if (returned?.Reason is null) throw new RequisitionException(400, "The return history needs review before resubmission.");
            try
            {
                using var evidence = JsonDocument.Parse(returned.Reason);
                if (evidence.RootElement.GetProperty("ReturnContent").GetString() == CanonicalContent(request, items))
                    throw new RequisitionException(400, "Correct the request content before resubmitting a Returned requisition.");
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException)
            { throw new RequisitionException(400, "The return history needs review before resubmission."); }
        }
        request.Status = PurchaseRequestStatus.Submitted; request.SubmittedAt = clock.GetUtcNow();
        Audit(actor, request, "Requisition.Submitted", previous, request.Status, null);
    }
    public void Validate(RequisitionForm form, bool submit)
    {
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(form, new(form), validation, true)) throw new RequisitionException(400, validation[0].ErrorMessage!);
        foreach (var item in form.Items)
            if (!Validator.TryValidateObject(item, new(item), validation, true)) throw new RequisitionException(400, validation.Last().ErrorMessage!);
        if (submit && form.Items.Count == 0) throw new RequisitionException(400, "At least one valid item is required before submission.");
        if (submit && form.TargetDate < Today) throw new RequisitionException(400, $"Target date must be {Today:yyyy-MM-dd} or later at submission.");
    }
    private async Task<PurchaseRequisition> LockAsync(Guid id, RequisitionActor actor)
    {
        var request = await database.PurchaseRequisitions.FromSqlInterpolated($"SELECT * FROM dbo.PurchaseRequisitions WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync();
        if (request is null || !actor.CanRead(request)) throw new RequisitionException(404, "Requisition not found.");
        return request;
    }
    private Task<List<PurchaseRequisitionItem>> ItemsAsync(Guid id) => database.PurchaseRequisitionItems.AsNoTracking().Where(x => x.PurchaseRequisitionId == id).OrderBy(x => x.Id).ToListAsync();
    private Task<List<AuditLog>> HistoryAsync(Guid id) => database.AuditLogs.AsNoTracking().Where(x => x.RecordType == "PurchaseRequisition" && x.RecordId == id.ToString()).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync();
    private async Task CheckVersionAsync(PurchaseRequisition request, IEnumerable<PurchaseRequisitionItem> items, string? version)
    {
        if (string.IsNullOrEmpty(version) || !string.Equals(version, Version(request, items, await HistoryAsync(request.Id)), StringComparison.Ordinal))
            throw new RequisitionException(409, "This requisition has changed. Reload it before saving or acting again.");
    }
    private static string CanonicalContent(PurchaseRequisition request, IEnumerable<PurchaseRequisitionItem> items) =>
        JsonSerializer.Serialize(new { request.Title, request.Reason, request.Priority, request.TargetDate,
            Items = items.OrderBy(x => x.Id).Select(x => new { x.Id, x.Description, Quantity = x.Quantity.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture), x.Specifications }) });
    private static string Version(PurchaseRequisition request, IEnumerable<PurchaseRequisitionItem> items, IEnumerable<AuditLog> history) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Request = request, Items = items.OrderBy(x => x.Id), Events = history.Select(x => x.Id).OrderBy(x => x) }))));
    private static object Snapshot(PurchaseRequisition request, IEnumerable<PurchaseRequisitionItem> items) => new
    { request.Title, request.Reason, request.Priority, request.TargetDate, Items = items.OrderBy(x => x.Id).Select(x => new { x.Id, x.Description, x.Quantity, x.Specifications }).ToArray() };
    private void Audit(RequisitionActor actor, PurchaseRequisition request, string action, PurchaseRequestStatus before, PurchaseRequestStatus after, string? reason) =>
        database.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorUserId = actor.UserId, Action = action, RecordType = "PurchaseRequisition", RecordId = request.Id.ToString(), PreviousStatus = before.ToString(), NewStatus = after.ToString(), Reason = reason, OccurredAt = clock.GetUtcNow() });
}
