using System.Security.Claims;
using System.Globalization;
using System.Text;
namespace SourcingTenderManagementSystem.Web.Data;

public sealed record AccessStamp(string UserId, string Name, DateTimeOffset At)
{
    public string Label => $"STMS | {Name} | User: {UserId} | {At:yyyy-MM-dd HH:mm:ss} UTC";
    public string Mark => $"STMS | {Name} | {UserId}";
    public static AccessStamp Create(ClaimsPrincipal user, DateTimeOffset now) { var id = user.FindFirstValue(ClaimTypes.NameIdentifier); BidRules.Require(user.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(id), 403, "An authenticated account is required for a personal copy."); string Clean(string text, int limit) => string.Concat(text.EnumerateRunes().Where(r => Rune.GetUnicodeCategory(r) is not (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate)).Take(limit).Select(r => r.ToString())); return new(Clean(id, 450), Clean(user.Identity.Name ?? id, 80), now.ToUniversalTime()); }
}
public sealed record WatermarkedCopy(byte[] Bytes, string ContentType, string Name);
