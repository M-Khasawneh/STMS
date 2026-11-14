using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using System.Text.Json;

// Diagnostic process only; never changes application settings or authentication.
if (args.Contains("--managed-networking"))
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

var failures = new List<string>();
if (args.Contains("--inspect-clarification-database") || args.Contains("--apply-clarification-migration"))
    return await ClarificationSchemaChecks.RunAsync(args.Contains("--apply-clarification-migration"));
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=SchemaVerificationOnly;Integrated Security=True;TrustServerCertificate=True")
    .Options;
using var context = new ApplicationDbContext(options);
var model = context.GetService<IDesignTimeModel>().Model;
var expectedTables = new[] { "Departments", "SupplierProfiles", "SupplierDocuments", "PurchaseRequisitions", "PurchaseRequisitionItems", "PurchaseRequisitionReviews", "Tenders", "TenderSuppliers", "Invitations", "Bids", "BidItems", "BidDocuments", "EvaluationCriteria", "TechnicalEvaluations", "FinancialEvaluations", "BidCostComponents", "EvaluationReports", "TenderApprovals", "CommitteeAssignments", "CommitteeMembers", "CommitteeVotes", "ComplianceReviews", "ClarificationRequests", "ClarificationResponses", "Notifications", "AuditLogs", "AiBidAnalyses", "TenderAwards", "TenderPurchaseRequisitions", "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims", "AspNetRoleClaims", "AspNetUserLogins", "AspNetUserTokens" };
var actualTables = model.GetEntityTypes().Select(x => x.GetTableName()).ToHashSet();
Check(actualTables.Contains("ClarificationAttachments"), "Missing clarification attachment table");
var attachmentModel = model.FindEntityType(typeof(ClarificationAttachment))!;
Check(attachmentModel.GetForeignKeys().Single().PrincipalEntityType.ClrType == typeof(ClarificationResponse), "Attachment must belong to a clarification response");
Check(attachmentModel.FindProperty(nameof(ClarificationAttachment.Sha256))!.GetMaxLength() == 64, "Attachment hash precision");
Check(attachmentModel.GetCheckConstraints().Any(x => x.Name == "CK_ClarificationAttachments_Size"), "Attachment size constraint missing");
foreach (var table in expectedTables)
    Check(actualTables.Contains(table), $"Missing required table: {table}");

foreach (var entity in model.GetEntityTypes()
             .Where(x => x.ClrType.Namespace == typeof(Tender).Namespace))
{
    Check(entity.FindPrimaryKey() is not null, $"Missing primary key: {entity.Name}");
    Check(entity.GetProperties().All(x => !x.IsShadowProperty()), $"Unexpected shadow property: {entity.Name}");
    foreach (var fk in entity.GetForeignKeys())
        Check(fk.DeleteBehavior == DeleteBehavior.NoAction, $"Destructive cascade on {entity.Name}");
    foreach (var property in entity.GetProperties().Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)))
        Check(property.GetPrecision() == 18 && property.GetScale() == 6, $"Inexact decimal mapping: {entity.Name}.{property.Name}");
    foreach (var fk in entity.GetForeignKeys().Where(x => x.PrincipalEntityType.ClrType == typeof(Bid)))
        if (entity.FindProperty("TenderId")?.ClrType == typeof(Guid))
            Check(fk.Properties.Any(x => x.Name == "TenderId"), $"Unscoped bid reference: {entity.Name}");
}

foreach (var type in new[] { typeof(TechnicalEvaluation), typeof(FinancialEvaluation) })
{
    var entity = model.FindEntityType(type)!;
    Check(entity.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(EvaluationCriterion)
        && x.Properties.Any(p => p.Name == "TenderId")), $"Unscoped criterion: {type.Name}");
}
Check(model.FindEntityType(typeof(ComplianceReview))!.GetCheckConstraints()
    .Any(x => x.Name == "CK_ComplianceReviews_NonCompliantReason"), "Noncompliant reason constraint missing");
Check(model.FindEntityType(typeof(ClarificationRequest))!.GetCheckConstraints()
    .Any(x => x.Name == "CK_ClarificationRequests_OneRelatedRecord"), "Clarification record integrity missing");
foreach (var type in new[] { typeof(Bid), typeof(TenderAward) })
    Check(model.FindEntityType(type)!.GetIndexes().All(x => !x.IsUnique),
        $"Unspecified business uniqueness imposed on {type.Name}");

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<ApplicationDbContext>(x => x.UseSqlServer(
    "Server=localhost;Database=SchemaVerificationOnly;Integrated Security=True;TrustServerCertificate=True"));
services.AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
using (var provider = services.BuildServiceProvider())
using (var scope = provider.CreateScope())
{
    var userStore = scope.ServiceProvider.GetRequiredService<IUserStore<IdentityUser>>();
    Check(userStore.GetType().Assembly == typeof(Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext).Assembly
        && userStore.GetType().GenericTypeArguments.Contains(typeof(ApplicationDbContext)),
        "Identity user store is not the EF context");
    Check(scope.ServiceProvider.GetRequiredService<IRoleStore<IdentityRole>>() is not null,
        "Identity role store missing");
}

