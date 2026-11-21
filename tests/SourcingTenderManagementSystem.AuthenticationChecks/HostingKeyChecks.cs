using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class HostingKeyChecks
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "stms-hosting-key-checks-" + Guid.NewGuid().ToString("N"));
        var environment = new TestEnvironment { ContentRootPath = root };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var checks = 0;
        void Check(bool passed, string message) { if (!passed) throw new Exception(message); checks++; }
        try
        {
            new ServiceCollection().AddHostingDataProtection(configuration, environment);
            Check(!Directory.Exists(root), "Default configuration must not change existing key storage.");
            configuration["DataProtection:PersistWindowsKeys"] = "true";
            if (!OperatingSystem.IsWindows())
            {
                try { new ServiceCollection().AddHostingDataProtection(configuration, environment); throw new Exception("Plaintext fallback permitted."); }
                catch (InvalidOperationException) { Check(!Directory.Exists(root), "Non-Windows opt-in fails before creating key storage."); }
                Console.WriteLine($"PASS: {checks} hosting key configuration safeguards. Windows DPAPI persistence not tested on this OS.");
                return;
            }

            ServiceProvider Provider()
            {
                var services = new ServiceCollection().AddLogging().AddSingleton<IHostEnvironment>(environment);
                services.AddHostingDataProtection(configuration, environment);
                return services.BuildServiceProvider();
            }
            const string sample = "synthetic-session-restart-check";
            string ciphertext;
            using (var first = Provider())
                ciphertext = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("STMS.Hosting.Test").Protect(sample);
            var directory = Path.Combine(root, "App_Data", "DataProtection-Keys");
            var files = Directory.GetFiles(directory, "key-*.xml");
            Check(files.Length == 1, "First protection must persist a key.");
            var xml = System.Xml.Linq.XDocument.Load(files.Single());
            Check(xml.Descendants().Any(e => e.Name.LocalName == "encryptedSecret") &&
                !xml.Descendants().Any(e => e.Name.LocalName == "masterKey"), "Key material must be DPAPI-encrypted on disk.");
            using (var second = Provider())
            {
                var protection = second.GetRequiredService<IDataProtectionProvider>();
                Check(protection.CreateProtector("STMS.Hosting.Test").Unprotect(ciphertext) == sample,
                    "A fresh provider must read the existing encrypted key.");
                try { protection.CreateProtector("Unrelated.Purpose").Unprotect(ciphertext); throw new Exception("Purpose isolation failed."); }
                catch (CryptographicException) { checks++; }
            }
            Check(Directory.GetFiles(directory, "key-*.xml").Length == 1, "Reload must reuse the existing key.");
            Check(!Directory.Exists(Path.Combine(root, "wwwroot")), "Key storage must remain outside public web assets.");
            Console.WriteLine($"PASS: {checks} database-free Windows DPAPI encryption, persistence and purpose-isolation checks.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "STMS.Hosting.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
