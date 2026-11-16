using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SourcingTenderManagementSystem.Web.Controllers;
namespace SourcingTenderManagementSystem.AuthenticationChecks;

internal static class StaticAssetPipelineChecks
{
    public static async Task Run()
    {
        var source=Path.GetFullPath("src/SourcingTenderManagementSystem.Web");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(AccountController).Assembly.GetName().Name,EnvironmentName="Development",ContentRootPath=source,WebRootPath=Path.Combine(source,"wwwroot")});
        builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.WebHost.UseStaticWebAssets();
        builder.Services.AddAuthentication("memory").AddScheme<AuthenticationSchemeOptions,BidMvcTestAuthentication>("memory",_=>{});builder.Services.AddAuthorization();
        await using var app=builder.Build();int downstream=0,checks=0;
        void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
        app.UseRouting();app.Use(async(_,next)=>{Interlocked.Increment(ref downstream);await next();});app.UseAuthentication();app.UseAuthorization();
        app.MapStaticAssets().ShortCircuit().AllowAnonymous();app.MapGet("/protected-check",()=>"authorized").RequireAuthorization();
        await app.StartAsync();try{
            using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
            foreach(var path in new[]{"/css/site.css","/css/appearance.css","/js/appearance.js","/images/brand/stms-logo.png"}){
                var response=await client.GetAsync(path);Check(response.StatusCode==HttpStatusCode.OK,"Static asset served "+path);Check(downstream==0,"Static asset bypasses authentication middleware "+path);
                Check(response.Headers.ETag is not null,"Asset validators remain available "+path);
            }
            var protectedResponse=await client.GetAsync("/protected-check");Check(protectedResponse.StatusCode==HttpStatusCode.Unauthorized,"Protected actions still require authentication");Check(downstream==1,"Dynamic request runs the normal pipeline");
            client.DefaultRequestHeaders.Add("X-Bid-Test-Actor","officer");Check((await client.GetAsync("/protected-check")).StatusCode==HttpStatusCode.OK&&downstream==2,"Authorized dynamic request preserved");
            Check((await client.GetAsync("/App_Data/private-document.pdf")).StatusCode!=HttpStatusCode.OK,"Private storage is not exposed");
            Console.WriteLine($"PASS: {checks} static asset short-circuit, cache validation and protected-route checks. No database configured.");
        }finally{await app.StopAsync();}
    }
}
