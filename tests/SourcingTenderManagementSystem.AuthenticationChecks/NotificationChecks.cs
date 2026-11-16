using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal sealed class MemoryNotifications : INotificationStore
{
    public static readonly Guid MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ForeignId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public List<Notification> Rows { get; } = [
        new() { Id = MessageId, RecipientUserId = "admin", Subject = "Website message: Fixture question", Message = "From: Fixture visitor\nEmail: visitor@example.test\n<script>private contact</script>", CreatedAt = DateTimeOffset.UtcNow },
        new() { Id = Guid.NewGuid(), RecipientUserId = "admin", Subject = "Fixture system notice", Message = "Only your notifications appear here.", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) },
        new() { Id = Guid.NewGuid(), RecipientUserId = "admin", Subject = "Website message: Registration enquiry", Message = "Fixture second administrator message.", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3) },
        new() { Id = ForeignId, RecipientUserId = "supplier", Subject = "Private supplier notice", Message = "Competing supplier private text", CreatedAt = DateTimeOffset.UtcNow }
    ];
    private IEnumerable<Notification> Own(string id, bool messages) => Rows.Where(x => x.RecipientUserId == id && (!messages || x.Subject.StartsWith("Website message: ") && x.TenderId is null && x.BidId is null && x.PurchaseRequisitionId is null));
    public Task<IReadOnlyList<Notification>> ListAsync(string id, bool messages, int skip, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Notification>>(Own(id, messages).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(skip).Take(take).ToArray());
    public Task<long> CountAsync(string id, bool messages, bool unread, CancellationToken ct) => Task.FromResult(Own(id, messages).LongCount(x => !unread || x.ReadAt is null));
    public Task<Notification?> FindAsync(string id, Guid record, bool messages, CancellationToken ct) => Task.FromResult(Own(id, messages).SingleOrDefault(x => x.Id == record));
    public Task<bool> MarkReadAsync(string id, Guid record, DateTimeOffset now, CancellationToken ct) {
        lock (Rows) { var row = Own(id, false).SingleOrDefault(x => x.Id == record); if (row is null) return Task.FromResult(false); row.ReadAt ??= now; return Task.FromResult(true); }
    }
    public Task<bool> IsMessageAdminAsync(string id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(id == "admin");
}
internal static class NotificationChecks
{
    public static async Task RunAsync(HttpClient client, IServiceProvider services)
    {
        var count = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; Console.WriteLine("PASS: " + label); }
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var query = new NotificationStore(db).Own("recipient-only", true).ToQueryString();
        Check(query.Contains("recipient-only") && query.Contains("Website message:") && query.Contains("TenderId") && query.Contains("BidId") && query.Contains("PurchaseRequisitionId"), "SQL message query includes recipient and Contact-only constraints");
        var rows = (MemoryNotifications)services.GetRequiredService<INotificationStore>();
        void Actor(string actor) { client.DefaultRequestHeaders.Remove("X-Bid-Test-Actor"); client.DefaultRequestHeaders.Add("X-Bid-Test-Actor", actor); }
        foreach (var actor in new[] { "anonymous", "supplier", "requester", "officer", "manager", "evaluator", "top" }) {
            Actor(actor); var response = await client.GetAsync("/Messages");
            Check(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "Messages restricted to Admin: " + actor);
        }
        Actor("admin");
        var html = await client.GetStringAsync("/Messages");
        Check(html.Contains("Fixture question") && !html.Contains("Competing supplier private text") && !html.Contains("Fixture system notice</th>"), "Admin message list excludes foreign accounts and system notices");
        Check(html.Contains("notification-dropdown") && html.Contains("<summary") && html.Contains("Loading your notifications"), "Bell renders a native dropdown without blocking page rendering");
        var menuResponse = await client.GetAsync("/Notifications/Menu");
        var menuHtml = await menuResponse.Content.ReadAsStringAsync();
        Check(menuResponse.StatusCode == HttpStatusCode.OK && menuResponse.Headers.Contains("X-STMS-Notification-Menu") && menuHtml.Contains("Mark as read") && menuHtml.Contains("notification-menu-list"), "Authorized menu endpoint returns stacked read actions");
        Check(!menuHtml.Contains("<script>private contact</script>") && menuHtml.Contains("&lt;script&gt;private contact&lt;/script&gt;"), "Notification menu encodes message text");
        Check((await client.GetAsync("/Messages/Details/" + MemoryNotifications.MessageId)).StatusCode == HttpStatusCode.OK && rows.Rows.Single(x => x.Id == MemoryNotifications.MessageId).ReadAt is null, "GET message details never changes read state");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        foreach (var action in new[] { "/Notifications/Read/", "/Messages/Open/" }) {
            Check((await client.PostAsync(action + MemoryNotifications.MessageId, new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "Read/open POST requires antiforgery");
        }
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["returnUrl"] = "https://example.test/redirect" };
        Check((await client.PostAsync("/Notifications/Read/" + MemoryNotifications.ForeignId, new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.NotFound, "Cannot mark another recipient's notification read");
        Check((await client.PostAsync("/Messages/Open/" + MemoryNotifications.ForeignId, new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.NotFound, "Cannot open a foreign supplier record through Messages");
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var read = await client.PostAsync("/Notifications/Read/" + MemoryNotifications.MessageId, new FormUrlEncodedContent(fields));
        var partial = await read.Content.ReadAsStringAsync();
        Check(read.StatusCode == HttpStatusCode.OK && read.Headers.Contains("X-STMS-Notification-Menu") && partial.Contains("data-unread=\"2\""), "Mark as read returns an updated HTML partial and actual unread count");
        var firstRead = rows.Rows.Single(x => x.Id == MemoryNotifications.MessageId).ReadAt;
        await client.PostAsync("/Notifications/Read/" + MemoryNotifications.MessageId, new FormUrlEncodedContent(fields));
        Check(firstRead.HasValue && rows.Rows.Single(x => x.Id == MemoryNotifications.MessageId).ReadAt == firstRead, "Repeated read preserves original timestamp");
        client.DefaultRequestHeaders.Remove("X-Requested-With");
        var redirect = await client.PostAsync("/Notifications/Read/" + MemoryNotifications.MessageId, new FormUrlEncodedContent(fields));
        Check(redirect.StatusCode == HttpStatusCode.Redirect && !redirect.Headers.Location!.IsAbsoluteUri, "No-JavaScript fallback rejects external return URLs");
        Check(rows.Rows.Single(x => x.Id == MemoryNotifications.ForeignId).ReadAt is null, "Other recipient's record remains untouched");
        using var scope = services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<NotificationInbox>();
        var staleAdmin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "no-longer-admin"), new Claim(ClaimTypes.Role, "Admin")], "Fixture"));
        try { await inbox.PageAsync(staleAdmin, 1, true, default); throw new Exception("Stale Admin accepted"); } catch (UnauthorizedAccessException) { Check(true, "Message service independently verifies actual Admin eligibility"); }
        Console.WriteLine($"PASS: {count} database-free message/notification ownership, read-state, MVC and antiforgery checks.");
    }
}
