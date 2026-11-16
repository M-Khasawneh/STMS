using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace SourcingTenderManagementSystem.Web.Data;

// Translate only known persistence failures. Never retry an uncertain modifying operation.
public sealed class DatabaseFailureFilter(ILogger<DatabaseFailureFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var sql = context.Exception as SqlException ?? (context.Exception as DbUpdateException)?.InnerException as SqlException;
        var conflict = context.Exception is DbUpdateConcurrencyException || sql?.Number is 1205 or 2601 or 2627;
        var unavailable = sql?.Number is -2 or 53 or 64 or 233 or 10054 or 10060 or 4060;
        var busy = context.Exception as BidException;
        if (!conflict && !unavailable && busy?.Status is not (429 or 503)) return;
        logger.LogWarning("Database operation was not confirmed ({ErrorCode}).", sql?.Number);
        context.ExceptionHandled = true;
        context.Result = new ViewResult {
            ViewName = "~/Views/Shared/WorkspaceProblem.cshtml", StatusCode = conflict ? 409 : busy?.Status ?? 503,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState) {
                Model = busy is not null ? busy.Message : conflict ? "A concurrent change prevented confirmation. Reload the record and check its current state before trying again."
                    : "Database access is temporarily unavailable. Check the record's current state before retrying a modifying action."
            }
        };
    }
}
