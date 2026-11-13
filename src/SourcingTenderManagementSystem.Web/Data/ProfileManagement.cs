using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SourcingTenderManagementSystem.Web.Models.Account;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IProfileTransactions { Task<IEmployeeTransaction> BeginAsync(CancellationToken ct); }
public sealed class ProfileTransactions(ApplicationDbContext db) : IProfileTransactions
{
    public async Task<IEmployeeTransaction> BeginAsync(CancellationToken ct) => new Transaction(await db.Database.BeginTransactionAsync(ct), db);
    private sealed class Transaction(IDbContextTransaction transaction, ApplicationDbContext db) : IEmployeeTransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        public async ValueTask DisposeAsync() { await transaction.DisposeAsync(); db.ChangeTracker.Clear(); }
    }
}
public sealed class ProfileManagement(UserManager<IdentityUser> users, IEmployeeEligibility eligibility, IProfileTransactions transactions)
{
    public async Task<IdentityUser> UserAsync(ClaimsPrincipal principal)
    {
        var user = principal.Identity?.IsAuthenticated == true ? await users.GetUserAsync(principal) : null;
        if (user is null || await users.IsLockedOutAsync(user) || !await eligibility.IsEligibleAsync(user)) throw new UnauthorizedAccessException();
        return user;
    }
    public async Task<ProfilePage> ReadAsync(ClaimsPrincipal principal)
    {
        var user = await UserAsync(principal);
        return new() { UserName = user.UserName ?? "", Contact = new() { Email = user.Email, PhoneNumber = user.PhoneNumber } };
    }
    public async Task<IdentityResult> ContactAsync(ClaimsPrincipal principal, ProfileContactForm form, CancellationToken ct)
    {
        await UserAsync(principal);
        var validation = Validate(form); if (!validation.Succeeded) return validation;
        await using var tx = await transactions.BeginAsync(ct);
        var user = await UserAsync(principal);
        static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var email = Clean(form.Email); var phone = Clean(form.PhoneNumber);
        if (email != user.Email) { var result = await users.SetEmailAsync(user, email); if (!result.Succeeded) return result; }
        if (phone != user.PhoneNumber) { var result = await users.SetPhoneNumberAsync(user, phone); if (!result.Succeeded) return result; }
        var stamp = await users.UpdateSecurityStampAsync(user); if (!stamp.Succeeded) return stamp;
        await tx.CommitAsync(); return IdentityResult.Success;
    }
    public async Task<IdentityResult> PasswordAsync(ClaimsPrincipal principal, ProfilePasswordForm form, CancellationToken ct)
    {
        await UserAsync(principal);
        var validation = Validate(form); if (!validation.Succeeded) return validation;
        await using var tx = await transactions.BeginAsync(ct);
        var user = await UserAsync(principal);
        if (form.NewPassword != form.ConfirmPassword) return IdentityResult.Failed(new IdentityError { Description = "The new passwords do not match." });
        var result = await users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
        if (result.Succeeded) await tx.CommitAsync();
        return result;
    }
    private static IdentityResult Validate(object form)
    {
        var errors = new List<ValidationResult>();
        return Validator.TryValidateObject(form, new ValidationContext(form), errors, true) ? IdentityResult.Success :
            IdentityResult.Failed(errors.Select(x => new IdentityError { Description = x.ErrorMessage ?? "Invalid profile information." }).ToArray());
    }
}
