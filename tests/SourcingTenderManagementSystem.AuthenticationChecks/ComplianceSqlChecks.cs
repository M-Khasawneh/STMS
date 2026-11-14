using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class ComplianceSqlChecks
{
    public static async Task RunAsync(bool committeeChecks = false, bool evaluationChecks = false, bool cleanupOnly = false, bool reportChecks = false)
    {
        evaluationChecks = evaluationChecks || reportChecks;
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        var target = new SqlConnectionStringBuilder(connection);
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB")
            throw new Exception("Unapproved test database target.");
        var marker = "compliance-check-" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), marker);
        var failure = new AuditFailure();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection).AddInterceptors(failure));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IWebHostEnvironment>(new FilesEnvironment(root));
        services.AddScoped<IBidData, BidData>();
        services.AddScoped<IComplianceManagement, ComplianceManagement>();
        services.AddScoped<CommitteeManagement>();
        services.AddScoped<TenderManagement>();
        services.AddScoped<EvaluationManagement>();
        services.AddScoped<IUnifiedReportManagement, UnifiedReportManagement>();
        await using var provider = services.BuildServiceProvider();
        var userIds = Enumerable.Range(0, evaluationChecks ? 8 : committeeChecks ? 7 : 3).Select(i => marker + "-" + i).ToArray();
        var tenderId = Guid.NewGuid(); var bidId = Guid.NewGuid(); var requestId = Guid.NewGuid();
        var profileId = Guid.NewGuid(); var linkId = Guid.NewGuid();
        string baselineUsers, baselineRoles, baselineReports, baselineApprovals;
        using (var scope = provider.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Apply approved migrations before tests.");
            if (reportChecks) {
                var reports = await db.EvaluationReports.AsNoTracking().ToListAsync();
                var approvals = await db.TenderApprovals.AsNoTracking().Where(x => x.Stage == ApprovalStage.Manager).ToListAsync();
                foreach (var group in reports.GroupBy(x => x.TenderId)) _ = ReportReviewRules.Current(group.ToArray(), approvals.Where(x => x.TenderId == group.Key).ToArray());
                Console.WriteLine($"Read-only report preflight: {reports.Count} existing reports; revision/decision records consistent.");
            }
            if (cleanupOnly) {
                var rows = await db.Users.AsNoTracking().Where(x => x.UserName!.StartsWith("compliance-check-")).ToListAsync();
                var pattern = new System.Text.RegularExpressions.Regex("^(compliance-check-[a-f0-9]{32})-[0-7]$");
                if (rows.Count != 8 || rows.Any(x => !pattern.IsMatch(x.UserName!)) || rows.Select(x => pattern.Match(x.UserName!).Groups[1].Value).Distinct().Count() != 1)
                    throw new Exception("Recovery requires exactly one identifiable eight-user evaluation fixture.");
                marker = pattern.Match(rows[0].UserName!).Groups[1].Value;
                root = Path.Combine(Path.GetTempPath(), marker);
                userIds = Enumerable.Range(0, 8).Select(i => marker + "-" + i).ToArray();
                tenderId = await db.Tenders.Where(x => x.Title == marker && x.CreatedByUserId == userIds[0]).Select(x => x.Id).SingleAsync();
                requestId = await db.TenderPurchaseRequisitions.Where(x => x.TenderId == tenderId).Select(x => x.PurchaseRequisitionId).SingleAsync();
                linkId = await db.TenderSuppliers.Where(x => x.TenderId == tenderId).Select(x => x.Id).SingleAsync();
                bidId = await db.Bids.Where(x => x.TenderId == tenderId && x.TenderSupplierId == linkId).Select(x => x.Id).SingleAsync();
                Console.WriteLine("Recovery identified one owned evaluation test fixture. Existing non-fixture rows will be compared after cleanup.");
            }
            if (committeeChecks || evaluationChecks) {
                var assignments = await db.CommitteeAssignments.AsNoTracking().Where(x => x.Kind == CommitteeKind.Evaluation).ToListAsync();
                var members = await db.CommitteeMembers.AsNoTracking().Where(x => assignments.Select(a => a.Id).Contains(x.CommitteeAssignmentId)).ToListAsync();
                var active = members.Where(x => CommitteeRules.Effective(x, DateTimeOffset.UtcNow)).ToArray();
                if (assignments.GroupBy(x => x.TenderId).Any(x => x.Count() > 1) || members.Any(x => !CommitteeRules.Positions.Contains(x.Position)) ||
                    active.GroupBy(x => new { x.TenderId, x.Position }).Any(x => x.Count() > 1) || active.GroupBy(x => new { x.TenderId, x.UserId }).Any(x => x.Count() > 1))
                    throw new Exception("Existing committee data conflict detected. No test writes performed.");
                Console.WriteLine($"Read-only committee preflight: {assignments.Count} assignments, {members.Count} membership records; no conflicts.");
            }
            baselineReports = JsonSerializer.Serialize(await db.EvaluationReports.AsNoTracking().Where(x => x.TenderId != tenderId).OrderBy(x => x.Id).ToListAsync());
            baselineApprovals = JsonSerializer.Serialize(await db.TenderApprovals.AsNoTracking().Where(x => x.TenderId != tenderId).OrderBy(x => x.Id).ToListAsync());
            baselineUsers = JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(x => !userIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync());
            baselineRoles = JsonSerializer.Serialize(await db.UserRoles.AsNoTracking().Where(x => !userIds.Contains(x.UserId)).OrderBy(x => x.UserId).ThenBy(x => x.RoleId).ToListAsync());
        }
        var count = 0;
        void Check(bool valid, string message) {
            if (!valid) throw new Exception(message);
            count++; Console.WriteLine("PASS: " + message);
        }
        ClaimsPrincipal Actor(int index) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userIds[index])], "SqlTest"));
        async Task<T> Execute<T>(Func<IComplianceManagement, Task<T>> action) {
            using var scope = provider.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<IComplianceManagement>());
        }
        async Task Mutate(Func<IComplianceManagement, Task> action) => await Execute(async service => { await action(service); return true; });
        Task<CompliancePage> Page(int actor = 0) => Execute(service => service.PageAsync(Actor(actor), bidId));
        async Task Reject(Func<Task> action, int status) {
            try { await action(); throw new Exception("Expected service rejection"); }
            catch (BidException e) { Check(e.Status == status, "SQL-backed guard rejects " + status); }
        }
        try
        {
            if (cleanupOnly) return;
            using (var scope = provider.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var department = await db.Departments.Select(x => x.Id).FirstAsync();
                for (var i = 0; i < userIds.Length; i++) {
                    var user = new IdentityUser { Id = userIds[i], UserName = userIds[i] };
                    var created = await users.CreateAsync(user);
                    if (!created.Succeeded) throw new Exception("Temporary Identity user creation failed.");
                    var role = i == 0 ? (committeeChecks || evaluationChecks ? ApplicationRoles.SourcingManager : ApplicationRoles.SourcingOfficer)
                        : i == 7 ? ApplicationRoles.SourcingOfficer : i >= 3 ? ApplicationRoles.EvaluationCommittee : ApplicationRoles.Supplier;
                    var assigned = await users.AddToRoleAsync(user, role);
                    if (!assigned.Succeeded) throw new Exception("Temporary role assignment failed.");
                    if (i >= 3) db.EmployeeDepartments.Add(new EmployeeDepartment { UserId = user.Id, DepartmentId = department });
                }
                db.EmployeeDepartments.Add(new EmployeeDepartment { UserId = userIds[0], DepartmentId = department });
                db.SupplierProfiles.Add(new SupplierProfile { Id = profileId, UserId = userIds[1], CompanyName = marker, RegistrationNumber = marker, TaxNumber = marker, Status = SupplierStatus.Approved });
                db.SupplierProfiles.Add(new SupplierProfile { Id = Guid.NewGuid(), UserId = userIds[2], CompanyName = marker + "-other", RegistrationNumber = marker + "-other", TaxNumber = marker + "-other", Status = SupplierStatus.Approved });
                db.PurchaseRequisitions.Add(new PurchaseRequisition { Id = requestId, DepartmentId = department, CreatedByUserId = userIds[0], Title = marker, Priority = "Medium", TargetDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = PurchaseRequestStatus.ConvertedToTender, CreatedAt = DateTimeOffset.UtcNow });
                db.Tenders.Add(new Tender { Id = tenderId, CreatedByUserId = userIds[0], Title = marker, Status = TenderStatus.Closed, SubmissionStart = DateTimeOffset.UtcNow.AddDays(-2), SubmissionDeadline = DateTimeOffset.UtcNow.AddDays(-1) });
                db.TenderPurchaseRequisitions.Add(new TenderPurchaseRequisition { TenderId = tenderId, PurchaseRequisitionId = requestId });
                db.TenderSuppliers.Add(new TenderSupplier { Id = linkId, TenderId = tenderId, SupplierProfileId = profileId });
                db.Invitations.Add(new Invitation { Id = Guid.NewGuid(), TenderSupplierId = linkId, SentByUserId = userIds[0], SentAt = DateTimeOffset.UtcNow.AddDays(-2), Status = InvitationStatus.Accepted });
                db.Bids.Add(new Bid { Id = bidId, TenderId = tenderId, TenderSupplierId = linkId, Status = BidStatus.Submitted, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1), Currency = "JOD", PaymentTerms = "Original terms", TechnicalDetails = "Original technical contents" });
                await db.SaveChangesAsync();
            }
            if (evaluationChecks) {
                await EvaluationSqlChecks.VerifyAsync(provider, userIds, tenderId, bidId, value => failure.Enabled = value, reportChecks);
                return;
            }
            if (committeeChecks) {
                await CommitteeSqlChecks.VerifyAsync(provider, userIds, tenderId, bidId, value => failure.Enabled = value);
                return;
            }
            var page = await Page();
            await Reject(() => Page(2), 404);
            await Reject(() => Mutate(s => s.DecideAsync(Actor(1), bidId, new() { Version = page.Version, Outcome = ComplianceStatus.Compliant })), 403);
            await Reject(() => Mutate(s => s.DecideAsync(Actor(0), bidId, new() { Version = page.Version, Outcome = ComplianceStatus.NonCompliant })), 400);
            await Mutate(s => s.AskAsync(Actor(0), bidId, new() { Version = page.Version, Subject = "Specification evidence", Description = "Please explain the submitted specification." }));
            await Reject(() => Mutate(s => s.AskAsync(Actor(0), bidId, new() { Version = page.Version, Subject = "Duplicate", Description = "Duplicate" })), 409);
            page = await Page(1);
            Check(page.Threads.Single().Request.RecipientUserId == userIds[1] && page.Reviews.Single().Outcome == ComplianceStatus.PendingClarification, "Request recipient and pending review persist");
            var clarification = page.Threads.Single().Request.Id;
            var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nclarification evidence\n%%EOF");
            ClarificationReplyForm Reply(string version) => new() { Version = version, Response = "Explanation of original contents", Files = [new FormFile(new MemoryStream(pdf), 0, pdf.Length, "Files", "evidence.pdf")] };
            failure.Enabled = true;
            try { await Mutate(s => s.ReplyAsync(Actor(1), bidId, clarification, Reply(page.Version))); throw new Exception("Expected audit failure"); }
            catch (InjectedAuditFailure) { Check(true, "Injected SQL save failure propagates"); }
            finally { failure.Enabled = false; }
            var afterFailure = await Page(1);
            Check(afterFailure.Threads.Single().Replies.Count == 0 && afterFailure.Threads.Single().Request.Status == ClarificationStatus.Pending, "Reply, attachments and request state roll back together");
            Check(!Directory.Exists(root) || Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories).Length == 0, "Uncommitted private evidence cleaned after rollback");
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
                try { await Mutate(s => s.ReplyAsync(Actor(1), bidId, clarification, Reply(afterFailure.Version))); return 200; }
                catch (BidException e) { return e.Status; }
            }));
            Check(results.Count(x => x == 200) == 1 && results.Count(x => x == 409) == 1, "Concurrent replies commit exactly once across independent SQL contexts");
            page = await Page();
            var attachment = page.Threads.Single().Replies.Single().Attachments.Single();
            await using (var download = await Execute(s => s.DownloadAsync(Actor(1), bidId, attachment.Id))) {
                using var bytes = new MemoryStream(); await download.Content.CopyToAsync(bytes);
                Check(bytes.ToArray().SequenceEqual(pdf), "SQL-linked private attachment downloads verified bytes");
            }
            await Reject(() => Execute(s => s.DownloadAsync(Actor(2), bidId, attachment.Id)), 404);
            await Mutate(s => s.ResolveAsync(Actor(0), bidId, clarification, page.Version));
            page = await Page();
            Check(page.Threads.Single().Request.Status == ClarificationStatus.Resolved, "Officer resolution persists");
            await Mutate(s => s.DecideAsync(Actor(0), bidId, new() { Version = page.Version, Outcome = ComplianceStatus.NonCompliant, Reasons = "Mandatory specification not met." }));
            page = await Page();
            Check(page.Reviews.Any(x => x.Outcome == ComplianceStatus.NonCompliant && x.Reasons == "Mandatory specification not met."), "Non-compliance reason preserved");
            await Mutate(s => s.DecideAsync(Actor(0), bidId, new() { Version = page.Version, Outcome = ComplianceStatus.Compliant, Reasons = "Review complete" }));
            using (var scope = provider.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var bid = await db.Bids.AsNoTracking().SingleAsync(x => x.Id == bidId);
                Check(bid.Status == BidStatus.Compliant && bid.Currency == "JOD" && bid.PaymentTerms == "Original terms" && bid.TechnicalDetails == "Original technical contents", "Compliance updates status while preserving submitted contents");
                Check(await db.Notifications.CountAsync(x => x.BidId == bidId) == 2, "Exactly one request and one response notification persist");
                Check(await db.AuditLogs.CountAsync(x => x.RecordType == "Bid" && x.RecordId == bidId.ToString()) == 5, "Review, request, response and resolution audit records persist");
            }
        }
        finally
        {
            failure.Enabled = false;
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            var questions = db.ClarificationRequests.Where(x => x.BidId == bidId).Select(x => x.Id);
            var responses = db.ClarificationResponses.Where(x => questions.Contains(x.ClarificationRequestId)).Select(x => x.Id);
            await db.ClarificationAttachments.Where(x => responses.Contains(x.ClarificationResponseId)).ExecuteDeleteAsync();
            await db.ClarificationResponses.Where(x => questions.Contains(x.ClarificationRequestId)).ExecuteDeleteAsync();
            await db.ClarificationRequests.Where(x => x.BidId == bidId).ExecuteDeleteAsync();
            await db.ComplianceReviews.Where(x => x.BidId == bidId).ExecuteDeleteAsync();
            await db.Notifications.Where(x => x.BidId == bidId || x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.AuditLogs.Where(x => userIds.Contains(x.ActorUserId!)).ExecuteDeleteAsync();
            await db.TenderApprovals.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.EvaluationReports.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.TechnicalEvaluations.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.FinancialEvaluations.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.EvaluationCriteria.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.CommitteeMembers.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.CommitteeAssignments.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.BidItems.Where(x => x.BidId == bidId).ExecuteDeleteAsync();
            await db.BidCostComponents.Where(x => x.BidId == bidId).ExecuteDeleteAsync();
            await db.Bids.Where(x => x.Id == bidId).ExecuteDeleteAsync();
            await db.Invitations.Where(x => x.TenderSupplierId == linkId).ExecuteDeleteAsync();
            await db.TenderSuppliers.Where(x => x.Id == linkId).ExecuteDeleteAsync();
            await db.TenderPurchaseRequisitions.Where(x => x.TenderId == tenderId).ExecuteDeleteAsync();
            await db.Tenders.Where(x => x.Id == tenderId).ExecuteDeleteAsync();
            await db.PurchaseRequisitionItems.Where(x => x.PurchaseRequisitionId == requestId).ExecuteDeleteAsync();
            await db.PurchaseRequisitions.Where(x => x.Id == requestId).ExecuteDeleteAsync();
            await db.SupplierProfiles.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.EmployeeDepartments.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.UserRoles.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync();
            await tx.CommitAsync();
            Check(baselineUsers == JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync()), "Existing Identity records preserved and temporary users removed");
            Check(baselineRoles == JsonSerializer.Serialize(await db.UserRoles.AsNoTracking().OrderBy(x => x.UserId).ThenBy(x => x.RoleId).ToListAsync()), "Existing role assignments preserved");
            Check(baselineReports == JsonSerializer.Serialize(await db.EvaluationReports.AsNoTracking().OrderBy(x => x.Id).ToListAsync()), "Existing report rows preserved and temporary versions removed");
            Check(baselineApprovals == JsonSerializer.Serialize(await db.TenderApprovals.AsNoTracking().OrderBy(x => x.Id).ToListAsync()), "Existing approval rows preserved and temporary decisions removed");
            var resolved = Path.GetFullPath(root);
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(resolved) != marker) throw new Exception("Unsafe private fixture cleanup path");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
        Console.WriteLine($"PASS: {count} SQL-backed compliance persistence/security/rollback/concurrency checks; temporary records and files cleaned.");
    }
    private sealed class InjectedAuditFailure : Exception;
    private sealed class AuditFailure : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action is "ClarificationAnswered" or "MemberReplaced" or "EvaluationConfirmed" or "ReportSubmitted" or "Tender.ManagerDecision")) throw new InjectedAuditFailure();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    private sealed class FilesEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ComplianceSqlChecks";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
