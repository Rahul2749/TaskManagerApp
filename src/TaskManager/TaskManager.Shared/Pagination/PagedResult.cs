namespace TaskManager.Shared.Pagination
{
    /// <summary>
    /// A page of results together with the metadata the UI needs to render paging controls.
    /// </summary>
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        /// <summary>Total records across all pages.</summary>
        public int TotalCount { get; set; }

        /// <summary>Total number of pages.</summary>
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPreviousPage => PageNumber > 1;

        public bool HasNextPage => PageNumber < TotalPages;
    }
}
