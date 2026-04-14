using ProductHub.Shared.Models;

namespace ProductHub.Shared.Extensions;

public static class QueryableExtensions
{
    public static PagedResult<T> ToPagedResult<T>(
        this IQueryable<T> query,
        PaginationQuery pagination)
    {
        var totalCount = query.Count();
        var items = query.Skip(pagination.Skip).Take(pagination.PageSize).ToList();
        return PagedResult<T>.Create(items, totalCount, pagination.PageNumber, pagination.PageSize);
    }
}
