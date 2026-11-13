using Microsoft.AspNetCore.Identity;

namespace SourcingTenderManagementSystem.Web.Security;

public static class RoleInitializer
{
    public static async Task InitializeAsync(RoleManager<IdentityRole> manager)
    {
        foreach (var role in ApplicationRoles.All)
        {
            if (await manager.RoleExistsAsync(role)) continue;
            var result = await manager.CreateAsync(new IdentityRole(role));
            // Another initializer may have created the same normalized role concurrently.
            if (!result.Succeeded && !await manager.RoleExistsAsync(role))
                throw new InvalidOperationException($"Role initialization failed for {role}: " +
                    string.Join("; ", result.Errors.Select(error => error.Code)));
        }
    }
}
