using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface INotificationStore
{
    Task<bool?> LiveEligibleAsync(string userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult<bool?>(null);
    Task<IReadOnlyList<Notification>> ListAsync(string userId, bool messagesOnly, int skip, int take, CancellationToken ct);
    Task<long> CountAsync(string userId, bool messagesOnly, bool unreadOnly, CancellationToken ct);
    Task<Notification?> FindAsync(string userId, Guid id, bool messagesOnly, CancellationToken ct);
    Task<bool> MarkReadAsync(string userId, Guid id, DateTimeOffset now, CancellationToken ct);
    Task<bool> IsMessageAdminAsync(string userId, DateTimeOffset now, CancellationToken ct);
}
public sealed class NotificationStore(ApplicationDbContext db) : INotificationStore
{
    public async Task<bool?> LiveEligibleAsync(string userId, DateTimeOffset now, CancellationToken ct) =>
        await EmployeeEligibility.Eligible(db, now).AnyAsync(u => u.Id == userId, ct);
    public IQueryable<Notification> Own(string userId, bool messagesOnly) => db.Notifications.AsNoTracking().Where(x => x.RecipientUserId == userId &&
        (!messagesOnly || x.Subject.StartsWith("Website message: ") && x.TenderId == null && x.BidId == null && x.PurchaseRequisitionId == null));
    public async Task<IReadOnlyList<Notification>> ListAsync(string userId, bool messagesOnly, int skip, int take, CancellationToken ct) =>
        await Own(userId, messagesOnly).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public Task<long> CountAsync(string userId, bool messagesOnly, bool unreadOnly, CancellationToken ct) =>
        Own(userId, messagesOnly).LongCountAsync(x => !unreadOnly || x.ReadAt == null, ct);
    public Task<Notification?> FindAsync(string userId, Guid id, bool messagesOnly, CancellationToken ct) => Own(userId, messagesOnly).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<bool> MarkReadAsync(string userId, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var changed = await db.Notifications.Where(x => x.Id == id && x.RecipientUserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, now), ct);
        return changed > 0 || await Own(userId, false).AnyAsync(x => x.Id == id, ct);
    }
    public Task<bool> IsMessageAdminAsync(string userId, DateTimeOffset now, CancellationToken ct) => new ContactMessageStore(db).Recipients(now).AnyAsync(x => x == userId, ct);
}
public sealed class NotificationInbox(INotificationStore store, TimeProvider clock, ILogger<NotificationInbox> logger)
{
    public Task<bool?> LiveEligibleAsync(ClaimsPrincipal user, CancellationToken ct) => store.LiveEligibleAsync(UserId(user), clock.GetUtcNow(), ct);
    private static string UserId(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id
        ? id : throw new UnauthorizedAccessException();
    private async Task<string> MessageAdmin(ClaimsPrincipal user, CancellationToken ct)
    {
        var id = UserId(user);
        if (!user.IsInRole(ApplicationRoles.Admin) || user.IsInRole(ApplicationRoles.Supplier) || !await store.IsMessageAdminAsync(id, clock.GetUtcNow(), ct)) throw new UnauthorizedAccessException();
        return id;
    }
    public async Task<NotificationMenu> MenuAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var id = UserId(user);
        try { return new(await store.ListAsync(id, false, 0, 8, ct), await store.CountAsync(id, false, true, ct), true); }
        catch (Exception e) when (e is System.Data.Common.DbException or TimeoutException) { logger.LogWarning("Notification menu temporarily unavailable."); return new([], null, false); }
    }
    public async Task<NotificationPage> PageAsync(ClaimsPrincipal user, int page, bool messagesOnly, CancellationToken ct)
    {
        var id = messagesOnly ? await MessageAdmin(user, ct) : UserId(user);
        page = Math.Clamp(page, 1, 1000000);
        return new(await store.ListAsync(id, messagesOnly, (page - 1) * 25, 25, ct), page,
            await store.CountAsync(id, messagesOnly, false, ct), await store.CountAsync(id, messagesOnly, true, ct), messagesOnly);
    }
    public async Task<Notification?> MessageAsync(ClaimsPrincipal user, Guid id, CancellationToken ct) =>
        await store.FindAsync(await MessageAdmin(user, ct), id, true, ct);
    public Task<bool> MarkReadAsync(ClaimsPrincipal user, Guid id, CancellationToken ct) => store.MarkReadAsync(UserId(user), id, clock.GetUtcNow(), ct);
}
