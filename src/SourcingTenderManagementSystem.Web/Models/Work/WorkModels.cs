namespace SourcingTenderManagementSystem.Web.Models.Work;

public sealed class WorkRecord
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string State { get; set; } = "";
    public DateTimeOffset? At { get; set; }
}
public sealed record WorkGroup(string Key, string Title, string Controller, string Action, string Parameter,
    string StateKind, IReadOnlyList<WorkRecord> Items, long Total);
public sealed record WorkPage(bool Tasks, string Query, string? Category, int Page, IReadOnlyList<WorkGroup> Groups)
{
    public const int Size = 20;
    public long Total => Groups.Sum(g => g.Total);
    public bool More => Category is not null && Groups.Any(g => (long)Page * Size < g.Total);
}
public sealed record AuditEntry(Guid Id, string Action, string RecordType, string RecordId, DateTimeOffset At) { public AuditEntry():this(default,"","","",default){} }
public sealed record AuditPage(string Query, string? Type, DateOnly? From, DateOnly? To, int Page, long Total, IReadOnlyList<AuditEntry> Items);
