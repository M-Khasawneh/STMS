using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Security;

public sealed class FaceOptions { public bool Enabled { get; set; } }
public sealed record FaceTicket(string UserId, string Stamp, string Operation, DateTimeOffset Expires);
public sealed class FaceChallenges(TimeProvider clock)
{
    private readonly Dictionary<string, FaceTicket> tickets = [];
    private readonly object sync = new();
    public string Issue(string userId, string stamp, string operation)
    {
        lock (sync) {
            foreach (var key in tickets.Where(x => x.Value.Expires <= clock.GetUtcNow()).Select(x => x.Key).ToArray()) tickets.Remove(key);
            if (tickets.Count >= 1000) throw new BidException(429, "Face capture is busy. Try again shortly.");
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            tickets[token] = new(userId, stamp, operation, clock.GetUtcNow().AddMinutes(2));
            return token;
        }
    }
    public FaceTicket? Consume(string token)
    {
        lock (sync) return tickets.Remove(token, out var ticket) && ticket.Expires > clock.GetUtcNow() ? ticket : null;
    }
}

public interface IFaceCredentialStore
{
    Task<FaceCredential?> Read(string userId, CancellationToken ct);
    Task Save(FaceCredential credential, string expectedVersion, CancellationToken ct);
    Task Remove(string userId, string expectedVersion, CancellationToken ct);
}
public sealed class FaceCredentialStore(ApplicationDbContext db) : IFaceCredentialStore
{
    public Task<FaceCredential?> Read(string userId, CancellationToken ct) => db.FaceCredentials.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public async Task Save(FaceCredential credential, string expectedVersion, CancellationToken ct)
    {
        var current = await db.FaceCredentials.SingleOrDefaultAsync(x => x.UserId == credential.UserId, ct);
        CheckVersion(current, expectedVersion);
        if (current is null) db.FaceCredentials.Add(credential);
        else { current.ProtectedTemplate = credential.ProtectedTemplate; current.UpdatedAt = credential.UpdatedAt; }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new BidException(409, "The face settings changed. Reload and try again."); }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { throw new BidException(409, "Face settings already exist. Reload before replacing them."); }
    }
    public async Task Remove(string userId, string expectedVersion, CancellationToken ct)
    {
        var current = await db.FaceCredentials.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        CheckVersion(current, expectedVersion);
        if (current is null) return;
        db.FaceCredentials.Remove(current);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new BidException(409, "The face settings changed. Reload and try again."); }
    }
    private static void CheckVersion(FaceCredential? current, string version)
    {
        if ((current is null && version.Length > 0) || (current is not null && Convert.ToBase64String(current.Version) != version))
            throw new BidException(409, "The face settings changed. Reload and try again.");
    }
}

public sealed record ProtectedFace(string Stamp, float[][] Samples);
public static class FaceTemplateCodec
{
    public static byte[] Encode(ProtectedFace face)
    {
        FaceSamples.Validate(face.Samples);
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
        writer.Write((byte)1); writer.Write(face.Stamp); writer.Write(face.Samples.Length);
        foreach (var sample in face.Samples) foreach (var value in sample) writer.Write(value);
        writer.Flush(); return bytes.ToArray();
    }
    public static ProtectedFace Decode(byte[] bytes)
    {
        if (bytes.Length > 4096) throw new InvalidDataException();
        using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
        if (reader.ReadByte() != 1) throw new InvalidDataException();
        var stamp = reader.ReadString(); var count = reader.ReadInt32();
        if (stamp.Length > 256 || count is < 3 or > 5) throw new InvalidDataException();
        var samples = Enumerable.Range(0,count).Select(_ => Enumerable.Range(0,128).Select(_ => reader.ReadSingle()).ToArray()).ToArray();
        if (stream.Position != stream.Length) throw new InvalidDataException();
        FaceSamples.Validate(samples); return new(stamp,samples);
    }
}
public static class FaceSamples
{
    public static void Validate(float[][] samples)
    {
        if (samples is null || samples.Length is < 3 or > 5 || samples.Any(x => x is null || x.Length != 128 || x.Any(v => !float.IsFinite(v) || Math.Abs(v) > 2)))
            throw new BidException(400, "Capture three to five valid face samples.");
        if (samples.Any(x => x.Sum(v => (double)v*v) is < .1 or > 4)) throw new BidException(400, "The face measurements are invalid. Capture your face again.");
        if (samples.Any(x => Distance(x, samples[0]) > .4)) throw new BidException(400, "The face samples are inconsistent. Capture one face in good lighting.");
    }
    public static double Distance(float[] a, float[] b) => Math.Sqrt(a.Zip(b, (x, y) => Math.Pow((double)x - y, 2)).Sum());
    public static bool Matches(float[][] actual, float[][] stored)
    {
        Validate(actual); Validate(stored);
        // Multiple samples must agree; never accept a single unusually close frame.
        return actual.Count(a => stored.Average(b => Distance(a, b)) < .48) >= 3;
    }
}

