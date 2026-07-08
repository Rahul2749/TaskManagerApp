using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskManager.Pagination;
using TaskManager.Shared.Pagination;

namespace TaskManager.Data.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IRepository{TEntity}"/>.
    /// Tenant scoping comes for free from the DbContext query filters.
    /// </summary>
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly ApplicationDbContext Context;
        protected readonly DbSet<TEntity> DbSet;

        public Repository(ApplicationDbContext context)
        {
            Context = context;
            DbSet = context.Set<TEntity>();
        }

        public IQueryable<TEntity> Query() => DbSet.AsQueryable();

        public IQueryable<TEntity> IncludeUnfiltered() => DbSet.IgnoreQueryFilters();

        public async Task<TEntity?> GetByIdAsync(int id) => await DbSet.FindAsync(id);

        public async Task<List<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
        {
            var query = Query();
            if (include != null) query = include(query);
            if (predicate != null) query = query.Where(predicate);
            return await query.ToListAsync();
        }

        public async Task<PagedResult<TEntity>> ListPagedAsync(
            int pageNumber,
            int pageSize,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null)
        {
            var query = Query();
            if (include != null) query = include(query);
            if (predicate != null) query = query.Where(predicate);
            return await query.ToPagedResultAsync(pageNumber, pageSize);
        }

        public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate) =>
            DbSet.AnyAsync(predicate);

        public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null) =>
            predicate == null ? DbSet.CountAsync() : DbSet.CountAsync(predicate);

        public void Add(TEntity entity) => DbSet.Add(entity);
        public void AddRange(IEnumerable<TEntity> entities) => DbSet.AddRange(entities);
        public void Update(TEntity entity) => DbSet.Update(entity);
        public void Remove(TEntity entity) => DbSet.Remove(entity);
        public void RemoveRange(IEnumerable<TEntity> entities) => DbSet.RemoveRange(entities);
    }
}
