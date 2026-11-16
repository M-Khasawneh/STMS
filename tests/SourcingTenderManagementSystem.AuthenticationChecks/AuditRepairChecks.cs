using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SourcingTenderManagementSystem.Web.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models;

namespace SourcingTenderManagementSystem.AuthenticationChecks;
internal static class AuditRepairChecks
{
    internal static async Task Run()
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
        var files = new MemoryFiles();
        await SupplierDocumentRecovery.ReconcileAsync(files, (_, _) => Task.FromResult(true), default);
        Check(files.Deleted.Count == 0 && files.Completed.Contains("pending"), "Uncertain successful commit retains referenced bytes");
        files = new();
        await SupplierDocumentRecovery.ReconcileAsync(files, (_, _) => Task.FromResult(false), default);
        Check(files.Deleted.Contains("pending") && files.Completed.Contains("pending"), "Rolled-back bytes and intent are retired");
        files = new();
        try { await SupplierDocumentRecovery.ReconcileAsync(files, (_, _) => throw new IOException("offline"), default); throw new Exception("Expected lookup failure"); }
        catch (IOException) { Check(files.Deleted.Count == 0 && files.Completed.Count == 0, "Unknown commit outcome never deletes bytes or intent"); }
        await SupplierDocumentRecovery.ReconcileAsync(files, (_, _) => Task.FromResult(true), default);
        Check(files.Completed.Count == 1 && files.Deleted.Count == 0, "Recovery retries after SQL returns");
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var exception = new ExceptionContext(action, []) { Exception = new DbUpdateConcurrencyException("synthetic") };
        new DatabaseFailureFilter(NullLogger<DatabaseFailureFilter>.Instance).OnException(exception);
        Check(exception.ExceptionHandled && exception.Result is ViewResult { StatusCode: 409 }, "Concurrency failures produce a safe conflict view");
        exception = new ExceptionContext(action, []) { Exception = new InvalidOperationException("unrelated") };
        new DatabaseFailureFilter(NullLogger<DatabaseFailureFilter>.Instance).OnException(exception);
        Check(!exception.ExceptionHandled, "Unrelated failures are not hidden");
        var page = new PageInfo(2, 25, 51, "retained", "Draft");
        Check(page.Pages == 3 && new PageInfo(1,25,0,null,null).Pages == 1, "Paging metadata handles boundaries");
        await RateLimit(Check);
        Console.WriteLine($"PASS: {checks} database-free audit repair recovery/concurrency checks.");
    }
    private static async Task RateLimit(Action<bool,string> check)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Services.AddPublicContact();
        await using var app = builder.Build(); app.UseRouting(); app.UseRateLimiter();
        app.MapPost("/registration-test", () => Results.Ok()).RequireRateLimiting("supplier-registration");
        await app.StartAsync();
        try {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            for (var i=0; i<3; i++) { using var response = await client.PostAsync("/registration-test", null); check(response.IsSuccessStatusCode, "Registration policy allows a bounded attempt"); }
            using var limited = await client.PostAsync("/registration-test", null);
            check((int)limited.StatusCode == 429 && limited.Headers.RetryAfter is not null, "Registration policy rejects excess requests with Retry-After");
            var action = typeof(SourcingTenderManagementSystem.Web.Controllers.SuppliersController).GetMethods().Single(m => m.Name == "Register" && m.GetParameters().Length == 2);
            check(action.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>().Single().PolicyName == "supplier-registration", "Supplier MVC POST uses the tested limiter");
        } finally { await app.StopAsync(); }
    }
    private sealed class MemoryFiles : IPrivateSupplierFiles
    {
        public HashSet<string> Deleted { get; } = [];
        public HashSet<string> Completed { get; } = [];
        public Task<IReadOnlyList<string>> PendingAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(["pending"]);
        public void Complete(string reference) => Completed.Add(reference);
        public void Delete(string reference) => Deleted.Add(reference);
        public Task<string> SaveAsync(ValidatedSupplierFile file, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> OpenAsync(string reference, CancellationToken ct) => throw new NotSupportedException();
    }
}
