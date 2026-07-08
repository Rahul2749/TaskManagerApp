namespace TaskManager.Data.Repositories
{
    /// <summary>
    /// Unit of Work: groups multiple repository mutations into a single transactional
    /// <c>SaveChanges</c> call. Use it when a single operation must write to several
    /// aggregates atomically (e.g. creating a task and its history together).
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Models.Organization> Organizations { get; }
        IRepository<Models.OrganizationMember> OrganizationMembers { get; }
        IRepository<Models.User> Users { get; }
        IRepository<Models.Project> Projects { get; }
        IRepository<Models.ProjectUser> ProjectUsers { get; }
        IRepository<Models.TaskItem> Tasks { get; }
        IRepository<Models.TaskHistory> TaskHistories { get; }

        /// <summary>Persists all staged changes across every repository.</summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Runs <paramref name="action"/> inside an EF Core transaction. Commits on
        /// success, rolls back on exception.
        /// </summary>
        Task ExecuteInTransactionAsync(Func<Task> action);
    }
}
