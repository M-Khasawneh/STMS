using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Security;

if (args.Length > 0 && args[0] == "--extract-bid-text") {
    Environment.ExitCode = await BidTextWorker.RunAsync(args); return;
}
if(args.Length==1 && args[0]=="--watermark-document"){Environment.ExitCode=await DocumentWatermarkWorker.RunAsync();return;}
var builder = WebApplication.CreateBuilder(args);

var provisionFirstAdmin = args.Contains("--provision-first-admin", StringComparer.Ordinal);
if (provisionFirstAdmin)
{
    // Keep credentials and exception details out of all configured log sinks.
    builder.Logging.ClearProviders();
    if (args.Length != 1)
    {
        Console.Error.WriteLine("Use --provision-first-admin alone. Credentials must be entered at the private console prompts.");
        Environment.ExitCode = 1;
        return;
    }
}

// Add services to the container.
builder.Services.AddControllersWithViews(options=>options.Filters.AddService<DocumentWatermarkFilter>());
builder.Services.AddScoped<DocumentWatermarkFilter>();
builder.Services.AddScoped<PrivateWatermarkedCopies>();
builder.Services.AddSingleton(new QrLinks(builder.Configuration["PublicSite:Origin"]??builder.Configuration["Passkeys:Origin"]??(builder.Environment.IsDevelopment()?"https://localhost:7162":null)));
builder.Services.AddPublicContact();
builder.Services.AddScoped<INotificationStore, NotificationStore>();
builder.Services.AddScoped<NotificationInbox>();
builder.Services.AddScoped<IWorkspaceDiscovery, WorkspaceDiscovery>();
builder.Services.AddScoped<ISupplierTenderPortal, SupplierTenderPortal>();
builder.Services.AddSingleton<NotificationWatchGate>();
builder.Services.AddScoped<IPublicStatistics, PublicStatistics>();
builder.Services.AddScoped<IAdminDashboard, AdminDashboard>();
builder.Services.AddScoped<IRoleDashboard, RoleDashboard>();
builder.Services.AddScoped<IProfileTransactions, ProfileTransactions>();
builder.Services.AddScoped<ProfileManagement>();
builder.Services.AddScoped<ProfilePictures>();
builder.Services.AddScoped<IWorkspacePresentation, WorkspacePresentation>();
builder.Services.AddScoped<IPrivateSupplierFiles, PrivateSupplierFiles>();
builder.Services.AddScoped<SupplierRegistration>();
builder.Services.AddScoped<SupplierReview>();
builder.Services.AddScoped<ISupplierExpiryStore, SupplierExpiryStore>();
builder.Services.AddScoped<SupplierDocumentExpiry>();
builder.Services.AddScoped<RequisitionManagement>();
builder.Services.AddScoped<TenderManagement>();
builder.Services.AddScoped<BidData>();
builder.Services.AddScoped<IBidData>(services => services.GetRequiredService<BidData>());
builder.Services.AddScoped<IBidDocumentData>(services => services.GetRequiredService<BidData>());
builder.Services.AddScoped<BidManagement>();
builder.Services.AddScoped<BidReviewPages>();
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSingleton<AiAnalysisGate>();
builder.Services.Configure<BidTextOptions>(builder.Configuration.GetSection("BidText"));
builder.Services.AddScoped<IBidTextExtractor,BidTextExtractor>();
builder.Services.AddScoped<IAiBidDocuments,AiBidDocuments>();
builder.Services.AddHttpClient<IOllamaBidClient,OllamaBidClient>(client => client.Timeout=TimeSpan.FromMinutes(3))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect=false,UseProxy=false });
builder.Services.AddScoped<IAiBidData,AiBidData>();
builder.Services.AddScoped<IAiBidManagement,AiBidManagement>();
builder.Services.AddScoped<IComplianceManagement, ComplianceManagement>();
builder.Services.AddScoped<IInternalClarifications, InternalClarificationManagement>();
builder.Services.AddScoped<IReporting, Reporting>();
builder.Services.AddScoped<CommitteeManagement>();
builder.Services.AddScoped<ITopCommitteeData, TopCommitteeData>();
builder.Services.AddScoped<ITopCommitteeManagement, TopCommitteeManagement>();
builder.Services.AddScoped<EvaluationManagement>();
builder.Services.AddScoped<IEvaluationManagement>(s => s.GetRequiredService<EvaluationManagement>());
builder.Services.AddScoped<IUnifiedReportManagement, UnifiedReportManagement>();
builder.Services.AddScoped<IPrivateBidFiles, PrivateBidFiles>();
builder.Services.AddScoped<IBidSubmissionDocuments, BidSubmissionDocuments>();
builder.Services.AddScoped<BidDocumentManagement>();
builder.Services.AddScoped<SupplierBidPages>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthenticationFoundation();
builder.Services.AddStmsPasskeys(new PasskeySite(builder.Configuration["Passkeys:Origin"] ?? (builder.Environment.IsDevelopment() ? "https://localhost:7162" : null)));
builder.Services.AddStmsFaceRecognition(builder.Configuration);
builder.Services.AddScoped<IDepartmentStore, DepartmentStore>();
builder.Services.AddScoped<IEmployeeData, EmployeeData>();
builder.Services.AddScoped<EmployeeManagement>();
builder.Services.AddScoped<IFirstAdminDatabase, FirstAdminDatabase>();
builder.Services.AddScoped<IFirstAdminConsole, FirstAdminConsole>();
builder.Services.AddScoped<FirstAdminProvisioner>();

var app = builder.Build();

if (provisionFirstAdmin)
{
    await using var scope = app.Services.CreateAsyncScope();
    Environment.ExitCode = await scope.ServiceProvider.GetRequiredService<FirstAdminProvisioner>().RunAsync();
    return;
}

// Explicit maintenance command only: normal web startup never changes database data.
if (args.Contains("--initialize-roles", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    await RoleInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    Console.WriteLine("Required roles initialized. No users or credentials created.");
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Only published wwwroot assets bypass cookie/database validation. Private document actions do not.
app.MapStaticAssets().ShortCircuit().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
