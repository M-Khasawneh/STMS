using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Models.Suppliers;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed class SupplierRegistration(ApplicationDbContext database, UserManager<IdentityUser> users,
    IPrivateSupplierFiles files, ILogger<SupplierRegistration> logger)
{
    public async Task<IdentityResult> RegisterAsync(SupplierRegistrationViewModel model, CancellationToken cancellationToken)
    {
        // Validate everything before creating an Identity user or touching storage.
        var validation = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new(model), validation, true))
            return Failure("Complete all required fields with valid information.");
        var uploads = new List<(string Type, ValidatedSupplierFile File)>();
        foreach (var (type, file) in new[] { ("Company Registration Certificate", model.CompanyCertificate), ("Business / Trade License", model.TradeLicense), ("Tax Registration Certificate", model.TaxCertificate) })
        {
            try { uploads.Add((type, await SupplierUploadValidation.ReadAsync(file, cancellationToken))); }
            catch (InvalidDataException exception) { return Failure(type + ": " + exception.Message); }
        }
        // Refuse writes before deployment of the unique indexes; application pre-checks alone are not race-safe.
        if (!(await database.Database.GetAppliedMigrationsAsync(cancellationToken)).Contains("20261113120000_SupplierRegistrationUniqueness"))
            return Failure("Supplier registration is temporarily unavailable while its database update awaits approval.");
        var registration = model.RegistrationNumber.Trim().ToUpperInvariant();
        var tax = model.TaxNumber.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(registration) || string.IsNullOrWhiteSpace(tax)) return Failure("Registration and tax numbers are required.");
        var saved = new List<string>();
        var committed = false;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await database.SupplierProfiles.AnyAsync(x => x.RegistrationNumber == registration || x.TaxNumber == tax, cancellationToken))
                return Failure("A company with this registration or tax number is already registered.");
            var user = new IdentityUser(model.UserName.Trim()) { Email = model.ContactEmail.Trim(), PhoneNumber = model.ContactPhone.Trim() };
            var result = await users.CreateAsync(user, model.Password);
            if (!result.Succeeded) return result;
            result = await users.AddToRoleAsync(user, ApplicationRoles.Supplier);
            if (!result.Succeeded) return result;
            var supplier = new SupplierProfile { Id = Guid.NewGuid(), UserId = user.Id, CompanyName = model.CompanyName.Trim(),
                RegistrationNumber = registration, TaxNumber = tax, ContactName = model.ContactName.Trim(), ContactEmail = model.ContactEmail.Trim(),
                ContactPhone = model.ContactPhone.Trim(), Status = SupplierStatus.Pending };
            database.SupplierProfiles.Add(supplier);
            foreach (var upload in uploads)
            {
                var reference = await files.SaveAsync(upload.File, cancellationToken);
                saved.Add(reference);
                database.SupplierDocuments.Add(new SupplierDocument { Id = Guid.NewGuid(), SupplierProfileId = supplier.Id,
                    DocumentType = upload.Type, OriginalName = upload.File.OriginalName, ContentType = upload.File.ContentType,
                    Size = upload.File.Bytes.LongLength, StorageReference = reference, UploadedAt = DateTimeOffset.UtcNow });
            }
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return IdentityResult.Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return Failure("The username, company registration or tax number is already registered."); }
        catch (IOException) { return Failure("Documents could not be stored. No registration was saved. Please try again."); }
        catch (UnauthorizedAccessException) { return Failure("Document storage is unavailable. No registration was saved."); }
        finally
        {
            if (!committed)
            {
                foreach (var reference in saved)
                {
                    try { files.Delete(reference); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    { logger.LogError("A private supplier upload requires cleanup: {Reference}", reference); }
                }
                // Also remove rolled-back tracked entities before another operation in this scope.
                database.ChangeTracker.Clear();
            }
        }
    }
    private static IdentityResult Failure(string message) => IdentityResult.Failed(new IdentityError { Code = "SupplierRegistration", Description = message });
}
