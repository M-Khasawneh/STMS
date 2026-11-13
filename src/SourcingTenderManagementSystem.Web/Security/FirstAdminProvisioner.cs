using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SourcingTenderManagementSystem.Web.Security;

public sealed class FirstAdminProvisioner(
    UserManager<IdentityUser> users, RoleManager<IdentityRole> roles,
    IFirstAdminDatabase database, IFirstAdminConsole console, IOptions<IdentityOptions> options)
{
    public async Task<int> RunAsync()
    {
        try
        {
            if (!console.IsInteractive)
                return Fail("Provisioning requires a private interactive terminal; redirected input/output is refused.");
            if (!await roles.RoleExistsAsync(ApplicationRoles.Admin))
                return Fail("The existing Admin role is missing. Provisioning refused.");
            if (await database.AnyUsersAsync())
                return Fail("Users already exist. First Admin provisioning is unavailable.");

            console.WriteLine("Provisioning the first account with username 'admin' and only the Admin role.");
            string? email = null;
            if (options.Value.User.RequireUniqueEmail || options.Value.SignIn.RequireConfirmedEmail ||
                options.Value.SignIn.RequireConfirmedAccount)
            {
                email = console.ReadLine("Email: ").Trim();
                if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
                    return Fail("A valid email address is required. No account was created.");
            }

            console.WriteLine("Enter the password privately. It must meet the existing Identity password policy. Press Escape to cancel.");
            var password = console.ReadPassword("Password: ");
            var confirmation = console.ReadPassword("Confirm password: ");
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
                return Fail("Passwords do not match. No account was created.");

            await using (var transaction = await database.BeginExclusiveAsync())
            {
                // Recheck after taking the lock; prompts never hold a database lock.
                if (await database.AnyUsersAsync())
                    return Fail("Users already exist. First Admin provisioning is unavailable.");
                if (!await roles.RoleExistsAsync(ApplicationRoles.Admin))
                    return Fail("The Admin role is no longer available. Provisioning refused.");

                var user = new IdentityUser("admin") { Email = email };
                if (!(await users.CreateAsync(user, password)).Succeeded)
                    return Fail("Identity rejected the account or password. Check the configured validation requirements. No account was created.");
                if (!(await users.AddToRoleAsync(user, ApplicationRoles.Admin)).Succeeded)
                    return Fail("Admin role assignment failed. Account creation has been rolled back.");

                var assignedRoles = await users.GetRolesAsync(user);
                if (assignedRoles.Count != 1 || assignedRoles[0] != ApplicationRoles.Admin)
                    return Fail("Unexpected role membership. Account creation has been rolled back.");
                await transaction.CommitAsync();
            }
            console.WriteLine("First Admin provisioned successfully. Username: admin. Role: Admin. Website was not started.");
            if (options.Value.SignIn.RequireConfirmedEmail || options.Value.SignIn.RequireConfirmedAccount)
                console.WriteLine("The account still requires the configured confirmation process before login; confirmation was not bypassed.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            return Fail("Provisioning canceled. Any uncommitted changes were rolled back.");
        }
        catch (Exception)
        {
            // Never print raw exceptions or validator descriptions: they can contain input.
            return Fail("Provisioning failed. Any uncommitted changes were rolled back. Inspect account state before retrying.");
        }
    }

    private int Fail(string message)
    {
        console.WriteLine(message);
        return 1;
    }
}
