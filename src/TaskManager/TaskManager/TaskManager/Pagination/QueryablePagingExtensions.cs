using Microsoft.EntityFrameworkCore;
using TaskManager.Shared.Pagination;

namespace TaskManager.Pagination
{
    public static class QueryablePagingExtensions
    {
        /// <summary>
        /// Counts the total rows, takes the requested page, and projects the result
        /// into a <see cref="PagedResult{T}"/>. Two round-trips, but the COUNT is
        /// computed by the database rather than materializing all rows.
        /// </summary>
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> source,
            int pageNumber,
            int pageSize)
        {
            var totalCount = await source.CountAsync();

            var items = await source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}
