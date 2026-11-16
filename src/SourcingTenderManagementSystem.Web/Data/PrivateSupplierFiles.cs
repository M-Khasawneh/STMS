using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SourcingTenderManagementSystem.Web.Data;

public sealed record ValidatedSupplierFile(byte[] Bytes, string OriginalName, string ContentType);

public interface IPrivateSupplierFiles
{
    Task<IAsyncDisposable?> LockRegistrationAsync(CancellationToken ct) => Task.FromResult<IAsyncDisposable?>(null);
    Task<IReadOnlyList<string>> PendingAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
    void Complete(string reference) { }
    Task<string> SaveAsync(ValidatedSupplierFile file, CancellationToken cancellationToken);
    Task<Stream> OpenAsync(string reference, CancellationToken cancellationToken);
    void Delete(string reference);
}

public sealed class PrivateSupplierFiles(IWebHostEnvironment environment) : IPrivateSupplierFiles
{
    // Deliberately outside wwwroot; no static file middleware points here.
    private readonly string root = Path.Combine(environment.ContentRootPath, "App_Data", "SupplierDocuments");
    public async Task<IAsyncDisposable?> LockRegistrationAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(root, ".registration.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous); }
            catch (IOException) when (attempt < 50) { await Task.Delay(100, ct); }
        }
    }
    public Task<IReadOnlyList<string>> PendingAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Directory.Exists(root)) return Task.FromResult<IReadOnlyList<string>>([]);
        return Task.FromResult<IReadOnlyList<string>>(Directory.EnumerateFiles(root, "*.bin.pending")
            .Select(p => Path.GetFileName(p)[..^8]).Where(r => Regex.IsMatch(r, "^[a-f0-9]{64}\\.bin$", RegexOptions.CultureInvariant)).ToArray());
    }
    public void Complete(string reference) => File.Delete(Resolve(reference) + ".pending");
    private string Resolve(string reference)
    {
        if (!Regex.IsMatch(reference, "^[a-f0-9]{64}\\.bin$", RegexOptions.CultureInvariant))
            throw new IOException("Invalid private file reference.");
        return Path.Combine(root, reference);
    }
    public async Task<string> SaveAsync(ValidatedSupplierFile file, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var reference = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant() + ".bin";
        var path = Resolve(reference);
        // Journal precedes bytes. Registration recovery holds an exclusive filesystem lease
        // across SQL commit and reconciliation, including between application processes.
        await using (var intent = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
        { intent.WriteByte(1); intent.Flush(true); }
        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(file.Bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(true);
            return reference;
        }
        catch { try { File.Delete(path); } catch (IOException) { /* Durable journal permits recovery. */ } throw; }
    }
    public Task<Stream> OpenAsync(string reference, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new FileStream(Resolve(reference), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));
    public void Delete(string reference) => File.Delete(Resolve(reference));
}

public static class SupplierUploadValidation
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    public static async Task<ValidatedSupplierFile> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > MaximumBytes)
            throw new InvalidDataException("Each required document must contain data and be no larger than 5 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".jpg" or ".png"))
            throw new InvalidDataException("Only PDF, JPG and PNG documents are allowed.");
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int length;
        while ((length = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + length > MaximumBytes) throw new InvalidDataException("Document exceeds 5 MB.");
            output.Write(buffer, 0, length);
        }
        var bytes = output.ToArray();
        var valid = extension switch
        {
            ".pdf" => bytes.AsSpan().StartsWith("%PDF-"u8) && Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).Contains("%%EOF", StringComparison.Ordinal),
            ".png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" => bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217,
            _ => false
        };
        if (!valid || bytes.LongLength != file.Length) throw new InvalidDataException("Document contents do not match the selected file format.");
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (name.Length > 512 || name.Any(char.IsControl)) throw new InvalidDataException("Document filename is invalid.");
        await PrivateWatermarkedCopies.ValidateUploadAsync(bytes, extension, cancellationToken);
        return new(bytes, name, extension == ".pdf" ? "application/pdf" : extension == ".png" ? "image/png" : "image/jpeg");
    }
}
