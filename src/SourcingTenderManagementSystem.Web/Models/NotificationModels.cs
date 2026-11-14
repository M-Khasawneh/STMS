using SourcingTenderManagementSystem.Web.Models.Entities;

namespace SourcingTenderManagementSystem.Web.Models;

public sealed record NotificationMenu(IReadOnlyList<Notification> Items, long? Unread, bool Available)
{
    public string? ReturnUrl { get; init; }
}
public sealed record NotificationPage(IReadOnlyList<Notification> Items, int Page, long Total, long Unread, bool MessagesOnly)
{
    public bool HasNext => (long)Page * 25 < Total;
}
