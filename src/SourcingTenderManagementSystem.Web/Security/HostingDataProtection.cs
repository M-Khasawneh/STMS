using Microsoft.AspNetCore.DataProtection;

namespace SourcingTenderManagementSystem.Web.Security;

public static class HostingDataProtection
{
    // Opt in only on a single Windows host whose worker has no user profile/registry key store.
    public static IServiceCollection AddHostingDataProtection(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("DataProtection:PersistWindowsKeys")) return services;
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Persistent Windows keys require Windows DPAPI; plaintext key storage is not permitted.");

        var directory = new DirectoryInfo(Path.Combine(environment.ContentRootPath, "App_Data", "DataProtection-Keys"));
        directory.Create();
        services.AddDataProtection()
            .PersistKeysToFileSystem(directory)
            .ProtectKeysWithDpapi(protectToLocalMachine: true);
        return services;
    }
}
