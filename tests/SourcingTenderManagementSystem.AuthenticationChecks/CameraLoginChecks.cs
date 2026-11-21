using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class CameraLoginChecks
{
    internal static async Task Run(HttpClient owner, IServiceProvider services)
    {
        var checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByNameAsync("passkey-user"))!;
        var store = services.GetRequiredService<MemoryFaceCredentials>();
        HttpClient Fresh() => new(new HttpClientHandler { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }) { BaseAddress = owner.BaseAddress };
        async Task<string> Anti(HttpClient client, string path) => WebUtility.HtmlDecode(Regex.Match(await client.GetStringAsync(path), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            if (token is not null) request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }
        float[][] Samples(bool different = false) => FaceSettingsChecks.Samples(different ? 1 : 0);
        async Task Change(string operation)
        {
            var anti = await Anti(owner, "/FaceSettings");
            using var begin = await Post(owner, "/FaceSettings/Begin", new { operation, currentPassword = PasskeyChecks.Password }, anti);
            Check(begin.StatusCode == HttpStatusCode.OK, "Password-confirmed camera preference begin");
            var token = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            using var complete = await Post(owner, "/FaceSettings/Complete", new { token, version = Convert.ToBase64String(store.Rows[user.Id].Version), samples = Array.Empty<float[]>() }, anti);
            Check(complete.StatusCode == HttpStatusCode.OK, "Camera preference saved with current concurrency version");
        }
        async Task Attempt(string name, bool success, bool different = false, Func<Task>? during = null)
        {
            using var client = Fresh(); var anti = await Anti(client, "/Account/Login");
            using var begin = await Post(client, "/CameraLogin/Begin", new { userName = name }, anti);
            Check(begin.StatusCode == HttpStatusCode.OK && begin.Headers.CacheControl?.NoStore == true, "Uniform anonymous challenge/no-store");
            var json = await begin.Content.ReadFromJsonAsync<JsonElement>();
            Check(json.EnumerateObject().Count() == 1, "Challenge exposes no account eligibility or biometric data");
            var token = json.GetProperty("token").GetString();
            Check(begin.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("__Host-STMS.Camera=") && c.Contains("secure", StringComparison.OrdinalIgnoreCase) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase)), "Browser-bound secure challenge cookie");
            if (during is not null) await during();
            using var complete = await Post(client, "/CameraLogin/Complete", new { token, samples = Samples(different) }, anti);
            var signedIn = complete.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("__Host-SourcingTender.Auth="));
            Check(complete.StatusCode == (success ? HttpStatusCode.OK : HttpStatusCode.BadRequest) && signedIn == success, $"Camera sign-in for {name}: expected success={success}, status={(int)complete.StatusCode}, cookie={signedIn}, twoFactor={user.TwoFactorEnabled}: {await complete.Content.ReadAsStringAsync()}");
            Check((await client.GetAsync("/FaceSettings")).StatusCode == (success ? HttpStatusCode.OK : HttpStatusCode.Redirect), "Real authentication/anonymous isolation after capture");
        }
        using (var anonymous = Fresh()) {
            foreach (var action in new[] { "Begin", "Complete" })
                Check((await Post(anonymous, "/CameraLogin/" + action, new { }, null)).StatusCode == HttpStatusCode.BadRequest, "Camera login requires CSRF " + action);
            Check((await owner.GetAsync("/CameraLogin/Begin")).StatusCode == HttpStatusCode.MethodNotAllowed, "No camera mutation by GET");
        }
        // Backward compatibility: a v1 encrypted template cannot silently become a credential.
        var codec = FaceTemplateCodec.Encode(new(user.SecurityStamp!, Samples()));
        var legacy = codec[..^1]; legacy[0] = 1;
        Check(!FaceTemplateCodec.Decode(legacy).LoginEnabled, "Legacy template opt-in defaults off");
        store.Rows[user.Id].ProtectedTemplate = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("STMS.CameraFace.Settings.v1").CreateProtector(user.Id).Protect(legacy);
        await Attempt("unknown-camera-account", false);
        await Attempt("other-passkey-user", false);
        await Attempt("passkey-user", false);
        using (var noPassword = await Post(owner, "/FaceSettings/Begin", new { operation = "enable-login" }, await Anti(owner, "/FaceSettings")))
            Check(noPassword.StatusCode == HttpStatusCode.BadRequest, "Opt-in requires current-password confirmation even inside an authenticated session");
        await Change("enable-login");
        await Attempt("passkey-user", true);
        using (var originatingBrowser = Fresh()) {
            var anti = await Anti(originatingBrowser, "/Account/Login");
            using var begin = await Post(originatingBrowser, "/CameraLogin/Begin", new { userName = "passkey-user" }, anti);
            var token = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
            using var stranger = Fresh(); var otherAnti = await Anti(stranger, "/Account/Login");
            using var stolen = await Post(stranger, "/CameraLogin/Complete", new { token, samples = Samples() }, otherAnti);
            Check(stolen.StatusCode == HttpStatusCode.BadRequest, "A valid token cannot cross browsers");
            using var complete = await Post(originatingBrowser, "/CameraLogin/Complete", new { token, samples = Samples(), returnUrl = "https://external.example.invalid/" }, anti);
            Check(complete.StatusCode == HttpStatusCode.OK, "Foreign browser cannot consume the originating challenge");
            Check((await complete.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("redirectUrl").GetString() == "/Workspace", "Camera login rejects external redirect destinations");
            var signedAnti = await Anti(originatingBrowser, "/FaceSettings");
            await originatingBrowser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",signedAnti}}));
            anti = await Anti(originatingBrowser, "/Account/Login");
            using var replay = await Post(originatingBrowser, "/CameraLogin/Complete", new { token, samples = Samples() }, anti);
            Check(replay.StatusCode == HttpStatusCode.BadRequest, "A used camera challenge cannot log in again");
        }
        await Attempt("passkey-user", false, different: true);
        await Attempt("passkey-user", false, during: () => Change("disable-login"));
        await Change("enable-login");
        var eligibility = services.GetRequiredService<PasskeyFixtureEligibility>();
        eligibility.Denied.Add(user.Id); await Attempt("passkey-user", false); eligibility.Denied.Remove(user.Id);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5)); await Attempt("passkey-user", false); await users.SetLockoutEndDateAsync(user, null);
        var stamp = user.SecurityStamp; user.SecurityStamp = Guid.NewGuid().ToString(); await Attempt("passkey-user", false); user.SecurityStamp = stamp;
        user.Email = "camera-fixture@example.invalid"; user.EmailConfirmed = true; user.TwoFactorEnabled = true;
        await Attempt("passkey-user", false); user.TwoFactorEnabled = false;
        var savedTemplate = store.Rows[user.Id]; store.Rows.Remove(user.Id);
        await Attempt("passkey-user", false); store.Rows[user.Id] = savedTemplate;
        await users.ResetAccessFailedCountAsync(user);
        for (var i = 0; i < 5; i++) await Attempt("passkey-user", false, different: true);
        Check(await users.IsLockedOutAsync(user), "Camera mismatch participates in Identity account lockout");
        await users.SetLockoutEndDateAsync(user, null);
        await Change("disable-login");
        Console.WriteLine($"PASS: {checks} database-free camera login opt-in, legacy, CSRF, cookie, match, eligibility, stamp, two-factor and lockout checks.");
    }
}
