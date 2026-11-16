using System.Diagnostics;
using System.Text.Json;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed class PrivateWatermarkedCopies
{
    private static readonly SemaphoreSlim workers = new(2, 2);
    public async Task<WatermarkedCopy> CreateAsync(BidDocumentDownload source, AccessStamp stamp, CancellationToken ct)
    {
        BidRules.Require(await workers.WaitAsync(TimeSpan.FromSeconds(5), ct), 503, "Personal-copy rendering is busy. Please try again.");
        try { return await CreateCoreAsync(source, stamp, ct); } finally { workers.Release(); }
    }
    public static async Task ValidateUploadAsync(byte[] bytes, string extension, CancellationToken ct)
    {
        BidRules.Require(await workers.WaitAsync(TimeSpan.FromSeconds(5), ct), 503, "Document validation is busy. Please try again.");
        try
        {
            await using var source = new BidDocumentDownload(new MemoryStream(bytes, false), "upload" + extension, "application/octet-stream", bytes.Length);
            await CreateCoreAsync(source, new AccessStamp("", "", DateTimeOffset.UnixEpoch), ct, true);
        }
        catch (BidException e) when (e.Status == 409) { throw new InvalidDataException("The document is damaged, encrypted or cannot be decoded safely.", e); }
        finally { workers.Release(); }
    }
    private static async Task<WatermarkedCopy> CreateCoreAsync(BidDocumentDownload source, AccessStamp stamp, CancellationToken ct, bool validateOnly = false)
    {
        using var memory = new MemoryStream(); var buffer = new byte[8192]; int read; while ((read = await source.Content.ReadAsync(buffer, ct)) > 0) { BidRules.Require(memory.Length + read <= 5242880, 409, "The protected document exceeds the personal-copy limit."); memory.Write(buffer, 0, read); }
        BidRules.Require(memory.Length is > 0 and <= 5242880, 409, "The protected document exceeds the personal-copy limit."); var type = Path.GetExtension(source.OriginalName).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".jpg" => "image/jpeg", ".png" => "image/png", _ => "" }; BidRules.Require(type != "", 409, "This document format cannot be watermarked.");
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }; info.ArgumentList.Add(typeof(DocumentWatermarkWorker).Assembly.Location); info.ArgumentList.Add("--watermark-document");
        using var process = new Process { StartInfo = info }; using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(45)); var token = deadline.Token; bool started = false; Task[] running = [];
        try
        {
            if (!process.Start()) throw new IOException(); started = true;
            async Task Send() { await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, new WatermarkInput(type, Convert.ToBase64String(memory.ToArray()), stamp, validateOnly), AiBidRules.Json, token); process.StandardInput.Close(); }
            async Task<byte[]> Read(Stream stream, int maximum) { using var output = new MemoryStream(); var block = new byte[8192]; int n; while ((n = await stream.ReadAsync(block, token)) > 0) { if (output.Length + n > maximum) throw new InvalidDataException(); output.Write(block, 0, n); } return output.ToArray(); }
            var output = Read(process.StandardOutput.BaseStream, 40 * 1024 * 1024); var error = Read(process.StandardError.BaseStream, 16000); var send = Send(); var exit = process.WaitForExitAsync(token); running = [output, error, send, exit];
            while (!exit.IsCompleted) { token.ThrowIfCancellationRequested(); process.Refresh(); if (process.HasExited) break; long workingSet; try { workingSet = process.WorkingSet64; } catch (InvalidOperationException) when (process.HasExited) { break; } if (workingSet > 700L * 1024 * 1024 || output.IsFaulted || error.IsFaulted || send.IsFaulted) throw new IOException(); await Task.Delay(100, token); }
            await Task.WhenAll(send, exit, error); var bytes = await output;
            BidRules.Require(process.ExitCode == 0 && bytes.Length > 0, 409, "A safe watermarked copy could not be created. The original remains unchanged; encrypted or malformed PDFs require administrator assistance.");
            var pdf = type == "application/pdf"; return new(bytes, pdf ? "application/pdf" : "image/png", Path.GetFileNameWithoutExtension(source.OriginalName) + "-watermarked." + (pdf ? "pdf" : "png"));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new BidException(503, "Personal-copy rendering timed out. No original file was released."); }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { throw new BidException(503, "Personal-copy rendering is unavailable. No original file was released."); }
        finally { if (started) { try { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } catch (InvalidOperationException) { } } deadline.Cancel(); try { await Task.WhenAll(running).WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception e) when (e is IOException or OperationCanceledException or TimeoutException or InvalidOperationException) { } }
    }
}
