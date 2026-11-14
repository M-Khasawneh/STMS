using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Explicit memory-only delivery boundary; never registered by the production web project.
internal sealed class MemoryContactMessages : IContactMessageStore
{
    public bool Available = true;
    public bool Fail;
    public List<ContactMessageForm> Messages { get; } = [];
    public Task<bool> DeliverAsync(ContactMessageForm form, DateTimeOffset now, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Fail) throw new DbUpdateException("Injected contact persistence failure");
        if (!Available) return Task.FromResult(false);
        Messages.Add(form);
        return Task.FromResult(true);
    }
}
internal static class PublicContactChecks
{
    public static async Task RunAsync(HttpClient client, IServiceProvider services)
    {
        var checks = 0;
        void Check(bool valid, string name) { if (!valid) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
        var query = new ContactMessageStore(db).Recipients(DateTimeOffset.UtcNow).ToQueryString();
        Check(query.Contains("Admin") && query.Contains("Supplier") && query.Contains("SupplierProfiles") && query.Contains("LockoutEnd") && !query.Contains("EmployeeDepartments"), "Recipient query restricts delivery to active non-supplier Admin accounts without limiting system-wide Admin by department");
        var store = (MemoryContactMessages)services.GetRequiredService<IContactMessageStore>();
        var service = new ContactMessages(store, TimeProvider.System);
        foreach (var invalid in new[] { new ContactMessageForm(), new ContactMessageForm { SenderName = "Name", Email = "bad-address", Subject = "Question", Message = "Text" },
            new ContactMessageForm { SenderName = "Name", Email = "sender@example.test", Subject = "Question", Message = new string('x', 4001) } }) {
            try { await service.SendAsync(invalid); throw new Exception("Expected contact validation"); }
            catch (ValidationException) { Check(store.Messages.Count == 0, "Invalid contact data never reaches storage"); }
        }
        var form = new ContactMessageForm { SenderName = "  Sender  ", Email = "sender@example.test", Subject = "  Question  ", Message = "  A test enquiry.  " };
        Check(await service.SendAsync(form) && store.Messages.Single().SenderName == "Sender" && store.Messages.Single().Message == "A test enquiry.", "Service validates and normalizes before delivery");
        store.Messages.Clear();
        foreach (var route in new[] { "/", "/Home/About", "/Home/Contact", "/Account/Login", "/Home/Privacy" }) {
            var response = await client.GetAsync(route); var html = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == HttpStatusCode.OK && html.Contains("public-topbar") && html.Contains("id=\"home-navigation\"") && html.Contains("public-footer"), "Shared public navbar/footer renders " + route);
        }
        var contact = await client.GetAsync("/Home/Contact");
        var contactHtml = await contact.Content.ReadAsStringAsync();
        Check(contact.Headers.CacheControl?.NoStore == true && contactHtml.Contains("name=\"__RequestVerificationToken\""), "Contact is no-store and includes antiforgery");
        var token = WebUtility.HtmlDecode(Regex.Match(contactHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Dictionary<string, string> Fields() => new() { ["__RequestVerificationToken"] = token, ["SenderName"] = "Visitor", ["Email"] = "visitor@example.test", ["Subject"] = "Fixture enquiry", ["Message"] = "<script>fixture message</script>", ["RecipientUserId"] = "competing-supplier" };
        // Antiforgery failures consume the same public request allowance, so test the three POST outcomes first.
        var invalidFields = Fields(); invalidFields["Email"] = "not-an-email";
        var invalidResponse = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(invalidFields));
        Check(invalidResponse.StatusCode == HttpStatusCode.BadRequest && store.Messages.Count == 0, "Invalid MVC form redisplays errors without storage");
        var submitted = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(Fields()));
        Check(submitted.StatusCode == HttpStatusCode.Redirect && store.Messages.Count == 1, "Anonymous valid contact delivers once and uses PRG");
        var confirmation = await client.GetStringAsync(submitted.Headers.Location!);
        Check(confirmation.Contains("Your message has been sent") && !confirmation.Contains("<script>fixture message</script>"), "Success is shown only after delivery and GET refresh does not resend");
        store.Fail = true;
        var failure = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(Fields()));
        var failedHtml = await failure.Content.ReadAsStringAsync();
        Check(failure.StatusCode == HttpStatusCode.ServiceUnavailable && store.Messages.Count == 1 && failedHtml.Contains("could not be delivered") && !failedHtml.Contains("Your message has been sent"), "Persistence failure shows honest recoverable feedback");
        Check(failedHtml.Contains("&lt;script&gt;fixture message&lt;/script&gt;") && !failedHtml.Contains("<script>fixture message</script>"), "Redisplayed public message is Razor encoded");
        var limited = await client.PostAsync("/Home/Contact", new FormUrlEncodedContent(Fields()));
        Check(limited.StatusCode == HttpStatusCode.TooManyRequests && limited.Headers.RetryAfter is not null && store.Messages.Count == 1, "Rate limit blocks further delivery and returns retry guidance");
        // No recipient is a delivery failure, not a fabricated success.
        store.Fail = false; store.Available = false;
        Check(!await service.SendAsync(form) && store.Messages.Count == 1, "Missing active administrator cannot produce a success receipt");
        var action = typeof(SourcingTenderManagementSystem.Web.Controllers.HomeController).GetMethods().Single(x => x.Name == "Contact" && x.GetParameters().Length > 0);
        Check(action.IsDefined(typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute), true), "Contact POST requires antiforgery independently of UI");
        Console.WriteLine($"PASS: {checks} database-free public contact/query/MVC/security checks; no SQL connection opened.");
    }
}

internal sealed class NoSqlContactFixture : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
{
    public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult ConnectionOpening(System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result) => throw new InvalidOperationException("Memory fixture forbids SQL connections.");
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult> ConnectionOpeningAsync(System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Memory fixture forbids SQL connections.");
}
