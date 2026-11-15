using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
namespace SourcingTenderManagementSystem.AuthenticationChecks;
// Test assembly only. Records view names/roles, never models, URLs, identifiers or personal data.
internal sealed class BrowserViewCoverage(string path):IAsyncResultFilter
{
    private readonly ConcurrentDictionary<string,object> rows=new();
    private readonly object sync=new();
    public static void Register(IServiceCollection services)
    {
        var value=Environment.GetEnvironmentVariable("STMS_BROWSER_COVERAGE");if(string.IsNullOrEmpty(value))return;
        var target=Path.GetFullPath(value);var root=Path.GetFullPath(Directory.GetCurrentDirectory())+Path.DirectorySeparatorChar;
        if(!Path.IsPathFullyQualified(value)||target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Browser coverage must be external.");
        services.AddSingleton(new BrowserViewCoverage(target));services.Configure<MvcOptions>(o=>o.Filters.AddService<BrowserViewCoverage>());
    }
    public async Task OnResultExecutionAsync(ResultExecutingContext context,ResultExecutionDelegate next)
    {
        var done=await next();if(context.Result is not ViewResult view||done.Exception is not null)return;
        var controller=context.RouteData.Values["controller"]?.ToString()??"";
        var action=context.RouteData.Values["action"]?.ToString()??"";
        var name=view.ViewName??action;
        var key=name.StartsWith("~/Views/")?name[8..].Replace(".cshtml",""):controller+"/"+name;
        var role=context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value??"anonymous";
        rows[key+"|"+role]=new{view=key,role,mode=context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor descriptor&&descriptor.ControllerTypeInfo.Assembly==typeof(UiPagePreviewController).Assembly?"presentation":"mvc-memory",status=context.HttpContext.Response.StatusCode};
        lock(sync){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(rows.Values));}
    }
}
