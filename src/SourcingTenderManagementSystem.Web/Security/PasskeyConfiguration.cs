using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
namespace SourcingTenderManagementSystem.Web.Security;
public sealed class PasskeySite {
 public Uri? Origin {get;}
 public PasskeySite(string? origin){if(Uri.TryCreate(origin,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.AbsolutePath=="/"&&string.IsNullOrEmpty(uri.Query)&&string.IsNullOrEmpty(uri.Fragment)&&string.IsNullOrEmpty(uri.UserInfo))Origin=uri;}
 public bool Matches(HttpRequest request)=>Origin is not null&&request.IsHttps&&string.Equals(request.Host.Host,Origin.Host,StringComparison.OrdinalIgnoreCase)&&(request.Host.Port??443)==Origin.Port;
 public bool ValidOrigin(string origin)=>Origin is not null&&Uri.TryCreate(origin,UriKind.Absolute,out var uri)&&uri.GetLeftPart(UriPartial.Authority)==Origin.GetLeftPart(UriPartial.Authority)&&uri.AbsolutePath=="/"&&uri.Query==""&&uri.Fragment==""&&uri.UserInfo=="";
}
public static class PasskeyConfiguration {
 public static IServiceCollection AddStmsPasskeys(this IServiceCollection services,PasskeySite site){
  services.AddSingleton(site);services.AddSingleton<PasskeyCeremonies>();services.AddScoped<PasskeyManagement>();
  services.Configure<IdentityPasskeyOptions>(options=>{
   options.ServerDomain=site.Origin?.Host??"passkeys.invalid";options.UserVerificationRequirement="required";
   options.ResidentKeyRequirement="required";options.AttestationConveyancePreference="none";
   options.AuthenticatorTimeout=TimeSpan.FromMinutes(3);
   options.ValidateOrigin=context=>ValueTask.FromResult(!context.CrossOrigin&&context.TopOrigin is null&&site.Matches(context.HttpContext.Request)&&site.ValidOrigin(context.Origin));
  });
  services.AddRateLimiter(options=>{options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;var previous=options.OnRejected;options.OnRejected=async(context,ct)=>{if(context.HttpContext.Request.Path.StartsWithSegments("/Passkeys")){context.HttpContext.Response.Headers.RetryAfter="60";await context.HttpContext.Response.WriteAsync("Too many passkey requests. Wait a minute and try again. Password sign-in remains available.",ct);}else if(previous is not null)await previous(context,ct);};options.AddPolicy("passkey",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));});
  return services;
 }
}
