using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.Web.Security;

public interface IFirstAdminTransaction : IAsyncDisposable
{
    Task CommitAsync();
}

public interface IFirstAdminDatabase
{
    Task<bool> AnyUsersAsync();
    Task<IFirstAdminTransaction> BeginExclusiveAsync();
}

public sealed class FirstAdminDatabase(ApplicationDbContext database) : IFirstAdminDatabase
{
    public Task<bool> AnyUsersAsync() => database.Users.AsNoTracking().AnyAsync();

    public async Task<IFirstAdminTransaction> BeginExclusiveAsync()
    {
        var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // Also locks an empty table. This serializes competing bootstrap attempts
            // and prevents another user insert between the final check and commit.
            await database.Users.FromSqlRaw(
                "SELECT * FROM [dbo].[AspNetUsers] WITH (TABLOCKX, HOLDLOCK)").AsNoTracking().AnyAsync();
            return new Transaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IFirstAdminTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        // EF rolls back an uncommitted transaction when it is disposed.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
