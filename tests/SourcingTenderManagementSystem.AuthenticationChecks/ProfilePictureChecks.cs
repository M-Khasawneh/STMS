using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class ProfilePictureChecks
{
    internal sealed class PictureFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "stms-picture-check-" + Guid.NewGuid().ToString("N"));
        public ProfilePictures Pictures => new(new Environment(root));
        public void Dispose()
        {
            var path = Path.GetFullPath(root);
            if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith("stms-picture-check-", StringComparison.Ordinal)) throw new Exception("Unsafe picture fixture path");
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "stms-picture-check-" + Guid.NewGuid().ToString("N"));
        var pictures = new ProfilePictures(new Environment(root));
        var png = DocumentExtractionChecks.Image();
        var checks = 0;
        void Check(bool valid) { if (!valid) throw new Exception("Profile picture check failed"); checks++; }
        FormFile File(byte[] bytes, string name) => new(new MemoryStream(bytes), 0, bytes.Length, "picture", name);
        try {
            Check(!pictures.Exists("employee"));
            await pictures.SaveAsync("employee", File(png, "picture.png"), default);
            Check(pictures.Exists("employee") && !pictures.Exists("other"));
            Check((await pictures.ReadAsync("employee", default))!.Value.Bytes.SequenceEqual(png));
            Check(await pictures.ReadAsync("other", default) is null);
            foreach (var file in new[] { File(png, "bad.jpg"), File(png, "bad.svg"), File([], "empty.png"), File(new byte[5 * 1024 * 1024 + 1], "large.png") }) {
                try { await pictures.SaveAsync("employee", file, default); throw new Exception("Invalid picture accepted"); }
                catch (InvalidDataException) { checks++; }
            }
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => pictures.SaveAsync("employee", File(png, "picture.png"), default)));
            Check((await pictures.ReadAsync("employee", default))!.Value.Bytes.SequenceEqual(png));
            Check(!Directory.Exists(Path.Combine(root, "wwwroot")));
            Check(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Length == 0);
            pictures.Remove("employee"); Check(!pictures.Exists("employee"));
            Console.WriteLine($"PASS: {checks} database-free profile-picture validation, isolation and atomic-storage checks.");
        }
        finally {
            var resolved = Path.GetFullPath(root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("stms-picture-check-", StringComparison.Ordinal)) throw new Exception("Unsafe cleanup path");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
    private sealed class Environment(string root) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = root; public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string ApplicationName { get; set; } = "PictureChecks"; public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