var migrations = context.GetService<IMigrationsAssembly>();
if (migrations.Migrations.Count > 0)
{
    foreach (var migrationType in migrations.Migrations.Values.Take(1))
    {
        var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);
        Check(migration.UpOperations.All(x => x is CreateTableOperation or CreateIndexOperation),
            "Initial migration contains an operation other than table/index creation");
        foreach (var table in expectedTables)
            Check(migration.UpOperations.OfType<CreateTableOperation>().Any(x => x.Name == table),
                $"Migration omits required table: {table}");
    }
    var assignment = migrations.CreateMigration(migrations.Migrations.Single(x => x.Key.EndsWith("_EmployeeDepartmentAssignment")).Value, context.Database.ProviderName!);
    Check(assignment.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).SequenceEqual(new[] { "EmployeeDepartments" }), "Department migration must add only EmployeeDepartments");
    Check(assignment.UpOperations.All(x => x is CreateTableOperation or CreateIndexOperation or SqlOperation), "Unexpected department migration operation");
    var mapping = model.FindEntityType(typeof(EmployeeDepartment))!;
    Check(mapping.FindPrimaryKey()!.Properties.Select(x => x.Name).SequenceEqual(new[] { "UserId" }), "Employee must have at most one department");
    Check(!mapping.FindProperty("DepartmentId")!.IsNullable && mapping.GetForeignKeys().Count() == 2, "Department assignment must reference an existing user and department");
    if (args.Contains("--write-department-script"))
        File.WriteAllText("docs/employee-department-migration.sql", context.GetService<IMigrator>().GenerateScript(
            "20261112224814_InitialIdentityAndProcurement", "20261113001222_EmployeeDepartmentAssignment"));
    var supplier = model.FindEntityType(typeof(SupplierProfile))!;
    foreach (var column in new[] { "UserId", "RegistrationNumber", "TaxNumber" })
        Check(supplier.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { column })), "Missing approved supplier uniqueness: " + column);
    if (args.Contains("--write-supplier-script"))
        File.WriteAllText("docs/supplier-registration-migration.sql", context.GetService<IMigrator>().GenerateScript(
            "20261113001222_EmployeeDepartmentAssignment", "20261113120000_SupplierRegistrationUniqueness"));
    foreach (var (type, columns) in new[] { (typeof(TenderPurchaseRequisition), new[] { "TenderId" }), (typeof(TenderPurchaseRequisition), new[] { "PurchaseRequisitionId" }), (typeof(TenderSupplier), new[] { "TenderId", "SupplierProfileId" }), (typeof(Invitation), new[] { "TenderSupplierId" }) })
        Check(model.FindEntityType(type)!.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(columns)), "Missing tender/invitation uniqueness: " + type.Name + ":" + string.Join(",",columns));
    if(args.Contains("--write-tender-script"))
        File.WriteAllText("docs/tender-invitation-migration.sql", context.GetService<IMigrator>().GenerateScript("20261113120000_SupplierRegistrationUniqueness", "20261113180000_TenderInvitationIntegrity"));
    Check(!context.Database.HasPendingModelChanges(), "Model differs from migration snapshot");
    var clarification = migrations.CreateMigration(migrations.Migrations.Single(x => x.Key.EndsWith("_ClarificationAttachments")).Value, context.Database.ProviderName!);
    Check(clarification.UpOperations.All(x => x is CreateTableOperation or CreateIndexOperation), "Clarification migration alters existing schema");
    Check(clarification.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).SequenceEqual(new[] { "ClarificationAttachments" }), "Clarification migration must add only its attachment table");
    var sql = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
    Check(!sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase), "Forward migration drops tables");
    Console.WriteLine($"Migrations verified: {migrations.Migrations.Count}");
}
else if (!args.Contains("--allow-no-migration"))
{
    failures.Add("No migration has been generated.");
}

if (failures.Count > 0)
{
    foreach (var failure in failures) Console.Error.WriteLine($"FAIL: {failure}");
    return 1;
}
Console.WriteLine($"PASS: model, keys, constrained relationships, decimal storage, non-destructive deletes and Identity stores ({expectedTables.Length + 1} required tables).");

if (args.Contains("--inspect-database"))
{
    // Read-only inspection using the configured credentials. Never creates schema or data.
    var settingsPath = Path.Combine("src", "SourcingTenderManagementSystem.Web", "appsettings.json");
    using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    var connectionString = settings.RootElement.GetProperty("ConnectionStrings")
        .GetProperty("DefaultConnection").GetString();
    var configuration = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
    Console.WriteLine($"Inspecting {configuration.DataSource}/{configuration.InitialCatalog}; IntegratedSecurity={configuration.IntegratedSecurity}");
    try
    {
        using var connection = new SqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
            SELECT s.name AS SchemaName, t.name AS TableName
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name;
            IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
                SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
            SELECT fk.name AS ForeignKeyName, OBJECT_NAME(fk.parent_object_id) AS TableName,
                   fk.is_disabled, fk.is_not_trusted FROM sys.foreign_keys fk ORDER BY fk.name;
            SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints ORDER BY name;
            """;
        using var reader = command.ExecuteReader();
        do
        {
            while (reader.Read())
                Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount)
                    .Select(i => $"{reader.GetName(i)}={reader.GetValue(i)}")));
        } while (reader.NextResult());
        Console.WriteLine("Database read-only inspection succeeded.");
    }
    catch (SqlException exception)
    {
        Console.Error.WriteLine($"DATABASE BLOCKED: {exception.Message}");
        return 2;
    }
}
return 0;

void Check(bool condition, string message)
{
    if (!condition) failures.Add(message);
}
