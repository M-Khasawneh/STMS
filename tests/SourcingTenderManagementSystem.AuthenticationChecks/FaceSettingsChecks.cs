using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SourcingTenderManagementSystem.Web.Data;
using SourcingTenderManagementSystem.Web.Models.Entities;
using SourcingTenderManagementSystem.Web.Security;

namespace SourcingTenderManagementSystem.AuthenticationChecks;

public sealed class MemoryFaceCredentials : IFaceCredentialStore
{
    public Dictionary<string,FaceCredential> Rows = [];
    private long revision;
    public Task<FaceCredential?> Read(string id,CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault(id));
    public Task Save(FaceCredential row,string version,CancellationToken ct)
    {
        var current=Rows.GetValueOrDefault(row.UserId);
        if((current is null&&version.Length>0)||(current is not null&&Convert.ToBase64String(current.Version)!=version))throw new BidException(409,"Stale face settings");
        row.Version=BitConverter.GetBytes(++revision);Rows[row.UserId]=row;return Task.CompletedTask;
    }
    public Task Remove(string id,string version,CancellationToken ct)
    {
        if(Rows.TryGetValue(id,out var row)&&Convert.ToBase64String(row.Version)!=version)throw new BidException(409,"Stale face settings");
        Rows.Remove(id);return Task.CompletedTask;
    }
}
internal static class FaceSettingsChecks
{
    private static int checks;
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    internal static float[][] Samples(int axis=0)=>Enumerable.Range(0,5).Select(_=>{var row=new float[128];row[axis]=1;return row;}).ToArray();
    internal static void Rules()
    {
        checks=0;var samples=Samples();FaceSamples.Validate(samples);
        Check(FaceSamples.Matches(samples,Samples()),"Same descriptor matches");
        Check(!FaceSamples.Matches(samples,Samples(1)),"Different descriptor does not match");
        void Invalid(float[][] data){try{FaceSamples.Validate(data);throw new Exception("Invalid samples accepted");}catch(BidException e){Check(e.Status==400,"Invalid descriptors rejected");}}
        Invalid([]);Invalid([samples[0],samples[0]]);Invalid(Enumerable.Range(0,6).Select(_=>samples[0]).ToArray());Invalid([new float[127],samples[0],samples[0]]);Invalid([new float[128],samples[0],samples[0]]);
        var bad=Samples();bad[0][0]=float.NaN;Invalid(bad);bad=Samples();bad[0][0]=float.PositiveInfinity;Invalid(bad);bad=Samples();bad[0][0]=3;Invalid(bad);bad=Samples();bad[0]=Samples(1)[0];Invalid(bad);
        var bytes=FaceTemplateCodec.Encode(new("stamp",samples));var decoded=FaceTemplateCodec.Decode(bytes);
        Check(bytes.Length<4096&&decoded.Stamp=="stamp"&&FaceSamples.Matches(samples,decoded.Samples),"Bounded binary template roundtrip");
        var provider=new EphemeralDataProtectionProvider();var protectedBytes=provider.CreateProtector("STMS.CameraFace.Settings.v1").CreateProtector("user-a").Protect(bytes);
        Check(!protectedBytes.SequenceEqual(bytes),"Face template encrypted");
        try{provider.CreateProtector("STMS.CameraFace.Settings.v1").CreateProtector("user-b").Unprotect(protectedBytes);throw new Exception("Cross-account template decrypted");}catch(System.Security.Cryptography.CryptographicException){Check(true,"Per-user protection scope");}
        var clock=new BidPhase1Checks.Fixture().Clock;var challenges=new FaceChallenges(clock);
        var token=challenges.Issue("a","stamp","enroll");Check(challenges.Consume(token)?.UserId=="a"&&challenges.Consume(token) is null,"Face token one-use");
        Check(challenges.Consume("unknown") is null,"Unknown token rejected");
        var expired = challenges.Issue("a", "stamp", "camera-login:");
        clock.Utc = clock.GetUtcNow().AddMinutes(3);
        Check(challenges.Consume(expired) is null, "Expired camera challenges cannot be used");
    }
    internal static async Task Mvc(HttpClient client,IServiceProvider services)
    {
        async Task<string> Anti(HttpClient c,string route){var r=await c.GetAsync(route);var html=await r.Content.ReadAsStringAsync();return WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);}
        var anti=await Anti(client,"/FaceSettings");Check(anti.Length>0,"Real camera Razor page compiles and contains antiforgery");
        async Task<HttpResponseMessage> Post(string action,object data,string? csrf=null,HttpClient? use=null){var request=new HttpRequestMessage(HttpMethod.Post,"/FaceSettings/"+action){Content=JsonContent.Create(data)};if(csrf is not null)request.Headers.Add("RequestVerificationToken",csrf);return await (use??client).SendAsync(request);}
        foreach(var action in new[]{"Begin","Complete"})Check((await Post(action,new{})).StatusCode==HttpStatusCode.BadRequest,"CSRF required "+action);
        Check((await client.GetAsync("/FaceSettings/Begin")).StatusCode==HttpStatusCode.MethodNotAllowed,"No face mutation by GET");
        var response=await Post("Begin",new{operation="enroll",currentPassword="wrong-password"},anti);Check(response.StatusCode==HttpStatusCode.BadRequest&&!(await response.Content.ReadAsStringAsync()).Contains("wrong-password"),"Password required and redacted");
        Check((await Post("Begin",new{operation="login"},anti)).StatusCode==HttpStatusCode.BadRequest,"No camera authentication operation");
        async Task<string> Begin(string operation){var r=await Post("Begin",new{operation,currentPassword=PasskeyChecks.Password},anti);Check(r.StatusCode==HttpStatusCode.OK,"Begin "+operation);return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;}
        var token=await Begin("enroll");response=await Post("Complete",new{token,version="",samples=Samples()},anti);Check(response.StatusCode==HttpStatusCode.OK,"Enrollment succeeds");
        var store=services.GetRequiredService<MemoryFaceCredentials>();Check(store.Rows.Count==1,"One template per user");var original=store.Rows.Values.Single();var version=Convert.ToBase64String(original.Version);
        Check(original.ProtectedTemplate.Length<8192&&!original.ProtectedTemplate.SequenceEqual(FaceTemplateCodec.Encode(new("stamp",Samples()))),"Only protected template persisted");
        Check((await Post("Complete",new{token,version,samples=Samples()},anti)).StatusCode==HttpStatusCode.BadRequest,"No replay");
        token=await Begin("verify");response=await Post("Complete",new{token,version,samples=Samples()},anti);Check(response.StatusCode==HttpStatusCode.OK&&(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("matched").GetBoolean(),"Matching own face");
        token=await Begin("verify");response=await Post("Complete",new{token,version,samples=Samples(1)},anti);Check(response.StatusCode==HttpStatusCode.OK&&!(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("matched").GetBoolean(),"Nonmatching face does not verify");
        token=await Begin("enroll");Check((await Post("Complete",new{token,version="stale",samples=Samples()},anti)).StatusCode==HttpStatusCode.Conflict&&store.Rows.Count==1,"Stale replacement rejected");
        token=await Begin("verify");Check((await Post("Complete",new{token,version,samples=new[]{new float[1]}},anti)).StatusCode==HttpStatusCode.BadRequest,"Malformed capture rejected");
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=HttpClientHandler.DangerousAcceptAnyServerCertificateValidator};using var other=new HttpClient(handler){BaseAddress=client.BaseAddress};
        Check((await other.GetAsync("/FaceSettings")).StatusCode==HttpStatusCode.Redirect,"Anonymous face access rejected");
        var otherAnti=await Anti(other,"/Account/Login");response=await other.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",otherAnti},{"UserName","other-passkey-user"},{"Password",PasskeyChecks.Password}}));Check(response.StatusCode==HttpStatusCode.Redirect,"Second memory-only account login");
        otherAnti=await Anti(other,"/FaceSettings");token=await Begin("verify");Check((await Post("Complete",new{token,version,samples=Samples()},otherAnti,other)).StatusCode==HttpStatusCode.BadRequest,"Another user cannot use capture token");
        Check(store.Rows.Count==1,"Isolation does not change original template");
        await CameraLoginChecks.Run(client, services);
        version = Convert.ToBase64String(store.Rows.Values.Single().Version);
        // Rate limiting is exercised separately after the functional flow to avoid masking authorization.
        services.GetRequiredService<IOptions<FaceOptions>>().Value.Enabled=false;
        var disabled=await client.GetAsync("/FaceSettings");Check(disabled.StatusCode==HttpStatusCode.OK&&(await disabled.Content.ReadAsStringAsync()).Contains("Awaiting activation"),"Disabled feature renders without schema");
        services.GetRequiredService<IOptions<FaceOptions>>().Value.Enabled=true;
        token=await Begin("remove");Check((await Post("Complete",new{token,version,samples=Array.Empty<float[]>()},anti)).StatusCode==HttpStatusCode.OK&&store.Rows.Count==0,"Own template removal");
        Console.WriteLine($"PASS: {checks} database-free face validation, encryption, ownership, password, antiforgery, concurrency and MVC checks. No SQL writes.");
    }
}
