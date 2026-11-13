using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class SupplierUploadChecks
{
    public static async Task RunAsync()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==");
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj <<>> endobj\n%%EOF");
        var jpg = new byte[] { 255, 216, 255, 224, 255, 217 };
        foreach (var (name, bytes) in new[] { ("ok.png", png), ("ok.pdf", pdf), ("ok.jpg", jpg) })
            Check((await SupplierUploadValidation.ReadAsync(File(name, bytes), default)).Bytes.SequenceEqual(bytes), "accepted extension and signature: " + name);
        foreach (var file in new[] { File("bad.exe", png), File("bad.png", pdf), File("bad.pdf", png), File("bad.jpg", pdf), File("empty.pdf", []), File("large.pdf", new byte[SupplierUploadValidation.MaximumBytes + 1]) })
        {
            try { await SupplierUploadValidation.ReadAsync(file, default); throw new Exception("Expected file rejection."); }
            catch (InvalidDataException) { Console.WriteLine("PASS: invalid supplier upload rejected"); }
        }
        try { await SupplierUploadValidation.ReadAsync(null, default); throw new Exception("Missing upload accepted."); }
        catch (InvalidDataException) { }
        var root = Path.Combine(Path.GetTempPath(), "supplier-files-" + Guid.NewGuid().ToString("N"));
        var storage = new PrivateSupplierFiles(new FileEnvironment(root));
        var reference = await storage.SaveAsync(await SupplierUploadValidation.ReadAsync(File("../../name.png", png), default), default);
        try
        {
            await using var input = await storage.OpenAsync(reference, default);
            using var output = new MemoryStream(); await input.CopyToAsync(output);
            Check(output.ToArray().SequenceEqual(png) && !reference.Contains("name"), "private file roundtrip uses random references");
            try { await storage.OpenAsync("../outside.bin", default); throw new Exception("Traversal accepted."); }
            catch (IOException) { Console.WriteLine("PASS: private file traversal refused"); }
        }
        finally { storage.Delete(reference); Directory.Delete(root, true); }
    }
    private static IFormFile File(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private sealed class FileEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "UploadChecks";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
