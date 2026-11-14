using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SourcingTenderManagementSystem.Web.Data;

internal static class ClarificationSchemaChecks
{
    private const string Previous = "20261113180000_TenderInvitationIntegrity";
    private const string Target = "20261114120000_ClarificationAttachments";
    public static async Task<int> RunAsync(bool apply)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
        await using var db = new ApplicationDbContext(options);
        // Read-only preflight. No EnsureCreated, seed operations or migrations run before these checks.
        if (!await db.Database.CanConnectAsync()) throw new InvalidOperationException("Configured database is unavailable. No changes applied.");
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        Console.WriteLine("Applied migrations: " + string.Join(", ", applied));
        Console.WriteLine("Pending migrations: " + string.Join(", ", pending));
        if (applied.LastOrDefault() != Previous || !pending.SequenceEqual(new[] { Target }))
            throw new InvalidOperationException("Migration state differs from the approved single-table change. Stopped.");
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model and snapshot differ. Stopped.");
        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name='ClarificationAttachments'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Attachment table already exists without migration history. Stopped.");
        command.CommandText = """
            SELECT COUNT(*) FROM dbo.ClarificationResponses r LEFT JOIN dbo.ClarificationRequests q ON r.ClarificationRequestId=q.Id WHERE q.Id IS NULL;
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Existing orphan clarification responses found. Stopped.");
        var before = await Counts(db);
        Console.WriteLine("Existing row counts: " + string.Join(", ", before.Select(x => $"{x.Key}={x.Value}")));
        Console.WriteLine($"Clarification states: Pending={await db.ClarificationRequests.CountAsync(x => x.Status == SourcingTenderManagementSystem.Web.Models.Entities.ClarificationStatus.Pending)}, Answered={await db.ClarificationRequests.CountAsync(x => x.Status == SourcingTenderManagementSystem.Web.Models.Entities.ClarificationStatus.Answered)}, Resolved={await db.ClarificationRequests.CountAsync(x => x.Status == SourcingTenderManagementSystem.Web.Models.Entities.ClarificationStatus.Resolved)}");
        var script = db.GetService<IMigrator>().GenerateScript(Previous, Target);
        if (script.Contains("DROP ", StringComparison.OrdinalIgnoreCase) || script.Contains("DELETE ", StringComparison.OrdinalIgnoreCase) || script.Contains("UPDATE [", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected destructive migration SQL. Stopped.");
        Console.WriteLine("Preflight passed. Forward migration creates only ClarificationAttachments and its index/constraints.");
        if (!apply) return 0;
        await db.GetService<IMigrator>().MigrateAsync(Target);
        var after = await Counts(db);
        foreach (var entry in before)
            if (after[entry.Key] != entry.Value) throw new InvalidOperationException($"Row count changed for {entry.Key}; investigate concurrent activity.");
        if (await db.ClarificationAttachments.CountAsync() != 0) throw new InvalidOperationException("Unexpected attachment records after migration.");
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Migrations remain pending.");
        Console.WriteLine("PASS: approved migration applied; existing row counts preserved; new attachment table empty; no pending migrations.");
        return 0;
    }
    private static async Task<Dictionary<string, long>> Counts(ApplicationDbContext db)
    {
        var result = new Dictionary<string, long>();
        // Fixed identifiers only, and aggregate counts avoid exposing personal records.
        foreach (var table in new[] { "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "Departments", "EmployeeDepartments", "SupplierProfiles", "SupplierDocuments", "PurchaseRequisitions", "Tenders", "TenderSuppliers", "Invitations", "Bids", "BidItems", "BidDocuments", "BidCostComponents", "ComplianceReviews", "ClarificationRequests", "ClarificationResponses", "Notifications", "AuditLogs" })
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{table}]";
            result[table] = Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        return result;
    }
}
