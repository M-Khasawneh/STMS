using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed class SupplierReview(ApplicationDbContext database, UserManager<IdentityUser> users)
{
    public async Task<string?> DecideAsync(Guid id, SupplierStatus expected, SupplierStatus decision, string? reason, string adminId, CancellationToken cancellationToken)
    {
        if (decision is not (SupplierStatus.Approved or SupplierStatus.Rejected or SupplierStatus.Suspended)) return "Select a valid decision.";
        if (decision != SupplierStatus.Approved && string.IsNullOrWhiteSpace(reason)) return "A reason is required for rejection or suspension.";
        if (reason?.Length > 2000) return "Reason cannot exceed 2000 characters.";
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Lock the profile during review; stale submissions cannot overwrite a later review.
        var supplier = await database.SupplierProfiles.FromSqlInterpolated($"SELECT * FROM dbo.SupplierProfiles WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(cancellationToken);
        if (supplier is null) return "Supplier is unavailable.";
        if (supplier.Status != expected) return "This application has changed. Reload before reviewing it.";
        if (!(expected == SupplierStatus.Pending && decision is SupplierStatus.Approved or SupplierStatus.Rejected) &&
            !(expected == SupplierStatus.Approved && decision == SupplierStatus.Suspended)) return "This decision is not available for the current status.";
        var user = await users.FindByIdAsync(supplier.UserId);
        if (user is null || !(await users.GetRolesAsync(user)).SequenceEqual(new[] { ApplicationRoles.Supplier })) return "Supplier account roles require separate review.";
        if (decision == SupplierStatus.Approved)
        {
            var documents = await database.SupplierDocuments.Where(x => x.SupplierProfileId == id).Select(x => x.DocumentType).ToListAsync(cancellationToken);
            if (!new[] { "Company Registration Certificate", "Business / Trade License", "Tax Registration Certificate" }.All(documents.Contains))
                return "All three mandatory documents are required for approval.";
        }
        supplier.Status = decision;
        supplier.ReviewReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        supplier.ReviewedByUserId = adminId;
        supplier.ReviewedAt = DateTimeOffset.UtcNow;
        var stamp = await users.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return "Account access could not be updated.";
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }
}
