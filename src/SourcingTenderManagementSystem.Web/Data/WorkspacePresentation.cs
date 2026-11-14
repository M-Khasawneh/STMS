using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SourcingTenderManagementSystem.Web.Data;

public interface IWorkspacePresentation
{
    Task<int?> UnreadAsync(ClaimsPrincipal user);
    bool HasAvatar(ClaimsPrincipal user);
}
public sealed class WorkspacePresentation(ApplicationDbContext db, ProfilePictures pictures, ILogger<WorkspacePresentation> logger) : IWorkspacePresentation
{
    public bool HasAvatar(ClaimsPrincipal user)
    {
        try { return !user.IsInRole("Supplier") && pictures.Exists(user.FindFirstValue(ClaimTypes.NameIdentifier)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { logger.LogWarning(e, "Profile picture unavailable."); return false; }
    }
    public async Task<int?> UnreadAsync(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null) return null;
        try { return await db.Notifications.CountAsync(x => x.RecipientUserId == id && x.ReadAt == null); }
        catch (Exception e) when (e is System.Data.Common.DbException or TimeoutException) { logger.LogWarning(e, "Unread notification count unavailable."); return null; }
    }
}
public sealed class ProfilePictures(IWebHostEnvironment environment)
{
    private string Resolve(string userId)
    {
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "ProfilePictures"));
        var web = Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot")));
        if (root.Equals(web, StringComparison.OrdinalIgnoreCase) || root.StartsWith(web + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Private picture storage required.");
        foreach (var parent in new[] { Path.GetDirectoryName(root)!, root })
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked picture storage is unsupported.");
        return Path.Combine(root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))) + ".bin");
    }
    public bool Exists(string? userId) => userId is not null && File.Exists(Resolve(userId));
    public async Task SaveAsync(string userId, IFormFile? file, CancellationToken ct)
    {
        var validated = await SupplierUploadValidation.ReadAsync(file, ct);
        if (validated.ContentType is not ("image/png" or "image/jpeg")) throw new InvalidDataException("Choose a JPG or PNG image up to 5 MB.");
        var path = Resolve(userId);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profile files are unsupported.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _ = Resolve(userId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temporary, validated.Bytes, ct); File.Move(temporary, path, true); }
        finally { File.Delete(temporary); }
    }
    public async Task<(byte[] Bytes, string Type)?> ReadAsync(string userId, CancellationToken ct)
    {
        var path = Resolve(userId);
        if (!File.Exists(path)) return null;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > SupplierUploadValidation.MaximumBytes) throw new InvalidDataException("Invalid profile picture.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var name = bytes.Length >= 8 && bytes[0] == 137 ? "avatar.png" : "avatar.jpg";
        var validated = await SupplierUploadValidation.ReadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name), ct);
        return (validated.Bytes, validated.ContentType);
    }
    public void Remove(string userId) => File.Delete(Resolve(userId));
}
