namespace SourcingTenderManagementSystem.Web.Models;

public sealed record WorkspaceMetric(string Label, long Count, string? Controller = null, string Action = "Index");
public sealed record WorkspaceRecord(Guid Id, string Title, string Status, string? Controller = null, string Action = "Details", DateTimeOffset? At = null);
public sealed record WorkspaceSection(string Title, string Description, IReadOnlyList<WorkspaceMetric> Metrics, IReadOnlyList<WorkspaceRecord> Records);
public sealed record RoleDashboardModel(bool Available, string UserName, IReadOnlyList<string> Roles, IReadOnlyList<WorkspaceSection> Sections)
{
    public static RoleDashboardModel Unavailable => new(false, "", [], []);
}
