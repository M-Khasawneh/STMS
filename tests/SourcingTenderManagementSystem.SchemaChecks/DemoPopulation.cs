using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Bids;
using SourcingTenderManagementSystem.Web.Models.Employees;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Requisitions;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Models.Tenders;
using SourcingTenderManagementSystem.Web.Security;

// Explicit, user-approved local demonstration population. Never registered in the web application's startup.
internal sealed class DemoPopulation
{
    private readonly ServiceProvider provider;
    private readonly DemoState state;
    private readonly string statePath;
    private readonly DemoClock clock;
    private DemoPopulation(ServiceProvider provider, DemoState state, string statePath, DemoClock clock)
    { this.provider = provider; this.state = state; this.statePath = statePath; this.clock = clock; }
    private static readonly string[] Accounts = ["demo.officer", "demo.manager", "demo.requester.it", "demo.requester.operations", "demo.evaluator.department", "demo.evaluator.finance", "demo.evaluator.technical", "demo.supplier.network", "demo.supplier.office", "demo.supplier.facilities", "demo.supplier.pending"];

    public static async Task<int> RunAsync()
    {
        var root = Path.GetFullPath("src/SourcingTenderManagementSystem.Web");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "appsettings.json")));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
        if (!target.IntegratedSecurity || target.DataSource != @"localhost\MSSQLSERVER02" || target.InitialCatalog != "SourcingTenderDB") throw new Exception("Unexpected database target. Stopped.");
        var path = Path.Combine(root, "App_Data", "DemoPopulation.json");
        var existing = File.Exists(path);
        var state = existing ? JsonSerializer.Deserialize<DemoState>(await File.ReadAllTextAsync(path))! : new DemoState();
        var clock = new DemoClock(state.BaseTime);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
        services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<IWebHostEnvironment>(new FilesEnvironment(root)); services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<IEmployeeData, EmployeeData>(); services.AddScoped<EmployeeManagement>();
        services.AddScoped<IDepartmentStore, DepartmentStore>();
        services.AddScoped<IPrivateSupplierFiles, PrivateSupplierFiles>(); services.AddScoped<SupplierRegistration>(); services.AddScoped<SupplierReview>();
        services.AddScoped<RequisitionManagement>(); services.AddScoped<TenderManagement>();
        services.AddScoped<BidData>(); services.AddScoped<IBidData>(s => s.GetRequiredService<BidData>()); services.AddScoped<IBidDocumentData>(s => s.GetRequiredService<BidData>());
        services.AddScoped<IPrivateBidFiles, PrivateBidFiles>(); services.AddScoped<IBidSubmissionDocuments, BidSubmissionDocuments>();
        services.AddScoped<BidManagement>(); services.AddScoped<BidDocumentManagement>(); services.AddScoped<CommitteeManagement>(); services.AddScoped<IComplianceManagement, ComplianceManagement>();
        await using var provider = services.BuildServiceProvider();
        var population = new DemoPopulation(provider, state, path, clock);
        await population.With<ApplicationDbContext, bool>(async db => {
            if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Database unavailable or migrations pending. Stopped.");
            if ((await db.Roles.CountAsync()) != ApplicationRoles.All.Count) throw new Exception("Review existing Identity roles before population.");
            if (!existing && await db.Users.AnyAsync(x => Accounts.Contains(x.UserName!))) throw new Exception("A demo username already exists without the local population manifest. Stopped.");
            Console.WriteLine($"Read-only preflight: departments={await db.Departments.CountAsync()}, employees={await db.EmployeeDepartments.CountAsync()}, suppliers={await db.SupplierProfiles.CountAsync()}, requests={await db.PurchaseRequisitions.CountAsync()}, tenders={await db.Tenders.CountAsync()}, bids={await db.Bids.CountAsync()}");
            return true;
        });
        if (state.Completed) {
            await population.Verify();
            Console.WriteLine("Demo dataset already completed. Verified without database writes.");
            return 0;
        }
        foreach (var account in Accounts) if (!state.Passwords.ContainsKey(account)) state.Passwords[account] = "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        SavePrivate(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        await population.Populate();
        await population.Verify();
        state.Completed = true;
        SavePrivate(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS: persistent DEMO dataset populated through application services. Private access details: " + path);
        return 0;
    }
    private async Task Verify()
    {
        foreach (var account in Accounts) {
            await With<UserManager<IdentityUser>, bool>(async users => {
                var user = await users.FindByNameAsync(account) ?? throw new Exception("Missing demo account.");
                if (!await users.CheckPasswordAsync(user, state.Passwords[account])) throw new Exception("Demo credentials do not match Identity.");
                return true;
            });
        }
        var offers = await With<ApplicationDbContext, List<Bid>>(db => db.Bids.AsNoTracking()
            .Where(b => db.Tenders.Any(t => t.Id == b.TenderId && t.Title.StartsWith("DEMO - "))).ToListAsync());
        foreach (var offer in offers) {
            var name = await With<ApplicationDbContext, string>(db => (from link in db.TenderSuppliers join profile in db.SupplierProfiles on link.SupplierProfileId equals profile.Id
                join user in db.Users on profile.UserId equals user.Id where link.Id == offer.TenderSupplierId select user.UserName!).SingleAsync());
            var principal = await Principal(name);
            var details = await With<BidManagement, BidDetails>(s => s.DetailsAsync(principal, offer.Id));
            foreach (var document in details.Snapshot.Documents) {
                await using var download = await With<BidDocumentManagement, BidDocumentDownload>(s => s.DownloadAsync(principal, offer.Id, document.Id));
                if (download.Size <= 0) throw new Exception("Demo bid document is missing.");
            }
        }
        Console.WriteLine($"PASS: all {Accounts.Length} account passwords match Identity hashes; private documents verified for {offers.Count} offers.");
    }
    private async Task<T> With<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        using var scope = provider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
    private async Task Do<TService>(Func<TService, Task> action) where TService : notnull =>
        _ = await With<TService, bool>(async service => { await action(service); return true; });
    private Task<IdentityUser?> User(string name) => With<UserManager<IdentityUser>, IdentityUser?>(u => u.FindByNameAsync(name));
    private async Task<ClaimsPrincipal> Principal(string name) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (await User(name))!.Id)], "DemoPopulation"));
    private async Task<Guid> Department(string name)
    {
        var found = await With<ApplicationDbContext, Guid?>(db => db.Departments.Where(x => x.Name == name).Select(x => (Guid?)x.Id).SingleOrDefaultAsync());
        if (found.HasValue) return found.Value;
        await Do<IDepartmentStore>(s => s.CreateAsync(name, default));
        return (await Department(name));
    }
    private static void Success(IdentityResult result) { if (!result.Succeeded) throw new Exception(string.Join(" ", result.Errors.Select(x => x.Description))); }
    private async Task Employee(string name, Guid department, params string[] roles)
    {
        if (await User(name) is not null) return;
        var password = state.Passwords[name];
        Success(await With<EmployeeManagement, IdentityResult>(s => s.CreateAsync(new EmployeeCreateViewModel {
            UserName = name, Email = name + "@example.invalid", DepartmentId = department, Roles = roles, Password = password, ConfirmPassword = password
        })));
    }
    private async Task<Guid> Supplier(string name, string company, bool approve, string admin)
    {
        var user = await User(name);
        if (user is null) {
            var password = state.Passwords[name];
            Success(await With<SupplierRegistration, IdentityResult>(s => s.RegisterAsync(new SupplierRegistrationViewModel {
                UserName = name, Password = password, ConfirmPassword = password, CompanyName = company,
                RegistrationNumber = "DEMO-REG-" + name, TaxNumber = "DEMO-TAX-" + name, ContactName = "DEMO company representative",
                ContactEmail = name + "@example.invalid", ContactPhone = "0000000000",
                CompanyCertificate = Document("company-registration.pdf", company + " - DEMO registration example"),
                TradeLicense = Document("trade-license.pdf", company + " - DEMO license example"),
                TaxCertificate = Document("tax-registration.pdf", company + " - DEMO tax document example")
            }, default)));
            user = (await User(name))!;
        }
        var profile = await With<ApplicationDbContext, SupplierProfile>(db => db.SupplierProfiles.AsNoTracking().SingleAsync(x => x.UserId == user.Id));
        if (!profile.CompanyName.StartsWith("DEMO", StringComparison.Ordinal)) throw new Exception("Existing supplier conflicts with demonstration data.");
        if (approve && profile.Status == SupplierStatus.Pending) {
            var error = await With<SupplierReview, string?>(s => s.DecideAsync(profile.Id, SupplierStatus.Pending, SupplierStatus.Approved, "Approved for demonstration only", admin, default));
            if (error is not null) throw new Exception(error);
        }
        return profile.Id;
    }
    private async Task<Guid> Request(string name, string owner, string description, string priority, PurchaseRequestStatus goal)
    {
        var principal = await Principal(owner);
        var ownerId = (await User(owner))!.Id;
        var title = "DEMO - " + name;
        var found = await With<ApplicationDbContext, PurchaseRequisition?>(db => db.PurchaseRequisitions.AsNoTracking().SingleOrDefaultAsync(x => x.Title == title));
        if (found is not null && found.CreatedByUserId != ownerId) throw new Exception("Existing request conflicts with demo ownership.");
        var id = found?.Id ?? await With<RequisitionManagement, Guid>(async s => await s.SaveAsync(null, await s.ActorAsync(principal), new RequisitionForm {
            Title = title, Reason = "DEMO - Synthetic procurement scenario for " + description, Priority = priority,
            TargetDate = DateOnly.FromDateTime(state.BaseTime.UtcDateTime.AddDays(60)), Items = [
                new RequisitionItemForm { Description = description, Quantity = 12m, Specifications = "DEMO - New units, one-year warranty, installation and delivery included." },
                new RequisitionItemForm { Description = "Installation and staff orientation", Quantity = 1m, Specifications = "DEMO - On-site setup and one training session." }
            ]
        }, false));
        if (goal == PurchaseRequestStatus.Draft || found?.Status == PurchaseRequestStatus.ConvertedToTender) return id;
        var detail = await With<RequisitionManagement, RequisitionDetails>(async s => await s.DetailsAsync(id, await s.ActorAsync(principal)));
        if (detail.Request.Status == PurchaseRequestStatus.Draft) await Do<RequisitionManagement>(async s => await s.SubmitAsync(id, await s.ActorAsync(principal), detail.Version));
        if (goal == PurchaseRequestStatus.Submitted) return id;
        var officer = await Principal("demo.officer");
        foreach (var decision in new[] { PurchaseRequestStatus.UnderReview, goal }) {
            await Do<RequisitionManagement>(async s => {
                var actor = await s.ActorAsync(officer); var current = await s.DetailsAsync(id, actor);
                if (current.Request.Status == decision || current.Request.Status is PurchaseRequestStatus.Approved or PurchaseRequestStatus.Returned) return;
                await s.ReviewAsync(id, actor, new RequisitionActionForm { Version = current.Version, Decision = decision,
                    Comments = decision == PurchaseRequestStatus.Returned ? "DEMO - Please clarify the warranty and delivery requirements." : "DEMO - Reviewed for sample workflow." });
            });
        }
        return id;
    }
    private async Task<TenderDetails> TenderDetails(Guid id, string actor = "demo.officer") =>
        await With<TenderManagement, TenderDetails>(async s => await s.DetailsAsync(id, await s.ActorAsync(await Principal(actor))));
    private async Task<Guid> Tender(string title, Guid source, Guid[] suppliers, bool draft, bool historical)
    {
        clock.Now = historical ? state.BaseTime.AddDays(-7) : state.BaseTime;
        var name = "DEMO - " + title;
        var found = await With<ApplicationDbContext, Tender?>(db => db.Tenders.AsNoTracking().SingleOrDefaultAsync(x => x.Title == name));
        var start = clock.Now.AddHours(-1); var deadline = historical ? state.BaseTime.AddDays(-1) : state.BaseTime.AddDays(21);
        var id = found?.Id ?? await With<TenderManagement, Guid>(async s => await s.SaveAsync(null, await s.ActorAsync(await Principal("demo.officer")), new TenderForm {
            RequisitionId = source, Title = name, Description = "DEMO - Illustrative tender linked to an approved purchase requisition.",
            Terms = "DEMO - JOD pricing, complete quantities, documented warranty and delivery. Sample data only.",
            Start = TenderTime.Local(start), Deadline = TenderTime.Local(deadline)
        }));
        if (draft || found?.Status == TenderStatus.Closed) return id;
        var detail = await TenderDetails(id);
        if (detail.Criteria.Count == 0) await Do<TenderManagement>(async s => await s.SaveCriteriaAsync(id, await s.ActorAsync(await Principal("demo.officer")), new CriteriaForm {
            Version = detail.Version, Items = [new CriterionForm { Name = "DEMO - Technical compliance", Description = "Sample technical criterion", Kind = EvaluationKind.Technical, Weight = 60m },
                new CriterionForm { Name = "DEMO - Commercial value", Description = "Sample commercial criterion", Kind = EvaluationKind.Financial, Weight = 40m }]
        }));
        detail = await TenderDetails(id);
        if (!TenderManagement.Approved(detail.Criteria)) await Do<TenderManagement>(async s => await s.ApproveCriteriaAsync(id, await s.ActorAsync(await Principal("demo.manager")), detail.Version));
        detail = await TenderDetails(id);
        if (detail.Tender.Status == TenderStatus.Draft) await Do<TenderManagement>(async s => await s.ChangeStatusAsync(id, await s.ActorAsync(await Principal("demo.officer")), detail.Version, TenderStatus.Published));
        await Do<TenderManagement>(async s => {
            var actor = await s.ActorAsync(await Principal("demo.officer")); var invitations = await s.InvitationsAsync(id, actor);
            var missing = suppliers.Except(invitations.Invitations.Select(x => x.Supplier.Id)).ToList();
            if (missing.Count > 0) await s.InviteAsync(id, actor, new InviteForm { Version = invitations.Details.Version, SupplierIds = missing });
        });
        detail = await TenderDetails(id);
        if (detail.Tender.Status == TenderStatus.Published) await Do<TenderManagement>(async s => await s.ChangeStatusAsync(id, await s.ActorAsync(await Principal("demo.officer")), detail.Version, TenderStatus.Open));
        return id;
    }
    private async Task Bid(Guid tenderId, string supplier, decimal price, bool submit)
    {
        var principal = await Principal(supplier);
        var userId = (await User(supplier))!.Id;
        await Do<TenderManagement>(async s => {
            var actor = await s.ActorAsync(principal); var row = (await s.InboxAsync(actor)).Single(x => x.Tender.Id == tenderId);
            if (row.Invitation.Status == InvitationStatus.Pending) await s.RespondAsync(row.Invitation.Id, actor, TenderManagement.InvitationVersion(row), InvitationStatus.Accepted);
        });
        var existing = await With<ApplicationDbContext, Bid?>(db => (from b in db.Bids.AsNoTracking() join link in db.TenderSuppliers on b.TenderSupplierId equals link.Id
            join profile in db.SupplierProfiles on link.SupplierProfileId equals profile.Id where b.TenderId == tenderId && profile.UserId == userId select b).SingleOrDefaultAsync());
        if (existing?.SubmittedAt is not null) return;
        var detail = await TenderDetails(tenderId);
        var id = existing?.Id ?? await With<BidManagement, Guid>(s => s.CreateDraftAsync(principal, tenderId, new BidDraftForm {
            DeliveryTimeframe = "DEMO - Delivery within 14 business days", TechnicalDetails = "DEMO - Complete offer meeting the illustrative item requirements.",
            PaymentTerms = "DEMO - Payment within 30 days after acceptance", OfferValidUntil = DateOnly.FromDateTime(state.BaseTime.UtcDateTime.AddDays(90)),
            Items = detail.Items.Select((item, i) => new BidLineForm { ItemId = item.Id, UnitPrice = i == 0 ? price : 75m }).ToList(),
            Charges = [new BidChargeForm { CostType = "DEMO delivery fee", Amount = 25m }]
        }));
        var bid = await With<BidManagement, BidDetails>(s => s.DetailsAsync(principal, id));
        if (bid.Snapshot.Documents.Count == 0) await With<BidDocumentManagement, Guid>(s => s.UploadAsync(principal, id, bid.Version, "Technical Proposal", Document("technical-proposal.pdf", "DEMO - Technical proposal for " + supplier)));
        if (submit) {
            bid = await With<BidManagement, BidDetails>(s => s.DetailsAsync(principal, id));
            await Do<BidManagement>(s => s.SubmitAsync(principal, id, bid.Version));
        }
    }
    private async Task Populate()
    {
        var baseline = await With<ApplicationDbContext, string>(async db => JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(x => !Accounts.Contains(x.UserName!)).OrderBy(x => x.Id).ToListAsync()));
        var procurement = await Department("DEMO - Procurement");
        var it = await Department("DEMO - Information Technology");
        var operations = await Department("DEMO - Operations");
        await Employee("demo.officer", procurement, ApplicationRoles.SourcingOfficer);
        await Employee("demo.manager", procurement, ApplicationRoles.SourcingManager);
        await Employee("demo.requester.it", it, ApplicationRoles.RequestingDepartment);
        await Employee("demo.requester.operations", operations, ApplicationRoles.RequestingDepartment);
        await Employee("demo.evaluator.department", it, ApplicationRoles.EvaluationCommittee);
        await Employee("demo.evaluator.finance", procurement, ApplicationRoles.EvaluationCommittee);
        await Employee("demo.evaluator.technical", it, ApplicationRoles.EvaluationCommittee);
        var admin = await With<ApplicationDbContext, string>(async db => (await db.Users.AsNoTracking().SingleAsync(x => x.NormalizedUserName == "ADMIN")).Id);
        var network = await Supplier("demo.supplier.network", "DEMO Network Solutions", true, admin);
        var office = await Supplier("demo.supplier.office", "DEMO Office Supplies", true, admin);
        var facilities = await Supplier("demo.supplier.facilities", "DEMO Facilities Services", true, admin);
        await Supplier("demo.supplier.pending", "DEMO Pending Supplier", false, admin);
        clock.Now = state.BaseTime.AddDays(-8);
        await Request("Office furniture planning", "demo.requester.operations", "Ergonomic office chair", "Low", PurchaseRequestStatus.Draft);
        await Request("Printer replacement", "demo.requester.it", "Network laser printer", "Medium", PurchaseRequestStatus.Submitted);
        await Request("Workspace maintenance", "demo.requester.operations", "Preventive maintenance service", "High", PurchaseRequestStatus.Returned);
        await Request("Staff workstation upgrade", "demo.requester.it", "Business workstation", "High", PurchaseRequestStatus.Approved);
        var source1 = await Request("Network infrastructure renewal", "demo.requester.it", "Managed network switch", "High", PurchaseRequestStatus.Approved);
        var source2 = await Request("Office equipment procurement", "demo.requester.operations", "Office equipment bundle", "Medium", PurchaseRequestStatus.Approved);
        var source3 = await Request("IT support services", "demo.requester.it", "Annual technical support subscription", "Urgent", PurchaseRequestStatus.Approved);
        var historical = await Tender("Network infrastructure tender", source1, [network, office, facilities], false, true);
        if ((await TenderDetails(historical)).Tender.Status != TenderStatus.Closed) {
            await Bid(historical, "demo.supplier.network", 145m, true);
            await Bid(historical, "demo.supplier.office", 158.5m, true);
            await Bid(historical, "demo.supplier.facilities", 162m, true);
            clock.Now = state.BaseTime;
            var detail = await TenderDetails(historical);
            await Do<TenderManagement>(async s => await s.ChangeStatusAsync(historical, await s.ActorAsync(await Principal("demo.officer")), detail.Version, TenderStatus.Closed));
        }
        clock.Now = state.BaseTime;
        var open = await Tender("Office equipment tender", source2, [network, office, facilities], false, false);
        await Bid(open, "demo.supplier.office", 89m, true);
        await Bid(open, "demo.supplier.network", 92.5m, false);
        await Tender("Technical support tender draft", source3, [], true, false);
        var manager = await Principal("demo.manager");
        var committee = await With<CommitteeManagement, CommitteePage>(s => s.PageAsync(manager, historical));
        if (committee.History.Count == 0) {
            var managerId = (await User("demo.manager"))!.Id;
            var departmentId = (await User("demo.evaluator.department"))!.Id;
            var financeId = (await User("demo.evaluator.finance"))!.Id;
            var technicalId = (await User("demo.evaluator.technical"))!.Id;
            await Do<CommitteeManagement>(s => s.SaveAsync(manager, historical, new CommitteeForm {
                Version = committee.Form.Version, ManagerUserId = managerId, DepartmentRepresentativeId = departmentId,
                FinancialRepresentativeId = financeId, TechnicalRepresentativeId = technicalId,
                Reason = "DEMO - Initial illustrative committee appointment"
            }));
        }
        var after = await With<ApplicationDbContext, string>(async db => JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(x => !Accounts.Contains(x.UserName!)).OrderBy(x => x.Id).ToListAsync()));
        if (baseline != after) throw new Exception("Existing accounts changed during population. Review before continuing.");
        await With<ApplicationDbContext, bool>(async db => {
            Console.WriteLine($"Verified demo rows: departments={await db.Departments.CountAsync(x => x.Name.StartsWith("DEMO - "))}, users={await db.Users.CountAsync(x => Accounts.Contains(x.UserName!))}, suppliers={await db.SupplierProfiles.CountAsync(x => x.CompanyName.StartsWith("DEMO "))}, requests={await db.PurchaseRequisitions.CountAsync(x => x.Title.StartsWith("DEMO - "))}, tenders={await db.Tenders.CountAsync(x => x.Title.StartsWith("DEMO - "))}, bids={await db.Bids.CountAsync(x => db.Tenders.Any(t => t.Id == x.TenderId && t.Title.StartsWith("DEMO - ")))}");
            return true;
        });
    }
    private static IFormFile Document(string name, string text)
    {
        // A valid one-page PDF, visibly marked as synthetic evidence.
        var content = "BT /F1 16 Tf 50 780 Td (DEMO - NOT AN OFFICIAL DOCUMENT) Tj 0 -40 Td /F1 10 Tf (" + text.Replace("(", "[").Replace(")", "]") + ") Tj ET";
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream" };
        var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString())); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString()); pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10") + " 00000 n \n");
        pdf.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        var bytes = Encoding.ASCII.GetBytes(pdf.ToString());
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", name);
    }
    private static void SavePrivate(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        if (OperatingSystem.IsWindows()) {
            var security = new FileSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }
        stream.SetLength(0); var bytes = Encoding.UTF8.GetBytes(text); stream.Write(bytes); stream.Flush(true);
    }
    private sealed class DemoState
    {
        public DateTimeOffset BaseTime { get; set; } = DateTimeOffset.UtcNow;
        public bool Completed { get; set; }
        public Dictionary<string, string> Passwords { get; set; } = [];
    }
    private sealed class DemoClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FilesEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DemoPopulation"; public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root; public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
