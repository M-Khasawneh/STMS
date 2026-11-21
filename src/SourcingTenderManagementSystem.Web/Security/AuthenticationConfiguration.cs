using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SourcingTenderManagementSystem.Web.Security;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddAuthenticationFoundation(this IServiceCollection services)
    {
        services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
        services.Configure<MvcOptions>(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.AddScoped<IEmployeeEligibility, EmployeeEligibility>();
        services.AddScoped<EmployeeSignInManager>();
        services.AddScoped<SignInManager<IdentityUser>>(provider => provider.GetRequiredService<EmployeeSignInManager>());
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.Name = "__Host-SourcingTender.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                if (context.Principal?.Identity?.IsAuthenticated != true) return;
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
                var user = await users.GetUserAsync(context.Principal);
                var eligibility = context.HttpContext.RequestServices.GetRequiredService<IEmployeeEligibility>();
                if (user is null || await users.IsLockedOutAsync(user) || !await eligibility.IsEligibleAsync(user))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("SignedInAccount", policy => policy.RequireAuthenticatedUser());
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().RequireRole(ApplicationRoles.EmployeeRoles).Build();
            options.FallbackPolicy = options.DefaultPolicy;
            foreach (var role in ApplicationRoles.EmployeeRoles)
                options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
        });
        return services;
    }
}
