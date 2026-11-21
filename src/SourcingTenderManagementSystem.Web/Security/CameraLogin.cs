using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Account;

namespace SourcingTenderManagementSystem.Web.Security;

public sealed class CameraLogin(UserManager<IdentityUser> users, EmployeeSignInManager signIn,
    IFaceCredentialStore store, FaceChallenges challenges, IDataProtectionProvider protection,
    IOptions<FaceOptions> options, TimeProvider clock)
{
    private const string Cookie = "__Host-STMS.Camera";
    private const string Failure = "Unable to sign in with the camera. Use your password or registered passkey.";
    private readonly IDataProtector protector = protection.CreateProtector("STMS.CameraFace.Settings.v1");
    private HttpContext Context => signIn.Context;
    private static CookieOptions CookieSettings => new() { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true };
    private void RequireAnonymous()
    {
        if (!options.Value.Enabled || Context.User.Identity?.IsAuthenticated == true) throw new BidException(400, Failure);
    }
    public async Task<string> Begin(string userName, CancellationToken ct)
    {
        RequireAnonymous();
        challenges.Consume(Context.Request.Cookies[Cookie] ?? "");
        var user = await users.FindByNameAsync(userName);
        var row = user is null ? null : await store.Read(user.Id, ct);
        // Identical challenge shape for unknown, unregistered and disabled accounts.
        // No template, eligibility flag or account-existence result leaves the server.
        var token = challenges.Issue(user?.Id ?? Guid.NewGuid().ToString(),
            user is null ? Guid.NewGuid().ToString() : await users.GetSecurityStampAsync(user),
            "camera-login:" + (row is null ? "" : Convert.ToBase64String(row.Version)));
        var settings = CookieSettings; settings.MaxAge = TimeSpan.FromMinutes(2);
        Context.Response.Cookies.Append(Cookie, token, settings);
        return token;
    }
    public async Task Complete(FaceCapture capture, CancellationToken ct)
    {
        RequireAnonymous();
        var cookie = Context.Request.Cookies[Cookie];
        Context.Response.Cookies.Delete(Cookie, CookieSettings);
        if (cookie is null || capture.Token is null || capture.Token.Length != 64 || cookie.Length != 64 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(cookie), Encoding.ASCII.GetBytes(capture.Token)))
            throw new BidException(400, Failure);
        var ticket = challenges.Consume(capture.Token);
        if (ticket is null || !ticket.Operation.StartsWith("camera-login:", StringComparison.Ordinal)) throw new BidException(400, Failure);
        await challenges.Writes.WaitAsync(ct);
        try
        {
            if (ticket.Expires <= clock.GetUtcNow()) throw new BidException(400, Failure);
            var user = await users.FindByIdAsync(ticket.UserId);
            if (user is null || ticket.Stamp != await users.GetSecurityStampAsync(user) ||
                !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user)) throw new BidException(400, Failure);
            var row = await store.Read(user.Id, ct);
            var matched = false;
            if (row is not null && ticket.Operation == "camera-login:" + Convert.ToBase64String(row.Version))
            {
                try
                {
                    var template = FaceTemplateCodec.Decode(protector.CreateProtector(user.Id).Unprotect(row.ProtectedTemplate));
                    matched = template.LoginEnabled && template.Stamp == ticket.Stamp && FaceSamples.Matches(capture.Samples, template.Samples);
                }
                catch (Exception e) when (e is CryptographicException or IOException or BidException) { matched = false; }
            }
            if (!matched)
            {
                await users.AccessFailedAsync(user);
                throw new BidException(400, Failure);
            }
            // Use the same Identity eligibility/lockout/two-factor path as other credentials.
            var result = await signIn.CameraSignInAsync(user);
            if (!result.Succeeded)
            {
                await signIn.Context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
                throw new BidException(400, Failure);
            }
        }
        finally { challenges.Writes.Release(); }
    }
}
