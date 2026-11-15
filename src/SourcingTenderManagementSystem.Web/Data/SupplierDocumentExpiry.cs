using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public static class SupplierExpiryRules
{
    public const int UpcomingDays = 30;
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Asia/Amman").DateTime);
    public static string Label(DateOnly? expires, DateOnly today) => expires is null ? "Expiry not recorded" : expires < today ? "Expired" : expires == today ? "Expires today" : expires <= today.AddDays(UpcomingDays) ? "Expires within 30 days" : "Expiry date recorded";
    public static string Version(SupplierDocument document) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{document.SupplierProfileId:D}|{document.Id:D}|{document.ExpiresOn:yyyy-MM-dd}")));
}

public interface ISupplierExpiryStore
{
    Task<IBidTransaction> BeginAsync(Guid supplierId, CancellationToken ct);
    Task<SupplierDocument?> DocumentAsync(Guid supplierId, Guid documentId, CancellationToken ct);
    Task SaveAsync(SupplierDocument document, AuditLog audit, CancellationToken ct);
}

public sealed class SupplierExpiryStore(ApplicationDbContext db) : ISupplierExpiryStore
{
    public async Task<IBidTransaction> BeginAsync(Guid supplierId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Same profile lock as supplier approval/suspension; metadata updates cannot race those operations.
            BidRules.Require(await db.SupplierProfiles.FromSqlInterpolated($"SELECT * FROM dbo.SupplierProfiles WITH (UPDLOCK,HOLDLOCK) WHERE Id={supplierId}").AnyAsync(ct), 404, "Supplier not found.");
            return new Transaction(transaction, db);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    public Task<SupplierDocument?> DocumentAsync(Guid supplierId, Guid documentId, CancellationToken ct) =>
        db.SupplierDocuments.SingleOrDefaultAsync(d => d.SupplierProfileId == supplierId && d.Id == documentId, ct);
    public async Task SaveAsync(SupplierDocument document, AuditLog audit, CancellationToken ct)
    {
        db.Entry(document).Property(d => d.ExpiresOn).IsModified = true;
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);
    }
    private sealed class Transaction(IDbContextTransaction transaction, ApplicationDbContext db) : IBidTransaction
    {
        private bool committed;
        public async Task CommitAsync(CancellationToken ct) { await transaction.CommitAsync(ct); committed = true; }
        public async ValueTask DisposeAsync() { await transaction.DisposeAsync(); if (!committed) db.ChangeTracker.Clear(); }
    }
}

public sealed class SupplierDocumentExpiry(ISupplierExpiryStore store, IBidData accounts, TimeProvider clock)
{
    private async Task<string> AdminAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var account = await accounts.AccountAsync(user, clock.GetUtcNow(), ct);
        BidRules.Require(account is { Enabled: true, EmployeeDepartment: true, SupplierId: null } && account.Roles.Contains(ApplicationRoles.Admin) && !account.Roles.Contains(ApplicationRoles.Supplier), 403, "Only an active administrator may record official document expiry dates.");
        return account.UserId;
    }
    public async Task SaveAsync(ClaimsPrincipal user, Guid supplierId, SupplierExpiryForm form, CancellationToken ct)
    {
        var actor = await AdminAsync(user, ct);
        BidRules.Require(supplierId != Guid.Empty && form.DocumentId != Guid.Empty && form.Version?.Length == 64, 400, "Reload the document before recording its expiry date.");
        await using var transaction = await store.BeginAsync(supplierId, ct);
        await AdminAsync(user, ct);
        var document = await store.DocumentAsync(supplierId, form.DocumentId, ct);
        BidRules.Require(document is not null, 404, "Document not found for this supplier.");
        BidRules.Require(string.Equals(form.Version, SupplierExpiryRules.Version(document), StringComparison.Ordinal), 409, "This expiry date has changed. Reload the application before updating it.");
        if (document.ExpiresOn == form.ExpiresOn) { await transaction.CommitAsync(ct); return; }
        var previous = document.ExpiresOn;
        document.ExpiresOn = form.ExpiresOn;
        await store.SaveAsync(document, new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actor,
            Action = "SupplierDocumentExpiryUpdated",
            RecordType = "SupplierDocument",
            RecordId = document.Id.ToString(),
            Reason = JsonSerializer.Serialize(new { PreviousExpiresOn = previous, ExpiresOn = document.ExpiresOn }),
            OccurredAt = clock.GetUtcNow()
        }, ct);
        await AdminAsync(user, ct);
        await transaction.CommitAsync(ct);
    }
}
