using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed class DocumentWatermarkFilter(PrivateWatermarkedCopies copies, TimeProvider clock) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Route segments are case-insensitive. Use MVC's selected action metadata,
        // never the caller's spelling, to ensure no alternate URL bypasses stamping.
        var selected = context.ActionDescriptor as ControllerActionDescriptor;
        var controller = selected?.ControllerName; var action = selected?.ActionName;
        if (action != "Document" || controller is not ("Bids" or "ReviewBids" or "Compliance") || context.Result is not FileStreamResult file) { await next(); return; }
        var ct = context.HttpContext.RequestAborted; context.HttpContext.Response.Headers.CacheControl = "no-store, private"; context.HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            await using var source = new BidDocumentDownload(file.FileStream, file.FileDownloadName, "application/octet-stream", 0);
            var marked = await copies.CreateAsync(source, AccessStamp.Create(context.HttpContext.User, clock.GetUtcNow()), ct);
            Guid BoundId(string key)
            {
                var raw = context.RouteData.Values.TryGetValue(key, out var route) ? route?.ToString() : context.HttpContext.Request.Query[key].FirstOrDefault();
                BidRules.Require(Guid.TryParse(raw, out var value), 400, "Invalid protected document reference.");
                return value;
            }
            var services = context.HttpContext.RequestServices; var id = BoundId("id");
            if (controller == "Compliance") { await using var check = await services.GetRequiredService<IComplianceManagement>().DownloadAsync(context.HttpContext.User, id, BoundId("attachmentId"), ct); }
            else { await using var check = await services.GetRequiredService<BidDocumentManagement>().DownloadAsync(context.HttpContext.User, id, BoundId("documentId"), ct); }
            var inline = string.Equals(context.HttpContext.Request.Query["view"], "true", StringComparison.OrdinalIgnoreCase); context.Result = new FileContentResult(marked.Bytes, marked.ContentType) { FileDownloadName = inline ? null : marked.Name }; if (inline) context.HttpContext.Response.Headers.ContentDisposition = "inline";
        }
        catch (BidException e) { context.Result = new ObjectResult(e.Message) { StatusCode = e.Status }; }
        await next();
    }
}
