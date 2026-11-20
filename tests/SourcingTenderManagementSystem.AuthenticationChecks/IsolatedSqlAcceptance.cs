using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Explicit approval only. Production connection is used solely for before/after SELECT fingerprints.
internal static partial class IsolatedSqlAcceptance
{
    internal sealed class FailureProbe : SaveChangesInterceptor
    {
        public string? Action; private bool pending;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { pending = Action is not null && data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == Action); return base.SavingChangesAsync(data, result, ct); }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        { if (pending) { pending = false; throw new InjectedFailure(); } return base.SavedChangesAsync(data, result, ct); }
    }
    internal sealed class InjectedFailure : Exception;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); }
    public static async Task Run()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var original = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        if (!original.IntegratedSecurity || original.DataSource != @"localhost\MSSQLSERVER02" || original.InitialCatalog != "SourcingTenderDB") throw new Exception("Unexpected protected source target.");
        var run = Guid.NewGuid().ToString("N"); var name = "STMS_Acceptance_" + run;
        if (!Regex.IsMatch(name, "^STMS_Acceptance_[a-f0-9]{32}$")) throw new Exception("Unsafe isolated database name.");
        var master = new SqlConnectionStringBuilder(original.ConnectionString) { InitialCatalog = "master", Pooling = false, ConnectTimeout = 5 };
        var isolated = new SqlConnectionStringBuilder(original.ConnectionString) { InitialCatalog = name, Pooling = false, ConnectTimeout = 5 };
        var root = Path.Combine(Path.GetTempPath(), "stms-acceptance-" + run);
        var baseline = await SupplierExpirySqlChecks.Snapshot(original.ConnectionString);
        await using var control = new SqlConnection(master.ConnectionString); await control.OpenAsync();
        async Task<object?> Scalar(string sql) { await using var command = control.CreateCommand(); command.CommandText = sql; command.Parameters.AddWithValue("@name", name); return await command.ExecuteScalarAsync(); }
        Check(Convert.ToInt32(await Scalar("SELECT CASE WHEN DB_ID(@name) IS NULL THEN 1 ELSE 0 END")) == 1, "Isolated target does not exist; production is SELECT-only");
        var created = false; var marked = false;
        try
        {
            await using (var create = control.CreateCommand()) { create.CommandText = "CREATE DATABASE [" + name + "]"; create.CommandTimeout = 60; await create.ExecuteNonQueryAsync(); created = true; }
            await using (var markerConnection = new SqlConnection(isolated.ConnectionString))
            { await markerConnection.OpenAsync(); await using var mark = markerConnection.CreateCommand(); mark.CommandText = "EXEC sys.sp_addextendedproperty @name=N'STMS.AcceptanceRun', @value=@run"; mark.Parameters.AddWithValue("@run", run); await mark.ExecuteNonQueryAsync(); marked = true; }
            var clock = new TenderRuleChecks.TestClock(DateTimeOffset.UtcNow); var failure = new FailureProbe();
            var services = new ServiceCollection(); services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(isolated.ConnectionString).AddInterceptors(failure));
            services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IWebHostEnvironment>(new DocumentExtractionChecks.EnvironmentFixture { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
            services.AddScoped<IPrivateSupplierFiles, PrivateSupplierFiles>(); services.AddScoped<SupplierRegistration>(); services.AddScoped<SupplierReview>();
            services.AddScoped<RequisitionManagement>(); services.AddScoped<TenderManagement>();
            services.AddScoped<BidData>(); services.AddScoped<IBidData>(s => s.GetRequiredService<BidData>()); services.AddScoped<IBidDocumentData>(s => s.GetRequiredService<BidData>());
            services.AddScoped<IPrivateBidFiles, PrivateBidFiles>(); services.AddScoped<IBidSubmissionDocuments, BidSubmissionDocuments>(); services.AddScoped<BidManagement>(); services.AddScoped<BidDocumentManagement>();
            services.AddScoped<IComplianceManagement, ComplianceManagement>(); services.AddScoped<CommitteeManagement>();
            services.AddScoped<EvaluationManagement>(); services.AddScoped<IEvaluationManagement>(s => s.GetRequiredService<EvaluationManagement>()); services.AddScoped<IUnifiedReportManagement, UnifiedReportManagement>();
            services.AddScoped<ITopCommitteeData, TopCommitteeData>(); services.AddScoped<ITopCommitteeManagement, TopCommitteeManagement>(); services.AddScoped<IReporting, Reporting>();
            await using (var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }))
            {
                using (var scope = provider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    Check(db.Database.GetDbConnection().Database == name && name != original.InitialCatalog, "Migration target is exclusively the new isolated database");
                    // The unchanged department migration deliberately requires exactly one reviewed first Admin.
                    // Reproduce that historical prerequisite only in this new, empty, owned database.
                    await db.GetService<IMigrator>().MigrateAsync("20261112224814_InitialIdentityAndProcurement");
                    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    await RoleInitializer.InitializeAsync(roles);
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                    var firstAdmin = new IdentityUser("admin");
                    Check((await users.CreateAsync(firstAdmin, "Aa1!" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)))).Succeeded &&
                        (await users.AddToRoleAsync(firstAdmin, ApplicationRoles.Admin)).Succeeded, "Historical first-Admin prerequisite prepared in isolated schema only");
                    await db.Database.MigrateAsync();
                    Check((await db.Database.GetAppliedMigrationsAsync()).SequenceEqual(db.Database.GetMigrations()), "All existing migrations applied in exact assembly order to isolated database only");
                    await RoleInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
                }
                await Journey(provider, clock, failure);
            }
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL: isolated acceptance before cleanup: " + e.Message); throw; }
        finally
        {
            if (created)
            {
                if (marked)
                {
                    await using var verify = new SqlConnection(isolated.ConnectionString); await verify.OpenAsync(); await using var query = verify.CreateCommand();
                    query.CommandText = "SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'STMS.AcceptanceRun'";
                    Check((string?)await query.ExecuteScalarAsync() == run, "Database cleanup ownership marker verified");
                }
                using var pool = new SqlConnection(isolated.ConnectionString); SqlConnection.ClearPool(pool);
                // No SINGLE_USER/forced disconnect. A busy or ownership-mismatched target must not be forced down.
                await using var drop = control.CreateCommand(); drop.CommandText = "DROP DATABASE [" + name + "]"; drop.CommandTimeout = 30; await drop.ExecuteNonQueryAsync();
                Check(Convert.ToInt32(await Scalar("SELECT CASE WHEN DB_ID(@name) IS NULL THEN 1 ELSE 0 END")) == 1, "Owned isolated database removed");
            }
            var resolved = Path.GetFullPath(root); var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(resolved) != "stms-acceptance-" + run) throw new Exception("Unsafe fixture file cleanup path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            var after = await SupplierExpirySqlChecks.Snapshot(original.ConnectionString);
            Check(after.Count == baseline.Count && baseline.All(x => after.TryGetValue(x.Key, out var row) && row == x.Value), "Protected database counts and full content fingerprints unchanged");
        }
        Console.WriteLine("PASS: isolated connected SQL acceptance completed; owned database and synthetic files removed. Production schema, records and settings unchanged.");
    }

    public static async Task CleanupOwned(string name)
    {
        if (!Regex.IsMatch(name, "^STMS_Acceptance_[a-f0-9]{32}$")) throw new Exception("Refusing a non-acceptance database.");
        var run = name["STMS_Acceptance_".Length..];
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var target = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Unexpected recovery server.");
        target.Pooling = false; target.InitialCatalog = name;
        await using (var verify = new SqlConnection(target.ConnectionString))
        { await verify.OpenAsync(); await using var command = verify.CreateCommand(); command.CommandText = "SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'STMS.AcceptanceRun'"; Check((string?)await command.ExecuteScalarAsync() == run, "Recovery marker matches the exact failed acceptance run"); }
        target.InitialCatalog = "master";
        await using var connection = new SqlConnection(target.ConnectionString); await connection.OpenAsync();
        await using var drop = connection.CreateCommand(); drop.CommandText = "DROP DATABASE [" + name + "]"; await drop.ExecuteNonQueryAsync();
        Check(true, "Owned failed-test database removed without forced disconnects or production changes");
    }
}
