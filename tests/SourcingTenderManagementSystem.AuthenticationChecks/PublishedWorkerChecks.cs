using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SourcingTenderManagementSystem.Web.Data;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class PublishedWorkerChecks
{
    public static async Task Run(string assembly)
    {
        assembly = Path.GetFullPath(assembly);
        if (!File.Exists(assembly) || Path.GetFileName(assembly) != "SourcingTenderManagementSystem.Web.dll") throw new Exception("Supply the published application assembly.");
        var source = DocumentExtractionChecks.TextPdf(41); var hash = SHA256.HashData(source);
        async Task<byte[]> Copy(string viewer)
        {
            var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(assembly), UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add(assembly); info.ArgumentList.Add("--watermark-document");
            using var process = Process.Start(info) ?? throw new Exception("Worker did not start");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try
            {
                using var output = new MemoryStream();
                var read = process.StandardOutput.BaseStream.CopyToAsync(output, deadline.Token);
                var errors = process.StandardError.ReadToEndAsync(deadline.Token);
                await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, new WatermarkInput("application/pdf", Convert.ToBase64String(source), new AccessStamp(viewer, "Synthetic package test", DateTimeOffset.UtcNow)), cancellationToken: deadline.Token);
                process.StandardInput.Close();
                await Task.WhenAll(read, errors, process.WaitForExitAsync(deadline.Token));
                if (process.ExitCode != 0 || output.Length is <= 0 or > 40 * 1024 * 1024) throw new Exception("Published worker failed or exceeded existing output bound");
                var bytes = output.ToArray();
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
                if (pdf.NumberOfPages != 41 || !pdf.GetPages().All(p => p.GetImages().Count() == 1 && string.IsNullOrWhiteSpace(p.Text))) throw new Exception("Published worker did not preserve all flattened pages");
                return bytes;
            }
            finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
        }
        var watch = Stopwatch.StartNew(); var copies = await Task.WhenAll(Copy("published-a"), Copy("published-b"));
        if (copies[0].SequenceEqual(copies[1]) || !hash.SequenceEqual(SHA256.HashData(source))) throw new Exception("Personal copy isolation/source integrity failed");
        Console.WriteLine($"PASS: published Release native worker renders two distinct protected 41-page PDFs in {watch.Elapsed.TotalSeconds:0.00}s within unchanged per-worker deadline; page flattening and source integrity verified. Windows host only; no SQL.");
    }
}
