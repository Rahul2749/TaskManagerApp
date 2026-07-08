namespace TaskManager.Models
{
    /// <summary>
    /// A user who "watches" a task and therefore receives notifications about its changes
    /// without being the assignee. Assignees and creators are implicitly notified; this
    /// table covers the explicit opt-in case.
    /// </summary>
    public class TaskWatcher
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public int UserId { get; set; }

        public DateTime WatchedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public TaskItem Task { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
