using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IContactMessageStore
{
    Task<bool> DeliverAsync(ContactMessageForm form, DateTimeOffset now, CancellationToken ct);
}
public sealed class ContactMessages(IContactMessageStore store, TimeProvider clock)
{
    public Task<bool> SendAsync(ContactMessageForm form, CancellationToken ct = default)
    {
        Validator.ValidateObject(form, new ValidationContext(form), true);
        return store.DeliverAsync(new ContactMessageForm {
            SenderName = form.SenderName.Trim(), Email = form.Email.Trim(), Subject = form.Subject.Trim(), Message = form.Message.Trim()
        }, clock.GetUtcNow(), ct);
    }
}
public sealed class ContactMessageStore(ApplicationDbContext db) : IContactMessageStore
{
    public IQueryable<string> Recipients(DateTimeOffset now) => db.Users.AsNoTracking()
        .Where(u => (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == ApplicationRoles.Admin)) &&
            !db.SupplierProfiles.Any(s => s.UserId == u.Id) &&
            !db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == ApplicationRoles.Supplier)))
        .Select(u => u.Id);

    public async Task<bool> DeliverAsync(ContactMessageForm form, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = await Recipients(now).ToListAsync(ct);
        if (recipients.Count == 0) return false;
        foreach (var id in recipients) db.Notifications.Add(new Notification {
            Id = Guid.NewGuid(), RecipientUserId = id, CreatedAt = now,
            Subject = "Website message: " + form.Subject,
            Message = "Public website enquiry\nFrom: " + form.SenderName + "\nEmail: " + form.Email + "\n\n" + form.Message
        });
        // EF's SaveChanges transaction persists all administrator copies together.
        await db.SaveChangesAsync(ct);
        return true;
    }
}
