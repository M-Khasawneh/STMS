namespace SourcingTenderManagementSystem.Web.Models.Tenders;
public static class TenderTime
{
    public static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman");
    public static DateTimeOffset Utc(DateTime local)
    {
        var value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(value) || Zone.IsAmbiguousTime(value)) throw new ArgumentException("Select an unambiguous time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(value, Zone), TimeSpan.Zero);
    }
    public static DateTime Local(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Zone).DateTime;
    public static string Display(DateTimeOffset utc) => Local(utc).ToString("yyyy-MM-dd HH:mm");
}
