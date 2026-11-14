using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Separate opt-in command. The entire test rolls back; no enquiry reaches a live administrator.
internal static class PublicContactSqlChecks
{
    public static async Task RunAsync(bool inboxChecks = false)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        var target = new SqlConnectionStringBuilder(connection);
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Unapproved SQL target.");
        var marker = "contact-check-" + Guid.NewGuid().ToString("N");
        var failure = new ContactFailure(marker);
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).AddInterceptors(failure).Options);
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Pending migrations: stopped before writes.");
        var count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); count++; Console.WriteLine("PASS: " + label); }
        async Task<string> Fingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Users = await db.Users.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            Roles = await db.UserRoles.AsNoTracking().OrderBy(x => x.UserId).ThenBy(x => x.RoleId).ToArrayAsync(),
            Notifications = await db.Notifications.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()
        })));
        var baseline = await Fingerprint();
        var adminRole = await db.Roles.AsNoTracking().SingleAsync(x => x.Name == "Admin");
        var supplierRole = await db.Roles.AsNoTracking().SingleAsync(x => x.Name == "Supplier");
        Console.WriteLine("Read-only SQL preflight passed; existing Identity roles and notification schema reused.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var admin = new IdentityUser(marker + "-admin") { Id = marker + "-admin", SecurityStamp = Guid.NewGuid().ToString() };
        var mixed = new IdentityUser(marker + "-supplier") { Id = marker + "-supplier", SecurityStamp = Guid.NewGuid().ToString() };
        var locked = new IdentityUser(marker + "-locked") { Id = marker + "-locked", LockoutEnabled = true, LockoutEnd = now.AddHours(1), SecurityStamp = Guid.NewGuid().ToString() };
        try {
            db.Users.AddRange(admin, mixed, locked);
            db.UserRoles.AddRange(new IdentityUserRole<string> { UserId = admin.Id, RoleId = adminRole.Id },
                new IdentityUserRole<string> { UserId = mixed.Id, RoleId = adminRole.Id }, new IdentityUserRole<string> { UserId = mixed.Id, RoleId = supplierRole.Id },
                new IdentityUserRole<string> { UserId = locked.Id, RoleId = adminRole.Id });
            await db.SaveChangesAsync();
            var store = new ContactMessageStore(db);
            var recipients = await store.Recipients(now).ToArrayAsync();
            Check(recipients.Contains(admin.Id) && !recipients.Contains(mixed.Id) && !recipients.Contains(locked.Id), "Actual SQL query excludes Supplier/mixed and locked accounts");
            var service = new ContactMessages(store, TimeProvider.System);
            var form = new ContactMessageForm { SenderName = "Fixture visitor", Email = "visitor@example.test", Subject = marker, Message = "<script>fixture enquiry</script>\nSecond line." };
            Check(await service.SendAsync(form), "Real contact service saves through SQL within rollback transaction");
            var rows = await db.Notifications.AsNoTracking().Where(x => x.Subject == "Website message: " + marker).ToArrayAsync();
            Check(rows.Length == recipients.Length && rows.Select(x => x.RecipientUserId).Order().SequenceEqual(recipients.Order()), "One private notification is persisted per eligible administrator");
            Check(rows.All(x => x.ReadAt == null && x.BidId == null && x.TenderId == null && x.PurchaseRequisitionId == null && x.CreatedAt.Offset == TimeSpan.Zero), "Messages are unread UTC notifications without procurement links");
            var own = rows.Single(x => x.RecipientUserId == admin.Id);
            Check(own.Message.Contains("Fixture visitor") && own.Message.Contains("visitor@example.test") && own.Message.Contains(form.Message), "Sender details and complete message persist as plain text");
            async Task<IReadOnlyList<Notification>> Inbox(string id) {
                var controller = new NotificationsController(new NotificationInbox(new NotificationStore(db), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationInbox>.Instance)) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "SqlTest")) } } };
                return ((NotificationPage)((ViewResult)await controller.Index()).Model!).Items;
            }
            Check((await Inbox(admin.Id)).Any(x => x.Id == own.Id) && (await Inbox(mixed.Id)).All(x => x.Subject != own.Subject), "Existing notification inbox query isolates the administrator's message from supplier account");
            if (inboxChecks) {
                var inbox = new NotificationInbox(store: new NotificationStore(db), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationInbox>.Instance);
                ClaimsPrincipal Principal(string id, string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "SqlTest"));
                var adminPrincipal = Principal(admin.Id, "Admin"); var supplierPrincipal = Principal(mixed.Id, "Supplier");
                var beforeMenu = await inbox.MenuAsync(adminPrincipal);
                Check(beforeMenu.Unread == 1 && beforeMenu.Items.Single().Id == own.Id, "SQL bell menu contains only the administrator's own unread message");
                var page = await inbox.PageAsync(adminPrincipal, 1, true, default);
                Check(page.Total == 1 && page.Unread == 1 && page.Items.Single().Id == own.Id, "SQL Admin message list and count match Contact records");
                Check(await new NotificationStore(db).Own(admin.Id, true).LongCountAsync() == page.Total, "Dashboard message aggregate uses the same recipient-scoped SQL data");
                Check(!await inbox.MarkReadAsync(supplierPrincipal, own.Id, default), "Supplier cannot change administrator read state in SQL");
                Check(await inbox.MessageAsync(adminPrincipal, own.Id, default) is { ReadAt: null }, "Reading message details does not modify SQL on GET");
                Check(await inbox.MarkReadAsync(adminPrincipal, own.Id, default), "Mark as read persists through actual SQL update");
                var readAt = (await inbox.MessageAsync(adminPrincipal, own.Id, default))!.ReadAt;
                Check(readAt.HasValue && (await inbox.MenuAsync(adminPrincipal)).Unread == 0, "SQL read timestamp updates the real unread count");
                await inbox.MarkReadAsync(adminPrincipal, own.Id, default);
                Check((await inbox.MessageAsync(adminPrincipal, own.Id, default))!.ReadAt == readAt, "Repeated SQL read preserves the original timestamp");
                try { await inbox.PageAsync(Principal(mixed.Id, "Admin"), 1, true, default); throw new Exception("Mixed-role Admin accepted"); }
                catch (UnauthorizedAccessException) { Check(true, "Actual SQL roles exclude a stale or mixed Supplier/Admin from message management"); }
            }
            failure.Enabled = true;
            try { await service.SendAsync(form); throw new Exception("Expected injected save failure"); }
            catch (DbUpdateException) { Check(true, "Persistence failure propagates rather than claiming delivery"); }
            failure.Enabled = false; db.ChangeTracker.Clear();
            Check(await db.Notifications.CountAsync(x => x.Subject == "Website message: " + marker) == rows.Length, "Failed save creates no extra administrator notifications");
        }
        finally {
            await tx.RollbackAsync(); db.ChangeTracker.Clear();
            Check(await Fingerprint() == baseline, "Rollback removed all temporary users/roles/messages; existing records match baseline");
        }
        Console.WriteLine($"PASS: {count} approved SQL contact persistence/isolation/rollback checks; no committed test data.");
    }
    private sealed class ContactFailure(string marker) : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<Notification>().Any(x => x.State == EntityState.Added && x.Entity.Subject == "Website message: " + marker)) throw new DbUpdateException("Injected contact save failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
