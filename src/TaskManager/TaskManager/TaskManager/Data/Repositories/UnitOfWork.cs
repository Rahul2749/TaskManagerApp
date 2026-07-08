using Microsoft.EntityFrameworkCore.Storage;

namespace TaskManager.Data.Repositories
{
    /// <summary>
    /// Shares a single <see cref="ApplicationDbContext"/> across every repository so that
    /// <c>SaveChangesAsync</c> flushes them as one atomic unit. Scoped lifetime matches EF Core.
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly Dictionary<Type, object> _repositories = new();
        private bool _disposed;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IRepository<Models.Organization> Organizations => Get<Models.Organization>();
        public IRepository<Models.OrganizationMember> OrganizationMembers => Get<Models.OrganizationMember>();
        public IRepository<Models.User> Users => Get<Models.User>();
        public IRepository<Models.Project> Projects => Get<Models.Project>();
        public IRepository<Models.ProjectUser> ProjectUsers => Get<Models.ProjectUser>();
        public IRepository<Models.TaskItem> Tasks => Get<Models.TaskItem>();
        public IRepository<Models.TaskHistory> TaskHistories => Get<Models.TaskHistory>();

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private IRepository<TEntity> Get<TEntity>() where TEntity : class
        {
            if (_repositories.TryGetValue(typeof(TEntity), out var cached))
                return (IRepository<TEntity>)cached;

            var repo = new Repository<TEntity>(_context);
            _repositories[typeof(TEntity)] = repo;
            return repo;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context.Dispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
