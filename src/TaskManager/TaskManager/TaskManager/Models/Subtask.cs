using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// A single checklist item belonging to a task. Independent of <see cref="TaskItem.Status"/>
    /// so progress can be tracked incrementally (e.g. 3 of 5 subtasks done).
    /// </summary>
    public class Subtask
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        /// <summary>Order within the parent task's checklist (manual reordering).</summary>
        public int SortOrder { get; set; }

        public int? AssignedToId { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        // Navigation
        public TaskItem Task { get; set; } = null!;
        public User? AssignedTo { get; set; }
    }
}
