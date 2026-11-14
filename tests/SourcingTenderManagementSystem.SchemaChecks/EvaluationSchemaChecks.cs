using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SourcingTenderManagementSystem.Web.Data;

internal static class EvaluationSchemaChecks
{
    public static async Task<int> ApplyAsync()
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync("src/SourcingTenderManagementSystem.Web/appsettings.json"));
        var connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (!pending.SequenceEqual(new[] { "20261114190000_EvaluationConfirmation" }) || db.Database.HasPendingModelChanges()) throw new Exception("Unexpected migration state. Stopped.");
        if (await db.EvaluationCriteria.AnyAsync(x => x.MaxScore != null && x.MaxScore != 100)) throw new Exception("Existing criterion uses a different score scale. Stopped.");
        var technical = await db.TechnicalEvaluations.CountAsync(); var financial = await db.FinancialEvaluations.CountAsync();
        var users = await db.Users.CountAsync(); var bids = await db.Bids.CountAsync(); var tenders = await db.Tenders.CountAsync();
        Console.WriteLine($"Read-only preflight: technical rows={technical}, financial rows={financial}, users={users}, bids={bids}, tenders={tenders}.");
        await db.GetService<IMigrator>().MigrateAsync("20261114190000_EvaluationConfirmation");
        if (await db.TechnicalEvaluations.CountAsync() != technical || await db.FinancialEvaluations.CountAsync() != financial ||
            await db.Users.CountAsync() != users || await db.Bids.CountAsync() != bids || await db.Tenders.CountAsync() != tenders) throw new Exception("Record counts changed. Review concurrent activity.");
        Console.WriteLine("PASS: approved nullable membership/confirmation columns, scoped keys and uniqueness applied. Existing record counts preserved.");
        return 0;
    }
}
