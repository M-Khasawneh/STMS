using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SourcingTenderManagementSystem.Web.Controllers;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

// Explicit opt-in only: requires approval for transient SQL writes and cleanup.
public static class SupplierSqlChecks
{
    public static async Task RunAsync()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
        var target = new SqlConnectionStringBuilder(connection);
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Unapproved SQL target.");
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
        var privateRoot = Path.Combine(Path.GetTempPath(), "supplier-sql-" + Guid.NewGuid().ToString("N"));
        var fileStore = new FaultingFiles(new PrivateSupplierFiles(new TestEnvironment(privateRoot)));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(AccountController).Assembly.GetName().Name,
            EnvironmentName = "Development", WebRootPath = Path.GetFullPath("src/SourcingTenderManagementSystem.Web/wwwroot") });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddControllersWithViews();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection));
        builder.Services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthenticationFoundation();
        builder.Services.AddSingleton<IPrivateSupplierFiles>(fileStore);
        builder.Services.AddScoped<SupplierRegistration>(); builder.Services.AddScoped<SupplierReview>();
        builder.Services.AddScoped<IEmployeeData, EmployeeData>(); builder.Services.AddScoped<EmployeeManagement>();
        builder.Services.AddScoped<IDepartmentStore, DepartmentStore>(); builder.Services.AddScoped<IPublicStatistics, PublicStatistics>();
        await using var app = builder.Build();
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        var marker = "supplier-check-" + Guid.NewGuid().ToString("N");
        var password = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        Dictionary<string, long> baseline;
        string originalUsers;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Check((await db.Database.GetAppliedMigrationsAsync()).Contains("20261113120000_SupplierRegistrationUniqueness"), "supplier migration is applied before SQL tests");
            baseline = await Counts(db);
            originalUsers = JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
        }
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        HttpClient Browser() => new(new HttpClientHandler { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate.Thumbprint }) { BaseAddress = new Uri(address) };
        using var anonymous = Browser(); using var admin = Browser(); using var supplierClient = Browser(); using var otherClient = Browser(); using var employeeClient = Browser();
        try
        {
            await CreateAccount(marker + "-admin", ApplicationRoles.Admin);
            await CreateAccount(marker + "-employee", ApplicationRoles.RequestingDepartment);
            await Login(admin, marker + "-admin", true);
            await Login(employeeClient, marker + "-employee", true);
            Check((await anonymous.GetAsync("/Suppliers")).StatusCode == HttpStatusCode.Redirect, "anonymous Admin list access denied");
            Check((await employeeClient.GetAsync("/Suppliers")).Headers.Location?.OriginalString.Contains("AccessDenied") == true, "employee Admin supplier access denied");
            Check((await anonymous.PostAsync("/Suppliers/Register", new MultipartFormDataContent())).StatusCode == HttpStatusCode.BadRequest, "supplier POST requires antiforgery");
            var token = await Token(anonymous, "/Suppliers/Register");
            foreach (var mode in new[] { "missing", "signature", "extension", "oversize" })
            {
                var invalid = await anonymous.PostAsync("/Suppliers/Register", Registration(marker + "-invalid", "R-" + marker, "T-" + marker, token, mode));
                Check(invalid.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, "invalid document refused: " + mode);
            }
            Check(await Find(marker + "-invalid") is null, "invalid uploads create no Identity user");
            var response = await anonymous.PostAsync("/Suppliers/Register", Registration(marker + "-one", "R-" + marker, "T-" + marker, token));
            Check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString.Contains("Confirmation") == true, "valid supplier registration redirects to confirmation");
            var one = (await Find(marker + "-one"))!;
            Check(one.Status == SupplierStatus.Pending, "new SQL supplier status is Pending");
            using (var scope = app.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var user = (await users.FindByIdAsync(one.UserId))!;
                Check(await users.CheckPasswordAsync(user, password) && (await users.GetRolesAsync(user)).SequenceEqual(new[] { ApplicationRoles.Supplier }), "Identity persists hashed password and only existing Supplier role");
                var docs = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SupplierDocuments.Where(x => x.SupplierProfileId == one.Id).ToListAsync();
                Check(docs.Count == 3 && docs.All(x => x.ContentType == "application/pdf"), "three mandatory SQL document records persist");
            }
            await Login(supplierClient, marker + "-one", false);
            var firstDoc = await DocumentId(one.Id);
            Check((await anonymous.GetAsync("/Suppliers/Document/" + firstDoc)).StatusCode == HttpStatusCode.Redirect, "anonymous document access denied");
            Check((await employeeClient.GetAsync("/Suppliers/Document/" + firstDoc)).Headers.Location?.OriginalString.Contains("AccessDenied") == true, "employee document access denied");
            foreach (var pair in new[] { ("R-" + marker, "OTHER-T-" + marker), ("OTHER-R-" + marker, "T-" + marker), (" r-" + marker + " ", "OTHER-T-" + marker) })
            {
                var duplicate = await anonymous.PostAsync("/Suppliers/Register", Registration(marker + "-duplicate", pair.Item1, pair.Item2, token));
                Check(duplicate.StatusCode == HttpStatusCode.OK && await Find(marker + "-duplicate") is null, "duplicate company/tax registration rejected without orphan user");
            }
            Check((await admin.GetAsync("/Suppliers/Details/" + one.Id)).StatusCode == HttpStatusCode.OK, "Admin supplier details render");
            var download = await admin.GetAsync("/Suppliers/Document/" + firstDoc);
            Check(download.StatusCode == HttpStatusCode.OK && download.Content.Headers.ContentDisposition?.DispositionType == "attachment" && download.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "Admin document downloads are protected attachments");
            await Decision(one.Id, SupplierStatus.Pending, SupplierStatus.Rejected, "", false);
            Check((await Find(marker + "-one"))!.Status == SupplierStatus.Pending, "rejection without reason leaves status unchanged");
            await Decision(one.Id, SupplierStatus.Pending, SupplierStatus.Approved, "", true);
            Check((await Find(marker + "-one"))!.Status == SupplierStatus.Approved, "Admin approval persists to SQL");
            await Login(supplierClient, marker + "-one", true);
            Check((await supplierClient.GetAsync("/Suppliers/Status")).StatusCode == HttpStatusCode.OK, "approved supplier can view own application status");
            Check((await supplierClient.GetAsync("/Suppliers/Document/" + firstDoc)).StatusCode == HttpStatusCode.OK, "approved supplier can download own documents");
            Check((await supplierClient.GetAsync("/Employees")).Headers.Location?.OriginalString.Contains("AccessDenied") == true, "approved supplier cannot access employee management");
            var deniedReview = await supplierClient.PostAsync("/Suppliers/Review/" + one.Id, new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedStatus"] = "Approved", ["Decision"] = "Suspended", ["Reason"] = "forged", ["__RequestVerificationToken"] = await Token(supplierClient, "/Suppliers/Status") }));
            Check(deniedReview.Headers.Location?.OriginalString.Contains("AccessDenied") == true, "supplier cannot perform Admin decisions");
            response = await anonymous.PostAsync("/Suppliers/Register", Registration(marker + "-two", "R2-" + marker, "T2-" + marker, token));
            Check(response.StatusCode == HttpStatusCode.Redirect, "second independent supplier registers");
            var two = (await Find(marker + "-two"))!;
            var secondDoc = await DocumentId(two.Id);
            await Decision(two.Id, SupplierStatus.Pending, SupplierStatus.Approved, "", true);
            await Login(otherClient, marker + "-two", true);
            Check((await supplierClient.GetAsync("/Suppliers/Document/" + secondDoc)).StatusCode == HttpStatusCode.NotFound && (await otherClient.GetAsync("/Suppliers/Document/" + firstDoc)).StatusCode == HttpStatusCode.NotFound, "suppliers cannot access competitor documents in either direction");
            Check((await supplierClient.GetAsync("/Suppliers/Details/" + two.Id)).Headers.Location?.OriginalString.Contains("AccessDenied") == true, "supplier cannot access competitor application details");
            // Saving an old Pending review cannot reverse an already approved application.
            await Decision(two.Id, SupplierStatus.Pending, SupplierStatus.Rejected, "stale", false);
            Check((await Find(marker + "-two"))!.Status == SupplierStatus.Approved, "stale review cannot overwrite current decision");
            await Decision(one.Id, SupplierStatus.Approved, SupplierStatus.Suspended, "", false);
            await Decision(one.Id, SupplierStatus.Approved, SupplierStatus.Suspended, "Temporary SQL test suspension", true);
            one = (await Find(marker + "-one"))!;
            Check(one.Status == SupplierStatus.Suspended && one.ReviewReason == "Temporary SQL test suspension" && one.ReviewedAt.HasValue, "suspension reason, reviewer and time persist");
            Check((await supplierClient.GetAsync("/Suppliers/Document/" + firstDoc)).Headers.Location?.OriginalString.Contains("Login") == true, "suspension revokes an existing supplier session");
            await Login(supplierClient, marker + "-one", false);
            response = await anonymous.PostAsync("/Suppliers/Register", Registration(marker + "-reject", "RR-" + marker, "TR-" + marker, token));
            var rejected = (await Find(marker + "-reject"))!;
            await Decision(rejected.Id, SupplierStatus.Pending, SupplierStatus.Rejected, "Missing applicable trade coverage", true);
            Check((await Find(marker + "-reject"))!.ReviewReason == "Missing applicable trade coverage", "Admin rejection persists documented reason");
            await Login(supplierClient, marker + "-reject", false);
            fileStore.FailAfter = 1;
            response = await anonymous.PostAsync("/Suppliers/Register", Registration(marker + "-failure", "RF-" + marker, "TF-" + marker, token));
            Check(response.StatusCode == HttpStatusCode.OK && await Find(marker + "-failure") is null, "mid-upload failure rolls back Identity and SQL registration");
            fileStore.FailAfter = null;
            var concurrent = await Task.WhenAll(RegisterConcurrent("-race1"), RegisterConcurrent("-race2"));
            Check(concurrent.Count(x => x.Succeeded) == 1, "concurrent company/tax duplicate attempts yield exactly one success");
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var count = await db.SupplierProfiles.CountAsync(x => x.RegistrationNumber == ("RACE-" + marker).ToUpper());
                Check(count == 1, "unique SQL indexes preserve one company after concurrent submissions");
                Check(await db.Users.CountAsync(x => x.UserName == marker + "-race1" || x.UserName == marker + "-race2") == 1, "failed concurrent registration leaves no orphan Identity account");
                Check(fileStore.References.Count == await db.SupplierDocuments.CountAsync(x => x.SupplierProfileId == one.Id || x.SupplierProfileId == two.Id || x.SupplierProfileId == rejected.Id || db.SupplierProfiles.Any(p => p.Id == x.SupplierProfileId && p.RegistrationNumber == ("RACE-" + marker).ToUpper())), "no orphan files remain after upload and concurrency failures");
            }
            var logout = await otherClient.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = await Token(otherClient, "/Suppliers/Status") }));
            Check(logout.StatusCode == HttpStatusCode.Redirect && (await otherClient.GetAsync("/Suppliers/Status")).Headers.Location?.OriginalString.Contains("Login") == true, "approved supplier can log out without employee privileges");
        }
        finally
        {
            await app.StopAsync();
            using var cleanup = app.Services.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = cleanup.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var ownedUsers = await db.Users.Where(x => x.UserName != null && x.UserName.StartsWith(marker + "-")).ToListAsync();
            var ownedIds = ownedUsers.Select(x => x.Id).ToList();
            var ownedProfiles = await db.SupplierProfiles.Where(x => ownedIds.Contains(x.UserId)).ToListAsync();
            var profileIds = ownedProfiles.Select(x => x.Id).ToList();
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.SupplierDocuments.RemoveRange(await db.SupplierDocuments.Where(x => profileIds.Contains(x.SupplierProfileId)).ToListAsync());
            await db.SaveChangesAsync();
            db.SupplierProfiles.RemoveRange(ownedProfiles);
            db.EmployeeDepartments.RemoveRange(await db.EmployeeDepartments.Where(x => ownedIds.Contains(x.UserId)).ToListAsync());
            await db.SaveChangesAsync();
            foreach (var user in ownedUsers) Check((await users.DeleteAsync(user)).Succeeded, "temporary SQL account cleaned up");
            await transaction.CommitAsync();
            foreach (var reference in fileStore.References.ToArray()) fileStore.Delete(reference);
            if (Directory.Exists(privateRoot)) Directory.Delete(privateRoot, true);
            var after = await Counts(db);
            Check(baseline.Count == after.Count && baseline.All(x => after.GetValueOrDefault(x.Key) == x.Value), "every SQL table count matches the pre-test baseline");
            Check(originalUsers == JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync()), "all pre-existing Identity accounts remain byte-for-byte unchanged");
        }
        Console.WriteLine("PASS: supplier SQL/HTTPS integration checks; temporary accounts, applications and private files removed.");

        async Task CreateAccount(string name, string role)
        {
            using var scope = app.Services.CreateScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser(name);
            Check((await users.CreateAsync(user, password)).Succeeded && (await users.AddToRoleAsync(user, role)).Succeeded, "temporary authorized SQL test fixture created");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.EmployeeDepartments.Add(new EmployeeDepartment { UserId = user.Id, DepartmentId = await db.Departments.Where(x => x.Name == "Administration").Select(x => x.Id).SingleAsync() });
            await db.SaveChangesAsync();
        }
        async Task<SupplierProfile?> Find(string name)
        {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(p => db.Users.Any(u => u.Id == p.UserId && u.UserName == name));
        }
        async Task<Guid> DocumentId(Guid id) { using var scope = app.Services.CreateScope(); return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SupplierDocuments.Where(x => x.SupplierProfileId == id).Select(x => x.Id).FirstAsync(); }
        async Task Login(HttpClient client, string name, bool expected)
        {
            var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["UserName"] = name, ["Password"] = password, ["__RequestVerificationToken"] = await Token(client, "/Account/Login") }));
            Check(expected ? response.StatusCode == HttpStatusCode.Redirect : response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("Unable to sign in."), "supplier approval/role state controls sign-in");
        }
        async Task Decision(Guid id, SupplierStatus expected, SupplierStatus decision, string reason, bool success)
        {
            var path = "/Suppliers/Details/" + id;
            var response = await admin.PostAsync("/Suppliers/Review/" + id, new FormUrlEncodedContent(new Dictionary<string,string> { ["ExpectedStatus"] = expected.ToString(), ["Decision"] = decision.ToString(), ["Reason"] = reason, ["__RequestVerificationToken"] = await Token(admin, path) }));
            Check(response.StatusCode == (success ? HttpStatusCode.Redirect : HttpStatusCode.OK), "Admin decision validation: " + decision);
        }
        MultipartFormDataContent Registration(string name, string registration, string tax, string csrf, string mode = "valid")
        {
            var content = new MultipartFormDataContent();
            foreach (var item in new Dictionary<string,string> { ["CompanyName"] = "Temporary SQL Test Company", ["RegistrationNumber"] = registration, ["TaxNumber"] = tax,
                ["ContactName"] = "Test Contact", ["ContactEmail"] = "supplier-check@example.invalid", ["ContactPhone"] = "+96265550100", ["UserName"] = name, ["Password"] = password, ["ConfirmPassword"] = password, ["__RequestVerificationToken"] = csrf }) content.Add(new StringContent(item.Value), item.Key);
            if (mode != "missing") foreach (var field in new[] { "CompanyCertificate", "TradeLicense", "TaxCertificate" })
            {
                var bytes = mode == "oversize" ? new byte[SupplierUploadValidation.MaximumBytes + 1] : mode == "signature" ? "not a PDF"u8.ToArray() : Pdf;
                content.Add(new ByteArrayContent(bytes), field, mode == "extension" ? "document.exe" : "document.pdf");
            }
            return content;
        }
        async Task<IdentityResult> RegisterConcurrent(string suffix)
        {
            using var scope = app.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<SupplierRegistration>().RegisterAsync(new SupplierRegistrationViewModel {
                CompanyName = "Concurrent SQL test", RegistrationNumber = "RACE-" + marker, TaxNumber = "RACET-" + marker,
                ContactName = "Test", ContactEmail = "race@example.invalid", ContactPhone = "+96265550100", UserName = marker + suffix,
                Password = password, ConfirmPassword = password, CompanyCertificate = Form(), TradeLicense = Form(), TaxCertificate = Form() }, default);
        }
    }
    private static byte[] Pdf => DocumentExtractionChecks.TextPdf();
    private static Microsoft.AspNetCore.Http.IFormFile Form() => new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(Pdf), 0, Pdf.Length, "file", "document.pdf");
    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path); var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success) throw new Exception("Missing antiforgery token: " + path);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static async Task<Dictionary<string,long>> Counts(ApplicationDbContext database)
    {
        await database.Database.OpenConnectionAsync(); var counts = new Dictionary<string,long>();
        foreach (var table in database.Model.GetEntityTypes().Select(x => x.GetTableName()!).Distinct().Append("__EFMigrationsHistory"))
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[" + table.Replace("]", "]]") + "]";
            counts[table] = (long)(await command.ExecuteScalarAsync())!;
        }
        return counts;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
    private sealed class FaultingFiles(IPrivateSupplierFiles inner) : IPrivateSupplierFiles
    {
        public int? FailAfter { get; set; }
        public System.Collections.Concurrent.ConcurrentDictionary<string,byte> Saved { get; } = new();
        public IReadOnlyCollection<string> References => Saved.Keys.ToArray();
        public async Task<string> SaveAsync(ValidatedSupplierFile file, CancellationToken cancellationToken)
        {
            if (FailAfter.HasValue) { if (FailAfter == 0) throw new IOException("Synthetic storage failure"); FailAfter--; }
            var reference = await inner.SaveAsync(file, cancellationToken); Saved.TryAdd(reference, 0); return reference;
        }
        public Task<Stream> OpenAsync(string reference, CancellationToken cancellationToken) => inner.OpenAsync(reference, cancellationToken);
        public void Delete(string reference) { inner.Delete(reference); Saved.TryRemove(reference, out _); }
    }
    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SupplierSqlChecks";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
