using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SourcingTenderManagementSystem.Web.Data;

namespace SourcingTenderManagementSystem.Web.Security;

public static class PublicContactConfiguration
{
    public static IServiceCollection AddPublicContact(this IServiceCollection services)
    {
        services.AddScoped<ContactMessages>();
        services.AddScoped<IContactMessageStore, ContactMessageStore>();
        services.AddRateLimiter(options => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) => {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                if (context.HttpContext.Request.Path.StartsWithSegments("/Suppliers/Register")) {
                    await context.HttpContext.Response.WriteAsync("Too many registration attempts. Wait a minute before trying again.", ct); return;
                }
                await context.HttpContext.Response.WriteAsync("Please wait a minute before sending another message. Return to Contact and try again.", ct);
            };
            options.AddPolicy("supplier-registration", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 3, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                }));
            options.AddPolicy("public-contact", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = 3, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                }));
        });
        return services;
    }
}
