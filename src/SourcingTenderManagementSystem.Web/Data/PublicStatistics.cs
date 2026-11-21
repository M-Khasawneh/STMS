using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IPublicStatistics
{
    Task<PublicHomeViewModel> ReadAsync(CancellationToken cancellationToken);
}

// Aggregate counts only. No public record identifiers or supplier details are exposed.
public sealed class PublicStatistics(ApplicationDbContext database, ILogger<PublicStatistics> logger) : IPublicStatistics
{
    public async Task<PublicHomeViewModel> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(database.Database.GetConnectionString()))
        {
            logger.LogWarning("Public statistics are unavailable because the database connection is not configured.");
            return new(null, null, null);
        }
        try
        {
            // Sequential queries share the scoped DbContext; never query it concurrently.
            var open = await database.Tenders.CountAsync(x => x.Status == TenderStatus.Open, cancellationToken);
            var approved = await database.SupplierProfiles.CountAsync(x => x.Status == SupplierStatus.Approved, cancellationToken);
            var converted = await database.PurchaseRequisitions.CountAsync(x => x.Status == PurchaseRequestStatus.ConvertedToTender, cancellationToken);
            return new(open, approved, converted);
        }
        catch (Exception exception) when (exception is System.Data.Common.DbException or TimeoutException)
        {
            logger.LogWarning("Public statistics are temporarily unavailable.");
            return new(null, null, null);
        }
    }
}
