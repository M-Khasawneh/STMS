using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Explicit opt-in only; committed temporary fixtures and exact-ID cleanup require approval.
internal static class SupplierExpirySqlChecks
{
    public static async Task Run()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var target = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Unexpected SQL test target.");
        target.ConnectTimeout = 5;
        var services = new ServiceCollection(); services.AddLogging(); services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(target.ConnectionString));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<TimeProvider>(TimeProvider.System); services.AddScoped<IBidData, BidData>(); services.AddScoped<SupplierExpiryStore>();
        var faults = new Faults(); services.AddSingleton(faults);
        services.AddScoped<ISupplierExpiryStore>(s => new Store(s.GetRequiredService<SupplierExpiryStore>(), faults)); services.AddScoped<SupplierDocumentExpiry>();
        await using var provider = services.BuildServiceProvider();
        var marker = "expiry-sql-" + Guid.NewGuid().ToString("N");
        var users = Enumerable.Range(0, 4).Select(i => new IdentityUser(marker + "-" + i) { Id = Guid.NewGuid().ToString() }).ToArray();
        var department = new Department { Id = Guid.NewGuid(), Name = marker };
        var suppliers = Enumerable.Range(0, 2).Select(i => new SupplierProfile { Id = Guid.NewGuid(), UserId = users[i + 2].Id, CompanyName = marker + "-" + i,
            RegistrationNumber = marker + "R" + i, TaxNumber = marker + "T" + i, ContactName = "Temporary expiry check", ContactEmail = "expiry@example.invalid", ContactPhone = "test-only", Status = SupplierStatus.Approved }).ToArray();
        var documents = suppliers.Select(s => new SupplierDocument { Id = Guid.NewGuid(), SupplierProfileId = s.Id, DocumentType = "Business / Trade License", OriginalName = "synthetic-no-file.pdf",
            StorageReference = marker + "/no-file", ContentType = "application/pdf", Size = 1, UploadedAt = DateTimeOffset.UtcNow }).ToArray();
        int checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
        ClaimsPrincipal Actor(int i) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, users[i].Id)], "ApprovedSqlExpiryCheck"));
        Dictionary<string, (long Count, string Hash)> baseline;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Schema is current; no migration applied");
            foreach (var role in new[] { ApplicationRoles.Admin, ApplicationRoles.RequestingDepartment, ApplicationRoles.Supplier }) Check(await db.Roles.AnyAsync(r => r.Name == role), "Existing role available: " + role);
            baseline = await Snapshot(target.ConnectionString); Check(baseline.Count >= 37, "All existing dbo table contents fingerprinted before testing");
        }
        try
        {
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                db.Departments.Add(department); await db.SaveChangesAsync();
                for (var i = 0; i < users.Length; i++)
                {
                    Check((await manager.CreateAsync(users[i], password)).Succeeded, "Temporary Identity fixture created");
                    Check((await manager.AddToRoleAsync(users[i], i == 0 ? ApplicationRoles.Admin : i == 1 ? ApplicationRoles.RequestingDepartment : ApplicationRoles.Supplier)).Succeeded, "Temporary fixture uses an existing role");
                    if (i < 2) db.EmployeeDepartments.Add(new EmployeeDepartment { UserId = users[i].Id, DepartmentId = department.Id });
                }
                db.SupplierProfiles.AddRange(suppliers); db.SupplierDocuments.AddRange(documents); await db.SaveChangesAsync();
            }
            async Task<SupplierDocument> Read() { using var scope = provider.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SupplierDocuments.AsNoTracking().SingleAsync(d => d.Id == documents[0].Id); }
            async Task<int> Audits() { using var scope = provider.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs.CountAsync(a => a.RecordType == "SupplierDocument" && a.RecordId == documents[0].Id.ToString()); }
            async Task Save(SupplierExpiryForm form, int actor = 0) { using var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<SupplierDocumentExpiry>().SaveAsync(Actor(actor), suppliers[0].Id, form, default); }
            async Task Deny(Func<Task> action, int status) { try { await action(); throw new Exception("Expected rejection"); } catch (BidException e) { Check(e.Status == status, "SQL-backed authorization/state rejection " + status); } }
            async Task<SupplierExpiryForm> Form(DateOnly? date) { var doc = await Read(); return new() { DocumentId = doc.Id, Version = SupplierExpiryRules.Version(doc), ExpiresOn = date }; }
            var today = SupplierExpiryRules.Today(DateTimeOffset.UtcNow); var initial = await Form(today.AddDays(30));
            await Deny(() => Save(initial, 1), 403); await Deny(() => Save(initial, 2), 403); await Deny(() => Save(initial, 3), 403);
            await Deny(() => Save(new() { DocumentId = documents[1].Id, Version = SupplierExpiryRules.Version(documents[1]), ExpiresOn = today }), 404);
            await Save(initial); var saved = await Read();
            Check(saved.ExpiresOn == today.AddDays(30), "Committed date survives a fresh SQL context reload");
            Check(saved.StorageReference == documents[0].StorageReference && saved.OriginalName == documents[0].OriginalName, "Original-file metadata unchanged");
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.RecordType == "SupplierDocument" && a.RecordId == saved.Id.ToString());
                Check(audit.Action == "SupplierDocumentExpiryUpdated" && audit.ActorUserId == users[0].Id && audit.OccurredAt.Offset == TimeSpan.Zero && audit.Reason!.Contains(today.AddDays(30).ToString("yyyy-MM-dd")), "SQL audit contains actor, UTC and new date");
                Check(await db.SupplierProfiles.Where(p => p.Id == suppliers[0].Id).Select(p => p.Status).SingleAsync() == SupplierStatus.Approved, "Supplier approval unchanged");
            }
            await Deny(() => Save(initial), 409);
            var count = await Audits(); await Save(await Form(saved.ExpiresOn)); Check(await Audits() == count, "No-op adds no audit");
            faults.Fail = true;
            try { await Save(await Form(today.AddDays(8))); throw new Exception("Expected rollback"); } catch (IOException) { Check((await Read()).ExpiresOn == saved.ExpiresOn && await Audits() == count, "Failure after real SaveChanges rolls date and audit back together"); } finally { faults.Fail = false; }
            var first = await Form(today.AddDays(10)); var second = await Form(today.AddDays(11));
            async Task<int> Race(SupplierExpiryForm f) { try { await Save(f); return 200; } catch (BidException e) { return e.Status; } }
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var races = new[] { first, second }.Select(f => Task.Run(async () => { await gate.Task; return await Race(f); })).ToArray(); gate.SetResult(); var outcomes = await Task.WhenAll(races);
            Check(outcomes.Count(x => x == 200) == 1 && outcomes.Count(x => x == 409) == 1 && await Audits() == count + 1, "Concurrent SQL transactions save once and reject stale overwrite");
            await Save(await Form(null)); Check((await Read()).ExpiresOn is null, "Clearing date persists unknown");
            using (var scope = provider.CreateScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(); var admin = (await manager.FindByIdAsync(users[0].Id))!;
                Check((await manager.RemoveFromRoleAsync(admin, ApplicationRoles.Admin)).Succeeded, "Temporary Admin role revoked");
            }
            await Deny(async () => await Save(await Form(today)), 403); Check((await Read()).ExpiresOn is null, "Stale principal cannot bypass actual SQL role revocation");
        }
        finally
        {
            // Exact generated IDs only; never delete by broad name/status or existing account IDs.
            using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); await using var tx = await db.Database.BeginTransactionAsync();
            var ids = users.Select(u => u.Id).ToArray(); var profiles = suppliers.Select(s => s.Id).ToArray(); var docs = documents.Select(d => d.Id).ToArray(); var records = docs.Select(id => id.ToString()).ToArray();
            db.AuditLogs.RemoveRange(await db.AuditLogs.Where(a => a.ActorUserId == users[0].Id && a.RecordType == "SupplierDocument" && records.Contains(a.RecordId)).ToListAsync());
            db.SupplierDocuments.RemoveRange(await db.SupplierDocuments.Where(d => docs.Contains(d.Id) && profiles.Contains(d.SupplierProfileId)).ToListAsync()); await db.SaveChangesAsync();
            db.SupplierProfiles.RemoveRange(await db.SupplierProfiles.Where(p => profiles.Contains(p.Id) && ids.Contains(p.UserId)).ToListAsync());
            db.EmployeeDepartments.RemoveRange(await db.EmployeeDepartments.Where(e => ids.Contains(e.UserId) && e.DepartmentId == department.Id).ToListAsync()); db.UserRoles.RemoveRange(await db.UserRoles.Where(r => ids.Contains(r.UserId)).ToListAsync()); await db.SaveChangesAsync();
            db.Users.RemoveRange(await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync()); db.Departments.RemoveRange(await db.Departments.Where(d => d.Id == department.Id).ToListAsync()); await db.SaveChangesAsync(); await tx.CommitAsync();
            var after = await Snapshot(target.ConnectionString);
            Check(after.Count == baseline.Count && baseline.All(x => after.TryGetValue(x.Key, out var value) && value == x.Value), "Every table count and SHA-256 contents fingerprint matches baseline after exact cleanup");
        }
        Console.WriteLine($"PASS: {checks} approved supplier-expiry SQL checks. Temporary records removed; existing contents unchanged. No migration or file changes.");
    }
    private sealed class Faults { public bool Fail; }
    private sealed class Store(SupplierExpiryStore inner, Faults faults) : ISupplierExpiryStore
    {
        public Task<IBidTransaction> BeginAsync(Guid id, CancellationToken ct) => inner.BeginAsync(id, ct);
        public Task<SupplierDocument?> DocumentAsync(Guid supplier, Guid document, CancellationToken ct) => inner.DocumentAsync(supplier, document, ct);
        public async Task SaveAsync(SupplierDocument document, AuditLog audit, CancellationToken ct) { await inner.SaveAsync(document, audit, ct); if (faults.Fail) throw new IOException("Synthetic post-save rollback probe"); }
    }
    private static async Task<Dictionary<string, (long Count, string Hash)>> Snapshot(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(); var tables = new List<(string Name, string Keys)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT t.name, STRING_AGG(QUOTENAME(c.name),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1 JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id WHERE s.name=N'dbo' GROUP BY t.name";
            await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        var snapshot = new Dictionary<string, (long, string)>();
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT * FROM [dbo].[" + table.Name.Replace("]", "]]") + "] ORDER BY " + table.Keys;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long count = 0;
            await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) { var values = new object[reader.FieldCount]; reader.GetValues(values); hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(values.Select(v => v is DBNull ? null : v).ToArray())); hash.AppendData([0]); count++; }
            snapshot[table.Name] = (count, Convert.ToHexString(hash.GetHashAndReset()));
        }
        return snapshot;
    }
}
