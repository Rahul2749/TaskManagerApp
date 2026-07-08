namespace TaskManager.Shared.Pagination
{
    /// <summary>
    /// Bindable query parameters for any paginated endpoint.
    /// </summary>
    public class PagedQuery
    {
        private int _pageNumber = 1;
        private int _pageSize = 20;

        /// <summary>1-based page number. Clamped to a minimum of 1.</summary>
        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? 1 : value;
        }

        /// <summary>Page size, clamped to 1–100 to prevent abuse.</summary>
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value is < 1 or > 100 ? 20 : value;
        }
    }
}
