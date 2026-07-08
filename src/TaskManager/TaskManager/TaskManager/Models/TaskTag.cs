using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManager.Models
{
    /// <summary>
    /// Many-to-many join between <see cref="TaskItem"/> and <see cref="Tag"/>.
    /// Explicit join entity (rather than a skip navigation) so we can add metadata
    /// like who applied the tag and when, without a schema change later.
    /// </summary>
    public class TaskTag
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public int TagId { get; set; }

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public TaskItem Task { get; set; } = null!;
        public Tag Tag { get; set; } = null!;
    }
}
