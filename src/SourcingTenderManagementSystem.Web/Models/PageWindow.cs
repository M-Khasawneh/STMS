using Microsoft.EntityFrameworkCore;
namespace SourcingTenderManagementSystem.Web.Models;
public sealed record PageInfo(int Page, int Size, int Total, string? Search, string? Status)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)Size));
}
public interface IPagedResults { PageInfo Info { get; } }
public sealed class PageWindow<T>(IList<T> items, PageInfo info) : System.Collections.ObjectModel.ReadOnlyCollection<T>(items), IPagedResults
{
    public PageInfo Info { get; } = info;
    public static async Task<PageWindow<T>> ReadAsync(IQueryable<T> query, int page, string? search, string? status, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 25d)));
        return new(await query.Skip((page - 1) * 25).Take(25).ToListAsync(ct), new(page, 25, total, search, status));
    }
}
