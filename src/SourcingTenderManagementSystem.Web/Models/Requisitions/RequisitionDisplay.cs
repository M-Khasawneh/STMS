using System.Text.Json;
using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models.Requisitions;

public static class RequisitionDisplay
{
    public static string Status(PurchaseRequestStatus status) => status switch
    {
        PurchaseRequestStatus.UnderReview => "Under Review",
        PurchaseRequestStatus.ConvertedToTender => "Converted to Tender",
        _ => status.ToString()
    };
    public static string Action(string action) => action switch
    {
        "Requisition.ConvertedToTender" => "Converted to tender",
        "Requisition.Created" => "Requisition created",
        "Requisition.Edited" => "Requisition updated",
        "Requisition.Submitted" => "Submitted for review",
        "Requisition.Reviewed" => "Review recorded",
        _ => action
    };
    public static string Changes(AuditLog entry)
    {
        if (entry.Action == "Requisition.Reviewed" && entry.NewStatus == "Returned" && entry.Reason is not null)
        {
            try { using var evidence = JsonDocument.Parse(entry.Reason); return evidence.RootElement.GetProperty("Comments").GetString() ?? ""; }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException) { return entry.Reason; }
        }
        if (entry.Action is not ("Requisition.Created" or "Requisition.Edited") || string.IsNullOrEmpty(entry.Reason)) return entry.Reason ?? "";
        try
        {
            using var document = JsonDocument.Parse(entry.Reason);
            var root = document.RootElement;
            var after = root.GetProperty("After");
            var before = root.GetProperty("Before");
            var lines = new List<string>();
            foreach (var (field, label) in new[] { ("Title", "Title"), ("Reason", "Reason"), ("Priority", "Priority"), ("TargetDate", "Target date") })
            {
                var value = after.GetProperty(field).GetString() ?? "";
                if (before.ValueKind == JsonValueKind.Null) lines.Add(label + ": " + value);
                else
                {
                    var previous = before.GetProperty(field).GetString() ?? "";
                    if (previous != value) lines.Add(label + ": " + previous + " -> " + value);
                }
            }
            var items = after.GetProperty("Items");
            if (before.ValueKind == JsonValueKind.Null) lines.Add("Items:\n" + Items(items));
            else if (before.GetProperty("Items").GetRawText() != items.GetRawText())
            {
                lines.Add("Previous items:\n" + Items(before.GetProperty("Items")));
                lines.Add("Updated items:\n" + Items(items));
            }
            return lines.Count == 0 ? "Saved without changes to the request content." : string.Join("\n\n", lines);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        { return "Earlier change details are stored in the audit record."; }
    }
    private static string Items(JsonElement items) => items.GetArrayLength() == 0 ? "No items" : string.Join("\n", items.EnumerateArray().Select(item =>
        item.GetProperty("Description").GetString() + " | Quantity: " + item.GetProperty("Quantity").GetDecimal().ToString("0.######") + " | Specifications: " + item.GetProperty("Specifications").GetString()));
}
