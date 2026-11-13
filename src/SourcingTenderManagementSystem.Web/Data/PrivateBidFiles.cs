using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed record BidFileIntent(Guid Id, Guid BidId, Guid TenderId, string? NewReference, string? OldReference);
public interface IPrivateBidFiles
{
    Task WriteIntentAsync(BidFileIntent intent, CancellationToken ct);
    Task<IReadOnlyList<BidFileIntent>> IntentsAsync(Guid bidId, CancellationToken ct);
    Task SaveAsync(Guid bidId, string reference, ValidatedSupplierFile file, CancellationToken ct);
    Task<Stream> OpenVerifiedAsync(BidDocument document, CancellationToken ct);
    Task DeleteAsync(Guid bidId, string reference, CancellationToken ct);
    Task CompleteIntentAsync(BidFileIntent intent, CancellationToken ct);
}

// Private bid storage has no static-file route. A registration document reference is never accepted here.
public sealed class PrivateBidFiles : IPrivateBidFiles
{
    private readonly string appData;
    private readonly string root;
    private static readonly Regex Reference = new("^([a-f0-9]{32})/([a-f0-9]{64})-([a-f0-9]{64})\\.bin\\z", RegexOptions.CultureInvariant);
    public PrivateBidFiles(IWebHostEnvironment environment)
    {
        appData = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data"));
        root = Path.Combine(appData, "BidDocuments");
        var web = Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot")));
        if (root.Equals(web, StringComparison.OrdinalIgnoreCase) || root.StartsWith(web + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bid storage must be outside the web root.");
    }
    public static string NewReference(Guid bidId, byte[] bytes) => bidId.ToString("N") + "/" +
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant() + "-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".bin";
    private static void NoLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Private bid storage contains an unsupported linked path.");
    }
    private string Folder(Guid bidId, bool create)
    {
        var folder = Path.Combine(root, bidId.ToString("N"));
        foreach (var path in new[] { appData, root, folder }) { NoLink(path); if (create) Directory.CreateDirectory(path); NoLink(path); }
        return folder;
    }
    private string Resolve(Guid bidId, string reference)
    {
        if (reference is null) throw new InvalidDataException("Invalid bid storage reference.");
        var match = Reference.Match(reference);
        if (!match.Success || match.Groups[1].Value != bidId.ToString("N")) throw new InvalidDataException("Invalid bid storage reference.");
        var path = Path.Combine(Folder(bidId, false), reference[(reference.IndexOf('/') + 1)..]);
        NoLink(path); NoLink(path + ".upload"); return path;
    }
    private string Journal(Guid bidId, bool create)
    {
        var folder = Path.Combine(Folder(bidId, create), ".pending");
        NoLink(folder); if (create) Directory.CreateDirectory(folder); NoLink(folder); return folder;
    }
    private void ValidateIntent(BidFileIntent intent)
    {
        if (intent.Id == Guid.Empty || intent.BidId == Guid.Empty || intent.TenderId == Guid.Empty || intent.NewReference is null && intent.OldReference is null)
            throw new InvalidDataException("Invalid bid file operation.");
        if (intent.NewReference is not null) _ = Resolve(intent.BidId, intent.NewReference);
        if (intent.OldReference is not null) _ = Resolve(intent.BidId, intent.OldReference);
    }
    public async Task WriteIntentAsync(BidFileIntent intent, CancellationToken ct)
    {
        ValidateIntent(intent);
        var folder = Journal(intent.BidId, true); var temporary = Path.Combine(folder, intent.Id.ToString("N") + ".tmp");
        var final = Path.Combine(folder, intent.Id.ToString("N") + ".json");
        NoLink(temporary); NoLink(final);
        // The durable intent precedes file creation. Recovery can safely remove incomplete .tmp intents.
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, intent, cancellationToken: ct);
            await stream.FlushAsync(ct); stream.Flush(true);
        }
        ct.ThrowIfCancellationRequested(); File.Move(temporary, final, false);
    }
    public async Task<IReadOnlyList<BidFileIntent>> IntentsAsync(Guid bidId, CancellationToken ct)
    {
        var folder = Journal(bidId, false); if (!Directory.Exists(folder)) return [];
        // Caller holds the tender lock, so no active writer for this bid can own a temporary intent.
        foreach (var path in Directory.EnumerateFiles(folder, "*.tmp")) { ct.ThrowIfCancellationRequested(); NoLink(path); File.Delete(path); }
        var intents = new List<BidFileIntent>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            ct.ThrowIfCancellationRequested(); NoLink(path);
            if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Invalid bid operation journal.");
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var intent = await JsonSerializer.DeserializeAsync<BidFileIntent>(stream, cancellationToken: ct) ?? throw new InvalidDataException("Invalid bid operation journal.");
            ValidateIntent(intent);
            if (intent.BidId != bidId || Path.GetFileName(path) != intent.Id.ToString("N") + ".json") throw new InvalidDataException("Invalid bid operation ownership.");
            intents.Add(intent);
        }
        return intents;
    }
    public async Task SaveAsync(Guid bidId, string reference, ValidatedSupplierFile file, CancellationToken ct)
    {
        _ = Folder(bidId, true); var path = Resolve(bidId, reference);
        var expectedHash = Reference.Match(reference).Groups[3].Value;
        if (file.Bytes.Length is <= 0 or > SupplierUploadValidation.MaximumBytes || expectedHash != Convert.ToHexString(SHA256.HashData(file.Bytes)).ToLowerInvariant())
            throw new InvalidDataException("Invalid bid file content.");
        var temporary = path + ".upload";
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        { await stream.WriteAsync(file.Bytes, ct); await stream.FlushAsync(ct); stream.Flush(true); }
        ct.ThrowIfCancellationRequested(); File.Move(temporary, path, false);
    }
    public async Task<Stream> OpenVerifiedAsync(BidDocument document, CancellationToken ct)
    {
        if (document.Size is <= 0 or > SupplierUploadValidation.MaximumBytes) throw new InvalidDataException("Invalid bid file size.");
        var path = Resolve(document.BidId, document.StorageReference);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (stream.Length != document.Size) throw new InvalidDataException("Bid file size does not match its record.");
        var validated = await SupplierUploadValidation.ReadAsync(new FormFile(stream, 0, stream.Length, "file", document.OriginalName), ct);
        if (validated.OriginalName != document.OriginalName || validated.ContentType != document.ContentType ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(validated.Bytes), Convert.FromHexString(Reference.Match(document.StorageReference).Groups[3].Value)))
            throw new InvalidDataException("Bid file verification failed.");
        // The caller gets bounded verified bytes, never a physical path or a handle that replacement can invalidate.
        return new MemoryStream(validated.Bytes, writable: false);
    }
    public Task DeleteAsync(Guid bidId, string reference, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = Resolve(bidId, reference); File.Delete(path); File.Delete(path + ".upload"); return Task.CompletedTask;
    }
    public Task CompleteIntentAsync(BidFileIntent intent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ValidateIntent(intent); var path = Path.Combine(Journal(intent.BidId, false), intent.Id.ToString("N") + ".json");
        NoLink(path); File.Delete(path); return Task.CompletedTask;
    }
}
