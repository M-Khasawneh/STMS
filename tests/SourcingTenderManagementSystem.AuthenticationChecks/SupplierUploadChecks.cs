using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public static class SupplierUploadChecks
{
    public static async Task RunAsync()
    {
        var png = DocumentExtractionChecks.Image();
        var pdf = DocumentExtractionChecks.TextPdf();
        var jpg = DocumentExtractionChecks.Image(format: SkiaSharp.SKEncodedImageFormat.Jpeg);
        foreach (var (name, bytes) in new[] { ("ok.png", png), ("ok.pdf", pdf), ("ok.jpg", jpg) })
            Check((await SupplierUploadValidation.ReadAsync(File(name, bytes), default)).Bytes.SequenceEqual(bytes), "accepted extension and signature: " + name);
        foreach (var file in new[] { File("signature.png", new byte[]{137,80,78,71,13,10,26,10}), File("signature.jpg", new byte[]{255,216,255,217}), File("malformed.pdf", "%PDF-1.4 broken %%EOF"u8.ToArray()), File("bad.exe", png), File("bad.png", pdf), File("bad.pdf", png), File("bad.jpg", pdf), File("empty.pdf", []), File("large.pdf", new byte[SupplierUploadValidation.MaximumBytes + 1]) })
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
            Check((await storage.PendingAsync(default)).Contains(reference), "supplier upload intent is durable before SQL commit");
            await SupplierDocumentRecovery.ReconcileAsync(storage, (_, _) => Task.FromResult(true), default);
            Check(!(await storage.PendingAsync(default)).Contains(reference), "committed document completes journal without deleting bytes");
            await using (var lease = await storage.LockRegistrationAsync(default))
            {
                using var cancelled = new CancellationTokenSource(100);
                try { await using var competing = await storage.LockRegistrationAsync(cancelled.Token); throw new Exception("Concurrent registration lease accepted"); }
                catch (OperationCanceledException) { Console.WriteLine("PASS: private supplier registration lease serializes concurrent recovery"); }
            }
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
