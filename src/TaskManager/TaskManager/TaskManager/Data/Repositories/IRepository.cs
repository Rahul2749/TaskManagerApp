using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManager.Shared.Pagination;

namespace TaskManager.Data.Repositories
{
    /// <summary>
    /// Generic, read/write repository abstraction over an EF Core <see cref="DbSet{TEntity}"/>.
    /// All queries are tenant-filtered automatically by the <see cref="ApplicationDbContext"/>
    /// global query filters, so callers don't need to think about tenancy for standard reads.
    /// </summary>
    public interface IRepository<TEntity> where TEntity : class
    {
        /// <summary>
        /// Returns a queryable that already has the EF query filter applied (tenant scoping).
        /// Use <see cref="IncludeUnfiltered"/> to bypass the filter for platform-level reads.
        /// </summary>
        IQueryable<TEntity> Query();

        /// <summary>Unfiltered query for platform/admin scopes (SuperAdmin, seeding).</summary>
        IQueryable<TEntity> IncludeUnfiltered();

        Task<TEntity?> GetByIdAsync(int id);

        Task<List<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

        Task<PagedResult<TEntity>> ListPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null);

        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);

        Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);

        void Add(TEntity entity);
        void AddRange(IEnumerable<TEntity> entities);
        void Update(TEntity entity);
        void Remove(TEntity entity);
        void RemoveRange(IEnumerable<TEntity> entities);
    }
}