public sealed class FaceRecognition(
    UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, IEmployeeEligibility eligibility,
    IFaceCredentialStore store, FaceChallenges challenges, IDataProtectionProvider protection,
    Microsoft.Extensions.Options.IOptions<FaceOptions> options, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("STMS.CameraFace.Settings.v1");
    private async Task<IdentityUser> Actor(ClaimsPrincipal principal)
    {
        if (!options.Value.Enabled) throw new BidException(503, "Camera face settings are awaiting database activation. Device passkeys still work.");
        var user = await users.GetUserAsync(principal);
        if (user is null || !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user) || !await eligibility.IsEligibleAsync(user)) throw new BidException(403, "This account is not eligible.");
        return user;
    }
    public async Task<FacePage> Page(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!options.Value.Enabled) return new(false, false, "", null);
        var user = await Actor(principal); var row = await store.Read(user.Id, ct);
        return new(true, row is not null, row is null ? "" : Convert.ToBase64String(row.Version), row?.UpdatedAt);
    }
    public async Task<string> Begin(ClaimsPrincipal principal, FaceOperation request)
    {
        var user = await Actor(principal);
        if (request.Operation is not ("enroll" or "verify" or "remove")) throw new BidException(400, "Choose a valid face operation.");
        if (request.Operation != "verify") {
            if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 256 || !(await signIn.CheckPasswordSignInAsync(user, request.CurrentPassword, lockoutOnFailure: true)).Succeeded)
                throw new BidException(400, "Unable to verify your current password.");
        }
        return challenges.Issue(user.Id, await users.GetSecurityStampAsync(user), request.Operation);
    }
    public async Task<bool> Complete(ClaimsPrincipal principal, FaceCapture capture, CancellationToken ct)
    {
        var user = await Actor(principal);
        var ticket = challenges.Consume(capture.Token);
        if (ticket is null || ticket.UserId != user.Id || ticket.Stamp != await users.GetSecurityStampAsync(user)) throw new BidException(400, "Face capture expired. Start again.");
        if (ticket.Operation == "remove") { await store.Remove(user.Id, capture.Version, ct); return true; }
        FaceSamples.Validate(capture.Samples);
        if (ticket.Operation == "enroll") {
            var payload = FaceTemplateCodec.Encode(new ProtectedFace(ticket.Stamp, capture.Samples));
            var encrypted = protector.CreateProtector(user.Id).Protect(payload);
            await store.Save(new() { UserId = user.Id, ProtectedTemplate = encrypted, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() }, capture.Version, ct);
            return true;
        }
        var stored = await store.Read(user.Id, ct);
        if (stored is null) throw new BidException(400, "Register your face first.");
        try {
            var template = FaceTemplateCodec.Decode(protector.CreateProtector(user.Id).Unprotect(stored.ProtectedTemplate));
            if (template.Stamp != ticket.Stamp) throw new BidException(400, "Register your face again after changing your password.");
            return FaceSamples.Matches(capture.Samples, template.Samples);
        } catch (Exception e) when (e is CryptographicException or IOException) { throw new BidException(400, "This template cannot be used. Register your face again."); }
    }
}
public static class FaceConfiguration
{
    public static IServiceCollection AddStmsFaceRecognition(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FaceOptions>(configuration.GetSection("FaceRecognition"));
        services.AddSingleton<FaceChallenges>(); services.AddScoped<IFaceCredentialStore, FaceCredentialStore>(); services.AddScoped<FaceRecognition>();
        services.AddRateLimiter(options => options.AddPolicy("face-settings", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
        return services;
    }
}
